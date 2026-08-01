Texture2D SeedTexture : register(t0);
SamplerState SeedSampler : register(s0);
Texture2D SourceTexture : register(t1);
SamplerState SourceSampler : register(s1);

cbuffer constants : register(b0)
{
	float4 sourceRect : packoffset(c0);
	float thickness : packoffset(c1.x);
	int mode : packoffset(c1.y);
};

bool IsValid(float4 seed)
{
	return seed.r >= 0.0;
}

float2 DecodeSeed(float4 seed)
{
	return float2(
		(seed.r * 256.0 + seed.g) * 16.0 - 1.0,
		(seed.b * 256.0 + seed.a) * 16.0 - 1.0);
}

float ApplyBevelProfile(float height)
{
	if (mode == 0)
		return height;
	if (mode == 1)
		return sqrt(saturate(height * (2.0 - height)));
	if (mode == 2)
		return 1.0 - sqrt(saturate(1.0 - height * height));
	if (mode == 3)
		return step(1.0, height);
	if (mode == 4)
		return 1.0 - abs(height * 2.0 - 1.0);
	if (mode == 5)
		return sqrt(saturate(1.0 - (1.0 - height * 2.0) * (1.0 - height * 2.0)));
	return height;
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0,
	float4 uv1 : TEXCOORD1
) : SV_Target
{
	float sourceAlpha = saturate(SourceTexture.Sample(SourceSampler, uv1.xy).a);
	float4 seed = SeedTexture.Sample(SeedSampler, uv0.xy);
	float height = 0.0;
	if (sourceAlpha > 0.0 && thickness > 0.0)
	{
		float normalizedDistance = 1.0;
		if (IsValid(seed))
		{
			float2 pixel = posScene.xy - sourceRect.xy;
			float distanceToEdge = length(DecodeSeed(seed) - pixel);
			normalizedDistance = saturate(distanceToEdge / max(thickness, 1.0));
		}
		height = ApplyBevelProfile(normalizedDistance);
	}

	return float4(height, height, height, 1.0);
}
