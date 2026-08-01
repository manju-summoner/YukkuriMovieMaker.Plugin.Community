Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 sourceRect : packoffset(c0);
	int stepSize : packoffset(c1.x);
};

bool IsValid(float2 seed)
{
	return max(abs(seed.x), abs(seed.y)) < 30000.0;
}

void SelectSeed(float2 storedCandidate, float2 candidatePixelOffset, inout float2 best, inout float bestDistance)
{
	if (!IsValid(storedCandidate))
		return;

	//隣接画素基準の相対ベクトルを、現在画素基準へ直して比較・保存する。
	float2 candidate = storedCandidate + candidatePixelOffset;
	float distanceSquared = dot(candidate, candidate);
	float2 bestPosition = IsValid(best) ? best : float2(1e10, 1e10);
	bool isTieBreakWinner = abs(distanceSquared - bestDistance) <= 0.0001
		&& (candidate.y < bestPosition.y || (candidate.y == bestPosition.y && candidate.x < bestPosition.x));
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
	float4 current = InputTexture.Sample(InputSampler, uv0.xy);
	float2 bestNormal = current.rg;
	float2 bestLowCoverage = current.ba;
	float bestNormalDistance = IsValid(bestNormal) ? dot(bestNormal, bestNormal) : 1e20;
	float bestLowCoverageDistance = IsValid(bestLowCoverage) ? dot(bestLowCoverage, bestLowCoverage) : 1e20;

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
			float4 candidate = InputTexture.Sample(InputSampler, candidateUv);
			float2 candidatePixelOffset = float2(x, y) * stepSize;
			SelectSeed(candidate.rg, candidatePixelOffset, bestNormal, bestNormalDistance);
			SelectSeed(candidate.ba, candidatePixelOffset, bestLowCoverage, bestLowCoverageDistance);
		}
	}

	return float4(bestNormal, bestLowCoverage);
}
