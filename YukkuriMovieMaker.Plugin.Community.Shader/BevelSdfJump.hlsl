Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 sourceRect : packoffset(c0);
	int stepSize : packoffset(c1.x);
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

void SelectSeed(float4 candidate, float2 pixel, inout float4 best, inout float bestDistance)
{
	if (!IsValid(candidate))
		return;

	float2 seedPosition = DecodeSeed(candidate);
	float distanceSquared = dot(seedPosition - pixel, seedPosition - pixel);
	float2 bestPosition = IsValid(best) ? DecodeSeed(best) : float2(1e10, 1e10);
	bool isTieBreakWinner = abs(distanceSquared - bestDistance) <= 0.0001
		&& (seedPosition.y < bestPosition.y || (seedPosition.y == bestPosition.y && seedPosition.x < bestPosition.x));
	if (distanceSquared < bestDistance || isTieBreakWinner)
	{
		best = candidate;
		bestDistance = distanceSquared;
	}
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float2 pixel = posScene.xy - sourceRect.xy;
	float4 best = InputTexture.Sample(InputSampler, uv0.xy);
	float bestDistance = IsValid(best) ? dot(DecodeSeed(best) - pixel, DecodeSeed(best) - pixel) : 1e20;

	[unroll]
	for (int y = -1; y <= 1; y++)
	{
		[unroll]
		for (int x = -1; x <= 1; x++)
		{
			if (x == 0 && y == 0)
				continue;

			float2 candidateScene = posScene.xy + float2(x, y) * stepSize;
			if (candidateScene.x < sourceRect.x || candidateScene.y < sourceRect.y
				|| candidateScene.x >= sourceRect.z || candidateScene.y >= sourceRect.w)
				continue;

			float2 candidateUv = uv0.xy + float2(x, y) * stepSize * uv0.zw;
			SelectSeed(InputTexture.Sample(InputSampler, candidateUv), pixel, best, bestDistance);
		}
	}

	return best;
}
