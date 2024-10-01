Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float angle : packoffset(c0.x);
	float maxRadius : packoffset(c0.y);
	bool isRotateOuter : packoffset(c0.z);
};

float2 rotate(float2 uv, float angle)
{
	float s = sin(angle);
	float c = cos(angle);
	return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
}
float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float radius = length(posScene.xy);
	float2 center = uv0.xy - posScene.xy * uv0.zw;

	float t = radius / maxRadius * angle * (isRotateOuter ? -1 : 1) + (isRotateOuter ? 0 : -angle);
	float2 uv = center + rotate(uv0.xy - center, t);
	float4 color = InputTexture.Sample(InputSampler, uv.xy);
	return color;
}