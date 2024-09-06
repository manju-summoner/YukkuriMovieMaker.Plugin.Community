Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float angle : packoffset(c0.x);
	float length : packoffset(c0.y);
	float opacity : packoffset(c0.z);
	float attenuation : packoffset(c0.w);

	float4 color1 : packoffset(c1);
	float4 color2 : packoffset(c2);
};

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float a = 0;
	float4 back = 0;
	[loop]
	for (int i = 0; i <= length; i++)
	{
		float x = -sin(angle) * -i;
		float y = cos(angle) * -i;
		float2 delta = float2(x, y);

		float4 current = InputTexture.Sample(InputSampler, uv0.xy - delta * uv0.zw);
		float currentAlpha = current.a;
		currentAlpha *= opacity;
		currentAlpha *= (1 - attenuation) + attenuation * (length - i) / length;

		if (a < currentAlpha)
		{
			float4 color = color1 + (color2 - color1) * i / length;
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
