Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 sourceRect : packoffset(c0);
};

static const float2 InvalidSeed = float2(32768.0, 32768.0);

float SampleAlpha(float2 uv)
{
	return saturate(InputTexture.Sample(InputSampler, uv).a);
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float alpha00 = SampleAlpha(uv0.xy + float2(-1, -1) * uv0.zw);
	float alpha10 = SampleAlpha(uv0.xy + float2( 0, -1) * uv0.zw);
	float alpha20 = SampleAlpha(uv0.xy + float2( 1, -1) * uv0.zw);
	float alpha01 = SampleAlpha(uv0.xy + float2(-1,  0) * uv0.zw);
	float alpha11 = SampleAlpha(uv0.xy);
	float alpha21 = SampleAlpha(uv0.xy + float2( 1,  0) * uv0.zw);
	float alpha02 = SampleAlpha(uv0.xy + float2(-1,  1) * uv0.zw);
	float alpha12 = SampleAlpha(uv0.xy + float2( 0,  1) * uv0.zw);
	float alpha22 = SampleAlpha(uv0.xy + float2( 1,  1) * uv0.zw);

	float minAlpha = min(alpha11, min(min(min(alpha00, alpha10), min(alpha20, alpha01)), min(min(alpha21, alpha02), min(alpha12, alpha22))));
	float maxAlpha = max(alpha11, max(max(max(alpha00, alpha10), max(alpha20, alpha01)), max(max(alpha21, alpha02), max(alpha12, alpha22))));
	float threshold = 0.5;
	bool crossesThreshold = minAlpha < threshold && maxAlpha >= threshold;
	float2 pixel = posScene.xy - sourceRect.xy;
	float2 sourceSize = sourceRect.zw - sourceRect.xy;
	float alphaEpsilon = 1.0 / 255.0;
	bool isSourceBoundary = pixel.x < 1.0 || pixel.y < 1.0
		|| pixel.x >= sourceSize.x - 1.0 || pixel.y >= sourceSize.y - 1.0;
	//低被覆図形では、非厳密な局所最大かつプラトーの外周をシードにする。
	//minAlpha==0を要求しないためAA付き境界も拾い、3x3内に0.5以上がある
	//通常AA輪郭では低被覆シードを作らない。
	bool isLocalLowCoveragePeak = alpha11 >= maxAlpha - alphaEpsilon;
	bool leavesLowCoveragePlateau = minAlpha < alpha11 - alphaEpsilon || isSourceBoundary;
	bool isLowCoverageEdge = !crossesThreshold && maxAlpha < threshold
		&& alpha11 > alphaEpsilon && isLocalLowCoveragePeak && leavesLowCoveragePlateau;

	float2 gradient = float2(
		(alpha20 + 2.0 * alpha21 + alpha22) - (alpha00 + 2.0 * alpha01 + alpha02),
		(alpha02 + 2.0 * alpha12 + alpha22) - (alpha00 + 2.0 * alpha10 + alpha20));
	float gradientLength = length(gradient);
	float2 fallbackDirection = pixel.x < 1.0 ? float2(1, 0)
		: pixel.x >= sourceSize.x - 1.0 ? float2(-1, 0)
		: pixel.y < 1.0 ? float2(0, 1)
		: pixel.y >= sourceSize.y - 1.0 ? float2(0, -1)
		: float2(1, 0);
	float2 direction = gradientLength > 0.0001 ? gradient / gradientLength : fallbackDirection;
	float normalizedAlpha = isLowCoverageEdge ? alpha11 / max(maxAlpha, 1.0 / 255.0) : alpha11;
	float2 edge = pixel + (0.5 - normalizedAlpha) * direction;
	//各画素からシードまでの相対ベクトルを保持する。絶対座標をhalfへ格納しないため、
	//大きな画像でも通常の1px AA輪郭の位置精度を維持できる。
	float2 seedOffset = edge - pixel;
	float2 normalSeed = crossesThreshold ? seedOffset : InvalidSeed;
	float2 lowCoverageSeed = isLowCoverageEdge ? seedOffset : InvalidSeed;
	return float4(normalSeed, lowCoverageSeed);
}
