Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 color1 : packoffset(c0);
	float4 color2 : packoffset(c1);

	float2 center : packoffset(c2.x);
	float lengthRate : packoffset(c2.z);
	float opacity : packoffset(c2.w);
	float attenuation : packoffset(c3.x);
};

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float a = 0;
	float4 back = 0;

	float2 from = (posScene.xy - center.xy) / (1 - lengthRate) + center.xy;
	float2 dv = from - posScene.xy;
	float2 e = dv / length(dv);

	float shadowLength = length(dv);
	[loop]
	for (int i = 0; i <= shadowLength; i++)
	{
		float2 v = e * i;

		float4 current = InputTexture.Sample(InputSampler, uv0.xy + v * uv0.zw);
		float currentAlpha = current.a;
		currentAlpha *= opacity;
		currentAlpha *= (1 - attenuation) + attenuation * (shadowLength - i) / shadowLength;

		if (a < currentAlpha)
		{
			float4 color = color1 + (color2 - color1) * i / shadowLength;
			a = max(a, currentAlpha);
			back = color + current * (1 - color.a);

			if (a == 1)
				break;
		}
	}
	a = min(a, 1);

	float4 front = InputTexture.Sample(InputSampler, uv0.xy);
	return front + float4(back.r, back.g, back.b, back.a) * a * (1 - front.a);
}
