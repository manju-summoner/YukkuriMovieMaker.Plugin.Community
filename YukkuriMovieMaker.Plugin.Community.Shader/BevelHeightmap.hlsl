Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float thickness : packoffset(c0.x);
	int mode : packoffset(c0.y);
};

float CalculateHeight(float4 uv0)
{
	if (thickness <= 0.0)
		return 0.0;

	// 1px未満でも画素中心でベベルを表現できるよう、距離場の最小幅を1pxにする。
	float effectiveThickness = max(thickness, 1.0);
	float range = ceil(effectiveThickness);
	float distance = effectiveThickness;

	[loop]
		for (int yi = -range; yi <= range; yi++)
		{
			[loop]
				for (int xi = -range; xi <= range; xi++)
				{
					float2 delta = float2(xi, yi);
					float2 uv1 = uv0.xy + delta * uv0.zw;

					float pixelDistance = length(delta);
					if (pixelDistance > effectiveThickness)
						continue;

					float4 color = InputTexture.Sample(InputSampler, uv1.xy);
					float coverage = saturate(color.a);
					if (coverage < 1)
					{
						// coverage=0.5を輪郭とみなし、画素中心から輪郭までの
						// サブピクセル距離を線形coverageから近似する。
						float boundaryDistance = max(0.0, pixelDistance + coverage - 0.5);
						distance = min(distance, boundaryDistance);
						if (distance == 0)
							break;
					}
				}
			if (distance == 0)
				break;
		}

	float height;
	height = saturate(distance / effectiveThickness);
	
	if (mode == 0) 
	{
		//角面
		return height;
	}
	else if (mode == 1)
	{
		//丸面
		return sin(acos(1 - height));
	}
	else if (mode == 2)
	{
		//匙面
		return 1 - sin(acos(height));

	}
	else if (mode == 3)
	{
		//しゃくり面
		return step(1, height);
	}
	else if (mode == 4)
	{
		//ときん面
		return 1 - abs(height * 2 - 1);
	}
	else if (mode == 5)
	{
		//紐面
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
