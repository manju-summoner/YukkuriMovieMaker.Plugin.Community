Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float distance : packoffset(c0.x);
	float angle : packoffset(c0.y);
	float strength : packoffset(c0.z);
	int mode : packoffset(c0.w);
};

float4 GetColor(float2 uv) {

	if (uv.x < 0 || uv.y < 0 || 1 < uv.x || 1 < uv.y)
		return 0;
	return InputTexture.Sample(InputSampler, uv);
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	const float Pi = 3.141592653589f;
	float x = 0;
	float y = distance;

	float2 delta = float2(x * cos(Pi * angle / 180.0) - y * sin(Pi * angle / 180.0), x * sin(Pi * angle / 180.0) + y * cos(Pi * angle / 180.0));

	float4 color0 = GetColor(uv0.xy);
	float4 color1 = GetColor(uv0.xy - delta * uv0.zw);
	float4 color2 = GetColor(uv0.xy + delta * uv0.zw);

	if (abs(mode) == 1)
	{
		color0.r *= 1 - strength;
		color0.g *= 1 - strength;
		color0.b *= 1;
		color0.a *= 1 - strength * 2 / 3;

		color1.r *= 0;
		color1.g *= strength;
		color1.b *= 0;
		color1.a *= strength / 3;

		color2.r *= strength;
		color2.g *= 0;
		color2.b *= 0;
		color2.a *= strength / 3;
	}
	else if (abs(mode) == 2) {

		color0.r *= 1 - strength;
		color0.g *= 1;
		color0.b *= 1 - strength;
		color0.a *= 1 - strength * 2 / 3;

		color1.r = 0;
		color1.g = 0;
		color1.b *= strength;
		color1.a *= strength / 3;

		color2.r *= strength;
		color2.g = 0;
		color2.b = 0;
		color2.a *= strength / 3;
	}
	else {
		color0.r *= 1;
		color0.g *= 1 - strength;
		color0.b *= 1 - strength;
		color0.a *= 1 - strength * 2 / 3;

		color1.r = 0;
		color1.g = 0;
		color1.b *= strength;
		color1.a *= strength / 3;

		color2.r = 0;
		color2.g *= strength;
		color2.b = 0;
		color2.a *= strength / 3;
	}

	float4 result = color0 + color1 + color2;
	if (mode < 0)
	{
		result.r *= result.a;
		result.g *= result.a;
		result.b *= result.a;
	}
	return result;
}