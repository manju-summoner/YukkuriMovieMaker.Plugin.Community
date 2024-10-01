Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float threshold : packoffset(c0.x);
	bool isInverted : packoffset(c0.y);
	bool keepColor : packoffset(c0.z);
};

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float4 color = InputTexture.Sample(InputSampler, uv0.xy);

	float v = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
	float rate;
	if (v <= threshold)
		rate = 0;
	else
		rate = 1;

	if (isInverted)
		rate = 1 - rate;

	if (keepColor)
		return color * rate;
	else
		return float4(rate, rate, rate, 1) * color.a;
}