Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float threshold : packoffset(c0.x);
	float smoothness : packoffset(c0.y);
	int mode : packoffset(c0.z);
	int isInvert : packoffset(c0.w);
};

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float4 color = InputTexture.Sample(InputSampler, uv0.xy);
	float3 rgb = color.a == 0 ? float3(0, 0, 0) : color.rgb / color.a;
	float luminance = dot(rgb, float3(0.2126, 0.7152, 0.0722));

	float alpha;
	if (mode == 0)
	{
		//暗い部分を透過
		if(smoothness == 0)
			alpha = luminance < threshold ? 0 : 1;
		else
			alpha = saturate(1 - (threshold - luminance) / smoothness);
	}
	else if (mode == 1)
	{
		//しきい値部分を透過
		if (smoothness == 0)
			alpha = abs(luminance - threshold) < 1 / 255 ? 0 : 1;
		else
			alpha = saturate(abs(luminance - threshold) / smoothness);
	}
	else if (mode == 2)
	{
		//しきい値部分を透過（範囲）
		alpha = abs(luminance - threshold) < smoothness ? 0 : 1;
	}
	else
	{
		alpha = 1;
	}

	if(isInvert == 1)
		alpha = 1 - alpha;
	
	return color * alpha;
}