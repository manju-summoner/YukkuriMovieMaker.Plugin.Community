Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 sourceRect : packoffset(c0);
};

static const float InvalidSeed = -1.0;

float4 EncodeSeed(float2 position)
{
	float2 value = (position + 1.0) / 16.0;
	float2 high = floor(value) / 256.0;
	float2 low = frac(value);
	return float4(high.x, low.x, high.y, low.y);
}

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
	bool isLowCoverageEdge = !crossesThreshold && minAlpha <= (1.0 / 255.0) && maxAlpha > (1.0 / 255.0);
	if (!crossesThreshold && !isLowCoverageEdge)
		return float4(InvalidSeed, 0, 0, 0);

	float2 gradient = float2(
		(alpha20 + 2.0 * alpha21 + alpha22) - (alpha00 + 2.0 * alpha01 + alpha02),
		(alpha02 + 2.0 * alpha12 + alpha22) - (alpha00 + 2.0 * alpha10 + alpha20));
	float gradientLength = length(gradient);
	float2 direction = gradientLength > 0.0001 ? gradient / gradientLength : float2(1, 0);
	float2 pixel = posScene.xy - sourceRect.xy;
	float normalizedAlpha = isLowCoverageEdge ? alpha11 / max(maxAlpha, 1.0 / 255.0) : alpha11;
	float2 edge = pixel + (0.5 - normalizedAlpha) * direction;
	return EncodeSeed(edge);
}
