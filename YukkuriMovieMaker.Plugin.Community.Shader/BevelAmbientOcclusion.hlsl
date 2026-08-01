Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
Texture2D HeightTexture : register(t1);
SamplerState HeightSampler : register(s1);

cbuffer constants : register(b0)
{
	float strength : packoffset(c0.x);
	float distance : packoffset(c0.y);
	float bias : packoffset(c0.z);
	float softness : packoffset(c0.w);
	float surfaceScale : packoffset(c1.x);
	int stepCount : packoffset(c1.y);
};

#define MAX_STEPS 8
#define DIRECTION_COUNT 4

float Height(float2 uv)
{
	return saturate(HeightTexture.SampleLevel(HeightSampler, uv, 0).a) * surfaceScale;
}

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0, float4 uv1 : TEXCOORD1) : SV_Target
{
	float4 source = SourceTexture.Sample(SourceSampler, uv0.xy);
	if (strength <= 0.0 || distance <= 0.0 || surfaceScale <= 0.0 || source.a <= 0.0)
		return source;

	float centerHeight = Height(uv1.xy);
	int steps = clamp(stepCount, 4, MAX_STEPS);
	float aoSum = 0.0;
	[unroll]
	for (int directionIndex = 0; directionIndex < DIRECTION_COUNT; directionIndex++)
	{
		float angle = 6.283185307 * ((float)directionIndex + 0.125) / DIRECTION_COUNT;
		float2 direction;
		sincos(angle, direction.y, direction.x);
		float directionOcclusion = 0.0;
		[loop]
		for (int stepIndex = 0; stepIndex < MAX_STEPS; stepIndex++)
		{
			if (stepIndex >= steps) break;
			float fraction = ((float)stepIndex + 0.5) / steps;
			float sampleDistance = distance * fraction * fraction;
			float blocker = Height(uv1.xy + direction * sampleDistance * uv1.zw) - centerHeight - bias;
			float sampleOcclusion = softness > 0.0001 ? smoothstep(0.0, softness, blocker) : step(0.0001, blocker);
			sampleOcclusion *= saturate(1.0 - sampleDistance / distance);
			directionOcclusion = max(directionOcclusion, sampleOcclusion);
		}
		aoSum += directionOcclusion;
	}

	float ao = saturate(aoSum / DIRECTION_COUNT) * saturate(strength);
	return float4(source.rgb * (1.0 - ao), source.a);
}
