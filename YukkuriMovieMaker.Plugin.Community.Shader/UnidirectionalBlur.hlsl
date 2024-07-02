Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float angle : packoffset(c0.x);
	int length : packoffset(c0.y);
};

float Gaussian(float x, float sigma)
{
	return exp(-0.5 * (x * x) / (sigma * sigma)) / (sigma * sqrt(2 * 3.14159));
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float2 e = float2(1.0, 0);
    float2 v = float2(cos(angle), sin(angle));

	float sigma = length / 2.0;
	float4 result;
	float totalWeight;
	[loop]
	for (int i = 0; i < length; i++)
	{
		float4 color = InputTexture.Sample(InputSampler, uv0.xy - v * i * uv0.zw);
		float weight = Gaussian(i, sigma);
		result += color * weight;
		totalWeight += weight;
	}
	result /= totalWeight;

	return result;
}