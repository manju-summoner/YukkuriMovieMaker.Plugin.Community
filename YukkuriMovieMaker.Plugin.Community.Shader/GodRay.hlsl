Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float lightX        : packoffset(c0.x);
	float lightY        : packoffset(c0.y);
	float intensity     : packoffset(c0.z);
	float decay         : packoffset(c0.w);

	float density       : packoffset(c1.x);
	float weight        : packoffset(c1.y);
	float samples       : packoffset(c1.z);
	float threshold     : packoffset(c1.w);

	float colorR        : packoffset(c2.x);
	float colorG        : packoffset(c2.y);
	float colorB        : packoffset(c2.z);
	float colorA        : packoffset(c2.w);

	float inputLeft     : packoffset(c3.x);
	float inputTop      : packoffset(c3.y);
	float inputRight    : packoffset(c3.z);
	float inputBottom   : packoffset(c3.w);
};

float4 SampleInput(Texture2D t, SamplerState s, float2 uv, float2 uvBoundsMin, float2 uvBoundsMax) {
	if (uv.x < uvBoundsMin.x || uv.x > uvBoundsMax.x || uv.y < uvBoundsMin.y || uv.y > uvBoundsMax.y)
		return float4(0.0f, 0.0f, 0.0f, 0.0f);
	return t.SampleLevel(s, uv, 0);
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_Target
{
	float2 texCoord = uv0.xy;
	
	float2 uvBoundsMin = uv0.xy + (float2(inputLeft, inputTop) - posScene.xy) * uv0.zw;
	float2 uvBoundsMax = uv0.xy + (float2(inputRight, inputBottom) - posScene.xy) * uv0.zw;
	
	float2 lightScene = float2(
		inputLeft + lightX * (inputRight - inputLeft),
		inputTop  + lightY * (inputBottom - inputTop)
	);
	float2 lightUV = uv0.xy + (lightScene - posScene.xy) * uv0.zw;

	float2 dir = (texCoord - lightUV) * density;
	int numSamples = (int)clamp(samples, 1.0f, 256.0f);
	float2 delta = dir / (float)numSamples;

	float2 currentUV = texCoord;
	float illuminationDecay = 1.0f;
	float4 accumulated = float4(0.0f, 0.0f, 0.0f, 0.0f);

	[loop]
	for (int i = 0; i < numSamples; i++)
	{
		currentUV -= delta;
		float4 s = SampleInput(InputTexture, InputSampler, currentUV, uvBoundsMin, uvBoundsMax);
		float lum = dot(s.rgb, float3(0.299f, 0.587f, 0.114f));
		float mask = (lum >= threshold) ? s.a : 0.0f;
		accumulated += s * mask * illuminationDecay * weight;
		illuminationDecay *= decay;
	}

	float4 original = SampleInput(InputTexture, InputSampler, texCoord, uvBoundsMin, uvBoundsMax);
	float4 tintColor = float4(colorR, colorG, colorB, colorA);
	float4 rays = accumulated * tintColor * intensity;

	float4 result;
	result.rgb = saturate(original.rgb + rays.rgb);
	result.a = saturate(original.a + rays.a);
	return result;
}
