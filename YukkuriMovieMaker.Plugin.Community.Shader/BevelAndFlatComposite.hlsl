Texture2D InputTexture0 : register(t0);
SamplerState InputSampler0 : register(s0);
Texture2D InputTexture1 : register(t1);
SamplerState InputSampler1 : register(s1);
Texture2D InputTexture2 : register(t2);
SamplerState InputSampler2 : register(s2);

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0,
	float4 uv1 : TEXCOORD1,
	float4 uv2 : TEXCOORD2
) : SV_Target
{
	bool isBevel = false;
	for (int yi = -1; yi <= 1; yi++)
	{
		for (int xi = -1; xi <= 1; xi++)
		{
			float2 uv = uv0.xy + float2(xi, yi) * uv0.zw;
			float4 color = InputTexture0.Sample(InputSampler0, uv);
			isBevel = isBevel || (color.a != 0 && color.a != 1);
		}
	}
	
	float4 color0 = InputTexture0.Sample(InputSampler0, uv0.xy);
	float4 color1 = InputTexture1.Sample(InputSampler1, uv1.xy);
	float4 color2 = InputTexture2.Sample(InputSampler2, uv2.xy);

	return isBevel ? color2 : color1;
}