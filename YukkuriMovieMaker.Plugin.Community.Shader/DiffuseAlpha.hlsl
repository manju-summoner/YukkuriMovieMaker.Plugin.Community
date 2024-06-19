Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float4 color = InputTexture.Sample(InputSampler, uv0.xy);
    float alpha = max(color.r, max(color.g, color.b));
	return float4(color.rgb, alpha);
}