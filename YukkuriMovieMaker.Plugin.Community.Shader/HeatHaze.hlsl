#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
	float strength             : packoffset(c0.x);
	float noiseScale           : packoffset(c0.y);
	float angle                : packoffset(c0.z);
	float flowSpeed            : packoffset(c0.w);

	float boilSpeed            : packoffset(c1.x);
	float chromaticAberPx      : packoffset(c1.y);
	int   enableBlur           : packoffset(c1.z);
	float blurStrengthPx       : packoffset(c1.w);

	float time                 : packoffset(c2.x);
	float pad0                 : packoffset(c2.y);
	float pad1                 : packoffset(c2.z);
	float pad2                 : packoffset(c2.w);
};

float4 SampleInput(float2 uv)
{
	if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
		return float4(0.0f, 0.0f, 0.0f, 0.0f);
	return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float2 SmoothNoise3D(float3 p)
{
	float3 i = floor(p);
	float3 f = frac(p);
	float3 u = f * f * (3.0f - 2.0f * f);

	float2 n000 = hash23(i + float3(0.0f, 0.0f, 0.0f));
	float2 n100 = hash23(i + float3(1.0f, 0.0f, 0.0f));
	float2 n010 = hash23(i + float3(0.0f, 1.0f, 0.0f));
	float2 n110 = hash23(i + float3(1.0f, 1.0f, 0.0f));
	float2 n001 = hash23(i + float3(0.0f, 0.0f, 1.0f));
	float2 n101 = hash23(i + float3(1.0f, 0.0f, 1.0f));
	float2 n011 = hash23(i + float3(0.0f, 1.0f, 1.0f));
	float2 n111 = hash23(i + float3(1.0f, 1.0f, 1.0f));

	float2 nxy0 = lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y);
	float2 nxy1 = lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y);
	return lerp(nxy0, nxy1, u.z) * 2.0f - 1.0f;
}

float2 Fbm(float3 p)
{
	float2 value = float2(0.0f, 0.0f);
	float amplitude = 0.5f;

	[unroll]
	for (int i = 0; i < 6; i++)
	{
		value += amplitude * SmoothNoise3D(p);
		p *= 2.0f;
		amplitude *= 0.5f;
	}
	return value;
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_TARGET
{
	float cosA = cos(angle);
	float sinA = sin(angle);
	float2 dir = float2(cosA, sinA);

	float2 flow = dir * (time * flowSpeed * noiseScale);

	float3 noiseCoord = float3(
		uv0.x * noiseScale + flow.x,
		uv0.y * noiseScale + flow.y,
		time * boilSpeed
	);
	float2 disp = Fbm(noiseCoord);

	float2 rotatedDisp = float2(
		disp.x * cosA - disp.y * sinA,
		disp.x * sinA + disp.y * cosA
	);

	float2 dispUV = rotatedDisp * strength * 0.1f;
	float2 sampleUV = uv0.xy + dispUV;

	float4 result;

	if (enableBlur != 0 && blurStrengthPx > 0.0f)
	{
		float2 blurStepUV = dir * float2(blurStrengthPx * uv0.z, blurStrengthPx * uv0.w);
		static const float kWeights[5] = { 0.0625f, 0.25f, 0.375f, 0.25f, 0.0625f };
		float4 blurred = float4(0.0f, 0.0f, 0.0f, 0.0f);

		[unroll]
		for (int i = 0; i < 5; i++)
		{
			float t = (float)(i - 2);
			blurred += kWeights[i] * SampleInput(sampleUV + blurStepUV * t);
		}
		result = blurred;
	}
	else
	{
		result = SampleInput(sampleUV);
	}

	if (chromaticAberPx > 0.0f)
	{
		float2 aberUV = float2(dir.x * chromaticAberPx * uv0.z, dir.y * chromaticAberPx * uv0.w);
		result.r = SampleInput(sampleUV - aberUV).r;
		result.b = SampleInput(sampleUV + aberUV).b;
	}

	return result;
}
