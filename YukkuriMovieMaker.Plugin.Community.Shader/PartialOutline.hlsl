Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
	float angle         : packoffset(c0.x);
	float bandCenter    : packoffset(c0.y);
	float halfBandWidth : packoffset(c0.z);
	float softness      : packoffset(c0.w);
	float centerX       : packoffset(c1.x);
	float centerY       : packoffset(c1.y);
	float pad0          : packoffset(c1.z);
	float pad1          : packoffset(c1.w);
};

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_TARGET
{
	float4 color = InputTexture.SampleLevel(InputSampler, uv0.xy, 0);

	float px = posScene.x - centerX;
	float py = posScene.y - centerY;

	float cosA = cos(angle);
	float sinA = -sin(angle);
	float proj = px * cosA + py * sinA;

	float dist = abs(proj - bandCenter);
	float edgeWidth = softness * halfBandWidth;
	float mask = 1.0f - smoothstep(halfBandWidth - edgeWidth, halfBandWidth + edgeWidth, dist);

	return color * mask;
}
