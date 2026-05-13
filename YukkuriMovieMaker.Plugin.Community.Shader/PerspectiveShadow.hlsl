Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
	float lightX         : packoffset(c0.x);
	float lightY         : packoffset(c0.y);
	float lightHeight    : packoffset(c0.z);
	float groundY        : packoffset(c0.w);

	float opacity        : packoffset(c1.x);
	float falloff        : packoffset(c1.y);
	float blurRadius     : packoffset(c1.z);
	float spread         : packoffset(c1.w);

	float4 shadowColor   : packoffset(c2);

	float alphaThreshold : packoffset(c3.x);
	float pad0           : packoffset(c3.y);
	float pad1           : packoffset(c3.z);
	float pad2           : packoffset(c3.w);
};

static const float EPSILON = 1e-4f;
static const float BLUR_THRESHOLD = 0.5f;
static const float GOLDEN_ANGLE = 2.39996323f;
static const float GAUSS_FALLOFF = 2.0f;
static const float MIN_WEIGHT_SUM = 1e-3f;
static const float DISTANCE_ATTENUATION = 0.005f;
static const float MAX_SPREAD_FACTOR = 3.0f;
static const int MIN_BLUR_SAMPLES = 16;
static const int MAX_BLUR_SAMPLES = 128;

float2 UnprojectFromShadow(float2 S, out bool valid)
{
	valid = false;
	float denom = groundY - lightY;
	if (abs(denom) < EPSILON)
		return float2(0.0f, 0.0f);

	float m = (S.y - lightY) / denom;
	if (m < 1.0f)
		return float2(0.0f, 0.0f);

	float invM = 1.0f / m;
	float Px = lightX + (S.x - lightX) * invM;
	float Py = groundY - lightHeight * (1.0f - invM);

	if (Py > groundY)
		return float2(0.0f, 0.0f);

	valid = true;
	return float2(Px, Py);
}

float4 SampleInput(float2 uv)
{
	if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
		return float4(0.0f, 0.0f, 0.0f, 0.0f);
	return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float SmoothAlphaThreshold(float alpha)
{
	if (alphaThreshold < EPSILON)
		return alpha;
	float edge0 = alphaThreshold * 0.5f;
	float edge1 = alphaThreshold * 1.5f;
	return alpha * smoothstep(edge0, edge1, alpha);
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0      : TEXCOORD0
) : SV_TARGET
{
	float2 P = posScene.xy;
	float2 baseUV = uv0.xy;
	float2 pxToUV = uv0.zw;

	float4 original = SampleInput(baseUV);

	bool valid;
	float2 Q = UnprojectFromShadow(P, valid);

	float4 shadow = float4(0.0f, 0.0f, 0.0f, 0.0f);

	if (valid)
	{
		float shadowDist = length(P - Q);

		float2 Quv = baseUV + (Q - P) * pxToUV;
		float dynamicBlur = blurRadius * (1.0f + min(shadowDist / max(lightHeight, 1.0f) * spread, MAX_SPREAD_FACTOR));

		float alpha = 0.0f;
		if (dynamicBlur <= BLUR_THRESHOLD)
		{
			float4 silhouette = SampleInput(Quv);
			alpha = SmoothAlphaThreshold(silhouette.a);
		}
		else
		{
			int numSamples = clamp((int)(dynamicBlur * 2.0f), MIN_BLUR_SAMPLES, MAX_BLUR_SAMPLES);
			float invSamples = 1.0f / (float)numSamples;
			float acc = 0.0f;
			float weightSum = 0.0f;
			[loop]
			for (int i = 0; i < numSamples; i++)
			{
				float fi = (float)i;
				float r = sqrt((fi + 0.5f) * invSamples);
				float theta = fi * GOLDEN_ANGLE;
				float2 off = float2(cos(theta), sin(theta)) * r * dynamicBlur;
				float2 sUV = Quv + off * pxToUV;
				float4 s = SampleInput(sUV);
				float gauss = exp(-GAUSS_FALLOFF * r * r);
				float contrib = SmoothAlphaThreshold(s.a);
				acc += contrib * gauss;
				weightSum += gauss;
			}
			alpha = acc / max(weightSum, MIN_WEIGHT_SUM);
		}

		float decay = lerp(1.0f, 1.0f / (1.0f + shadowDist * DISTANCE_ATTENUATION), saturate(falloff));

		float finalAlpha = alpha * opacity * decay * shadowColor.a;
		shadow = float4(shadowColor.rgb * finalAlpha, finalAlpha);
	}

	float invA = 1.0f - original.a;
	float4 result;
	result.rgb = saturate(original.rgb + shadow.rgb * invA);
	result.a = saturate(original.a + shadow.a * invA);
	return result;
}
