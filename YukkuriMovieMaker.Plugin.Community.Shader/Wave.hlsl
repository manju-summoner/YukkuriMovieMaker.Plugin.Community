Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float angle : packoffset(c0.x);
	float angle2 : packoffset(c0.y);
	float amplitude : packoffset(c0.z);
	float waveLength : packoffset(c0.w);
	float phase : packoffset(c1.x);
};

float GetPosition(float2 direction, float2 pos, float waveLength)
{
	float2 posDirection = dot(direction, pos);
	float position = waveLength == 0 ? 0 : length(posDirection) / waveLength;

	float t = atan2(posDirection.x, posDirection.y);
	return -3.14 / 2 < t && t < 3.14 / 2 ? position : -position;
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv : TEXCOORD0
) : SV_TARGET
{
	const float pi = 3.141592653589f;

	float freq = 1;
	float2 direction = float2(cos(angle), sin(angle));
	float2 direction2 = float2(cos(angle + angle2), sin(angle + angle2));
	float position = GetPosition(direction, posScene.xy, waveLength);
	float2 delta = direction2 * sin(2 * pi * phase + position) * amplitude * uv.zw;
	float4 color = InputTexture.Sample(InputSampler, uv.xy + delta);
	return color;
}