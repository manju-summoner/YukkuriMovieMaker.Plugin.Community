Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float radius : packoffset(c0.x);
	float brightness : packoffset(c0.y);
	float edgeStrength : packoffset(c0.z);
	float limit : packoffset(c0.w);
};

static float pi = 3.141592;
static float goldenAngle = 2.399963;

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	if(radius == 0)
		return InputTexture.Sample(InputSampler, uv0.xy);

	float4 result = float4(0, 0, 0, 0);
	float gamma = 1 + brightness;
	float limitedRadius = min(limit, radius);
	float samples = limitedRadius * limitedRadius * pi;
	float radiusScaling = radius / sqrt(samples);
	float4 totalGain = 0;
	for (float i = 0; i < samples; i++)
	{
		float r = radiusScaling * sqrt(i);
		float t = goldenAngle * i;
		float2 delta = float2(r * cos(t), r * sin(t));

		float2 uv = uv0.xy + delta * uv0.zw;
		float gain = edgeStrength == 0 || samples <= 1 ? 1 : pow(abs(r), edgeStrength);
		float4 color = InputTexture.Sample(InputSampler, uv);
		result += pow(abs(color), gamma) * gain;
		totalGain += gain;
	}
	return pow(abs(result / totalGain), 1 / gamma);
}