Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float thickness : packoffset(c0.x);
	int mode : packoffset(c0.y);
};

float CalculateHeight(float4 uv0)
{
	float range = ceil(thickness);
	float distance = thickness;

	[loop]
		for (int yi = -range; yi <= range; yi++)
		{
			[loop]
				for (int xi = -range; xi <= range; xi++)
				{
					float2 delta = float2(xi, yi);
					float2 uv1 = uv0.xy + delta * uv0.zw;

					if (length(delta) > thickness)
						continue;

					float4 color = InputTexture.Sample(InputSampler, uv1.xy);
					if (color.a == 0)
					{
						distance = min(distance, length(delta));
						if (distance == 0)
							break;
					}
				}
			if (distance == 0)
				break;
		}

	float height;
	if (thickness != 0)
		height = distance / thickness;
	else
		height = distance == 0 ? 0 : 1;
	
	if (mode == 0) 
	{
		//äpñ 
		return height;
	}
	else if (mode == 1)
	{
		//ä€ñ 
		return sin(acos(1 - height));
	}
	else if (mode == 2)
	{
		//ç˙ñ 
		return 1 - sin(acos(height));

	}
	else if (mode == 3)
	{
		//ÇµÇ·Ç≠ÇËñ 
		return step(1, height);
	}
	else if (mode == 4)
	{
		//Ç∆Ç´ÇÒñ 
		return 1 - abs(height * 2 - 1);
	}
	else if (mode == 5)
	{
		//ïRñ 
		return sin(acos(1 - height * 2));
	}
	return height;
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float height = CalculateHeight(uv0);
	return float4(height, height, height, 1);
}