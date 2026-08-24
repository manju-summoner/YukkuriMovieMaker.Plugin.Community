// 異方性クワハラフィルター パス1: 構造テンソルの計算
//
// 入力画像から Sobel 勾配 (gx, gy) を求め、局所構造テンソル
//   E = [ gx*gx  gx*gy ]
//       [ gx*gy  gy*gy ]
// を出力する。中間バッファは 8bpc UNORM [0,1] のため、負値や 1 を超える値を
// 取り得るテンソル成分はエンコードして格納する。
//   R = gx*gx          (>=0, Sobel を 1/4 正規化するので [0,1])
//   G = gy*gy          (>=0, 同上)
//   B = gx*gy*0.5+0.5  (符号付きを [0,1] へ)
// エンコードはアフィン変換なので、パス2 のガウス平滑化はこの RGB を
// そのままぼかすだけで「平滑化後テンソルのエンコード」と一致する
// (重み総和=1 のとき凸結合とアフィン変換が可換)。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

float4 SampleInput(float2 uv)
{
	if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
		return float4(0.0f, 0.0f, 0.0f, 0.0f);
	return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float Luminance(float2 uv)
{
	float4 c = SampleInput(uv);
	// 直線 RGB の輝度 (Rec.709)。プリマルチプライ済みでも透明部は rgb=0。
	return dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_TARGET
{
	float2 t = uv0.zw; // テクセルサイズ (UV/px)
	float2 c = uv0.xy;

	// 3x3 Sobel
	float tl = Luminance(c + float2(-t.x, -t.y));
	float tc = Luminance(c + float2( 0.0f, -t.y));
	float tr = Luminance(c + float2( t.x, -t.y));
	float ml = Luminance(c + float2(-t.x, 0.0f));
	float mr = Luminance(c + float2( t.x, 0.0f));
	float bl = Luminance(c + float2(-t.x, t.y));
	float bc = Luminance(c + float2( 0.0f, t.y));
	float br = Luminance(c + float2( t.x, t.y));

	// 正の重み総和 4 で割って概ね [-1,1] に正規化
	float gx = ((tr + 2.0f * mr + br) - (tl + 2.0f * ml + bl)) * 0.25f;
	float gy = ((bl + 2.0f * bc + br) - (tl + 2.0f * tc + tr)) * 0.25f;

	float gxx = gx * gx;
	float gyy = gy * gy;
	float gxy = gx * gy;

	return float4(gxx, gyy, gxy * 0.5f + 0.5f, 1.0f);
}
