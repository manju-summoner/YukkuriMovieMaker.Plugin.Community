// 異方性クワハラフィルター パス3: 本体
//
// 入力0: 元画像(色, プリマルチプライ済み)
// 入力1: パス2 が出力した平滑化済み構造テンソル (R=gxx, G=gyy, B=encoded gxy)
//
// 1. テンソルを固有値分解し、局所の流れ方向(エッジ接線)と異方性 A を求める
// 2. 円形カーネルを A に応じて楕円に歪める(エッジに沿って伸長)
// 3. 楕円内を N×N 格子で走査し、各サンプルを角度で 8 扇形へ振り分け
//    (隣接扇形へ線形ブレンドしてバンディングを抑制)、色と色^2 を蓄積
// 4. 各扇形の平均と分散を求め、分散の小さい扇形ほど大きい重みで加重平均
//    w_k = 1 / (1 + sigma_k^q)  ← エッジをまたがず筆致のように塗る

Texture2D ColorTexture  : register(t0);
SamplerState ColorSampler : register(s0);
Texture2D TensorTexture : register(t1);
SamplerState TensorSampler : register(s1);

static const int SECTORS = 8;
// 半径24の円内格子点数 N(24)=1793。C#側 MaxSampleCount と一致させること(導出はそちらのコメント参照)
static const int MAX_SAMPLES = 1793;

cbuffer Constants : register(b0)
{
	float radiusPx    : packoffset(c0.x); // 筆の半径(px)
	float sharpness   : packoffset(c0.y); // 鮮鋭度 q
	float anisotropy  : packoffset(c0.z); // 異方性 0..1
	int   sampleCount : packoffset(c0.w);
	float4 samples[MAX_SAMPLES] : packoffset(c1);
};

// 色が [0,1] 正規化のため標準偏差は小さい。エッジ抑制を効かせるには
// std を O(1) にスケールしてから応答関数 1/(1+(HARDNESS*std)^q) に通す。
// この係数で std≈1/HARDNESS 付近を「エッジ判定のしきい値」とする。
static const float HARDNESS = 8.0f;

float4 SampleColor(float2 uv)
{
	if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
		return float4(0.0f, 0.0f, 0.0f, 0.0f);
	return ColorTexture.SampleLevel(ColorSampler, uv, 0);
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0,
	float4 uv1      : TEXCOORD1
) : SV_TARGET
{
	float2 texel = uv0.zw;

	// --- 平滑化テンソルを取得してデコード ---
	float4 T = TensorTexture.SampleLevel(TensorSampler, uv1.xy, 0);
	float gxx = T.x;
	float gyy = T.y;
	float gxy = T.z * 2.0f - 1.0f;

	// --- 2x2 対称テンソルの固有値分解 ---
	float tr = gxx + gyy;
	float disc = sqrt(max((gxx - gyy) * (gxx - gyy) + 4.0f * gxy * gxy, 0.0f));
	float lambda1 = 0.5f * (tr + disc); // 大(勾配方向)
	float lambda2 = 0.5f * (tr - disc); // 小(エッジ接線方向)
	// テンソルは半正定値なので理論上 A は [0,1] に収まるが、中間バッファが 8bpc UNORM で
	// 量子化されて半正定値性が崩れると lambda2 が負に振れ、A が 1 を大きく超えることがある。
	// A が暴れると長軸が MapOutputRectToInputRects で宣言した余白を超えてサンプリングし、
	// 入力矩形外の未定義テクセルを読んで出力に NaN/巨大値が混入するため saturate で抑える。
	float A = (tr > 1e-6f) ? saturate((lambda1 - lambda2) / tr) : 0.0f;

	// 勾配方向の固有ベクトル (gxy, lambda1 - gxx)。ほぼ等方なら向きは任意。
	float2 grad = float2(gxy, lambda1 - gxx);
	float glen = length(grad);
	float2 gradDir = (glen > 1e-6f) ? grad / glen : float2(1.0f, 0.0f);
	float2 tangent = float2(-gradDir.y, gradDir.x); // エッジ接線 = 勾配の直交

	// --- 異方性に応じた楕円 (面積保存、エッジ沿いに伸長) ---
	float aniso = A * saturate(anisotropy);
	float major = radiusPx * (1.0f + aniso); // エッジ接線方向(長軸)
	float minor = radiusPx / (1.0f + aniso); // 勾配方向(短軸)

	float2 majorUv = major * tangent * texel;
	float2 minorUv = minor * gradDir * texel;

	// 扇形ごとの蓄積 (プリマルチプライ色をそのまま平均)
	float4 mSum[SECTORS];
	float3 m2Sum[SECTORS];
	float  wSum[SECTORS];
	[unroll]
	for (int s = 0; s < SECTORS; s++)
	{
		mSum[s]  = float4(0.0f, 0.0f, 0.0f, 0.0f);
		m2Sum[s] = float3(0.0f, 0.0f, 0.0f);
		wSum[s]  = 0.0f;
	}

	[loop]
	for (int idx = 0; idx < sampleCount; idx++)
	{
		float4 sp = samples[idx];
		float4 c = SampleColor(uv0.xy + sp.x * majorUv + sp.y * minorUv);

		int k0 = (int)sp.w;
		int k1 = (k0 + 1) & (SECTORS - 1);
		float fr = sp.w - (float)k0;

		float w0 = sp.z * (1.0f - fr);
		float w1 = sp.z * fr;

		float3 c2 = c.rgb * c.rgb;
		mSum[k0]  += w0 * c;   m2Sum[k0] += w0 * c2;  wSum[k0] += w0;
		mSum[k1]  += w1 * c;   m2Sum[k1] += w1 * c2;  wSum[k1] += w1;
	}

	// --- 分散が小さい扇形ほど大きい重みで加重平均 ---
	float4 result = float4(0.0f, 0.0f, 0.0f, 0.0f);
	float totalW = 0.0f;
	float q = max(sharpness, 0.0f);

	[unroll]
	for (int k = 0; k < SECTORS; k++)
	{
		float wk = wSum[k];
		if (wk < 1e-6f)
			continue;
		float4 mean = mSum[k] / wk;
		float3 var = max(m2Sum[k] / wk - mean.rgb * mean.rgb, 0.0f);
		float sigma2 = var.r + var.g + var.b;
		float sigma = sqrt(sigma2);
		// 分散が小さい(=均質な)扇形ほど大きい重み。HARDNESS で std を O(1) に
		// スケールし、std がしきい値を超えるエッジ跨ぎ扇形を q に応じて抑制する。
		// pow(0, 0) は exp2(0 * log2(0)) = NaN になるため底に下限を入れる(q>=1 では影響しない)
		float weight = 1.0f / (1.0f + pow(max(HARDNESS * sigma, 1e-8f), q));
		result += weight * mean;
		totalW += weight;
	}

	if (totalW < 1e-6f)
		return SampleColor(uv0.xy);

	return result / totalW;
}
