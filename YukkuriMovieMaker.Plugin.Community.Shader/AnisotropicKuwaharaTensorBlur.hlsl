// 異方性クワハラフィルター パス2: 構造テンソルの平滑化
//
// パス1 が出力したエンコード済みテンソル (R=gxx, G=gyy, B=encoded gxy) を
// ガウスぼかしして局所オリエンテーションを安定化する。
// エンコードはアフィン変換なので、エンコード済み RGB を直接ぼかせば
// 「平滑化後テンソルのエンコード」に一致する(重み総和=1)。
// アルファ(=1)もぼかされるが 1 のまま変わらない。
//
// 平滑化半径は筆半径から独立した小さめの固定値とする(Kyprianidis 2009 に倣う)。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

static const int   BLUR_RADIUS = 5;
static const float BLUR_SIGMA  = 2.6f;

float4 SampleTensor(float2 uv)
{
	// テンソルは境界外を「エッジ無し」= (0,0,0.5) として扱う
	if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
		return float4(0.0f, 0.0f, 0.5f, 1.0f);
	return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_TARGET
{
	float2 t = uv0.zw;
	float2 c = uv0.xy;

	float inv2s2 = 1.0f / (2.0f * BLUR_SIGMA * BLUR_SIGMA);
	float4 sum = float4(0.0f, 0.0f, 0.0f, 0.0f);
	float wsum = 0.0f;

	[loop]
	for (int j = -BLUR_RADIUS; j <= BLUR_RADIUS; j++)
	{
		[loop]
		for (int i = -BLUR_RADIUS; i <= BLUR_RADIUS; i++)
		{
			float2 o = float2((float)i, (float)j);
			float w = exp(-dot(o, o) * inv2s2);
			sum += w * SampleTensor(c + o * t);
			wsum += w;
		}
	}

	return sum / max(wsum, 1e-6f);
}
