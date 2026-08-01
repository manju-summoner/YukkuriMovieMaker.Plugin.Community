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

bool IsValid(float2 seed)
{
	return max(abs(seed.x), abs(seed.y)) < 30000.0;
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
	float4 seeds = SeedTexture.Sample(SeedSampler, uv0.xy);
	float2 normalSeed = seeds.rg;
	float2 lowCoverageSeed = seeds.ba;
	float height = 0.0;
	bool isThresholdInterior = sourceAlpha >= 0.5;
	bool isLowCoverageInterior = sourceAlpha > (1.0 / 255.0) && sourceAlpha < 0.5 && IsValid(lowCoverageSeed);
	//通常領域と低被覆領域は、それぞれ独立して伝播した距離場だけを使う。
	//低被覆シードが無い通常図形のAA外縁は対象外とし、外側ベベルを抑える。
	if ((isThresholdInterior || isLowCoverageInterior) && thickness > 0.0)
	{
		float normalizedDistance = 1.0;
		float2 seed = isThresholdInterior ? normalSeed : lowCoverageSeed;
		if (IsValid(seed))
		{
			float distanceToEdge = length(seed);
			normalizedDistance = saturate(distanceToEdge / max(thickness, 1.0));
		}
		height = ApplyBevelProfile(normalizedDistance);
	}

	return float4(height, height, height, 1.0);
}
