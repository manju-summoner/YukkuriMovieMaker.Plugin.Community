Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float thickness : packoffset(c0.x);
}; 

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float4 color0 = InputTexture.Sample(InputSampler, uv0.xy);
	if (color0.a == 0) {
		return color0;
	}

    int range = ceil(thickness);
	float alpha = 1;

	[loop]
	for (int yi = -range; yi <= range; yi++)
	{
		[loop]
		for (int xi = -range; xi <= range; xi++)
		{
			float2 delta = float2(xi, yi);
			float2 uv = uv0.xy + delta * uv0.zw;
			
			if(length(delta) > thickness)
				continue;

			float4 color = InputTexture.Sample(InputSampler, uv.xy);
			alpha = min(alpha, color.a);
		}
	}
	return color0 * alpha;
}