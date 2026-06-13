Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float threshold : packoffset(c0.x);
    float softness : packoffset(c0.y);
    float thickness : packoffset(c0.z);
    float intensity : packoffset(c0.w);

    float colorR : packoffset(c1.x);
    float colorG : packoffset(c1.y);
    float colorB : packoffset(c1.z);
    float colorA : packoffset(c1.w);

    int useSourceColor : packoffset(c2.x);
    int includeAlpha : packoffset(c2.y);
    int ignoreImageBorder : packoffset(c2.z);
    float inputLeft : packoffset(c2.w);

    float inputTop : packoffset(c3.x);
    float inputWidth : packoffset(c3.y);
    float inputHeight : packoffset(c3.z);
};

float3 SrgbToLinear(float3 c)
{
    float3 lo = c / 12.92f;
    float3 hi = pow(max((c + 0.055f) / 1.055f, 0.0f), 2.4f);
    return lerp(lo, hi, step(0.04045f, c));
}

float3 LinearToOklab(float3 rgb)
{
    float l = 0.4122214708f * rgb.r + 0.5363325363f * rgb.g + 0.0514459929f * rgb.b;
    float m = 0.2119034982f * rgb.r + 0.6806995451f * rgb.g + 0.1073969566f * rgb.b;
    float s = 0.0883024619f * rgb.r + 0.2817188376f * rgb.g + 0.6299787005f * rgb.b;

    float l_ = pow(max(l, 0.0f), 1.0f / 3.0f);
    float m_ = pow(max(m, 0.0f), 1.0f / 3.0f);
    float s_ = pow(max(s, 0.0f), 1.0f / 3.0f);

    return float3(
		0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
		1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
		0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_);
}

float4 SampleInput(float2 uv, float2 globalUV, out float inside)
{
    inside = (globalUV.x >= 0.0f && globalUV.x <= 1.0f && globalUV.y >= 0.0f && globalUV.y <= 1.0f) ? 1.0f : 0.0f;
    if (inside == 0.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 SampleFeature(float2 uv, float2 globalUV, int alphaFlag, out float inside)
{
    float4 raw = SampleInput(uv, globalUV, inside);
    float a = saturate(raw.a);
    float3 straight = a > 1e-5f ? raw.rgb / a : float3(0.0f, 0.0f, 0.0f);
    float3 linearRgb = SrgbToLinear(saturate(straight));
    float3 lab = LinearToOklab(linearRgb);
    return float4(lab, alphaFlag != 0 ? a : 0.0f);
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float2 centerGlobalUV = float2(
        (posScene.x - inputLeft) / inputWidth,
        (posScene.y - inputTop) / inputHeight);

    float2 globalUVStep = float2(thickness / inputWidth, thickness / inputHeight);
    float2 uvStep = thickness * uv0.zw;

    float in00, in10, in20, in01, in21, in02, in12, in22;

    float4 f00 = SampleFeature(uv0.xy + float2(-uvStep.x, -uvStep.y), centerGlobalUV + float2(-globalUVStep.x, -globalUVStep.y), includeAlpha, in00);
    float4 f10 = SampleFeature(uv0.xy + float2(0.0f, -uvStep.y), centerGlobalUV + float2(0.0f, -globalUVStep.y), includeAlpha, in10);
    float4 f20 = SampleFeature(uv0.xy + float2(uvStep.x, -uvStep.y), centerGlobalUV + float2(globalUVStep.x, -globalUVStep.y), includeAlpha, in20);
    float4 f01 = SampleFeature(uv0.xy + float2(-uvStep.x, 0.0f), centerGlobalUV + float2(-globalUVStep.x, 0.0f), includeAlpha, in01);
    float4 f21 = SampleFeature(uv0.xy + float2(uvStep.x, 0.0f), centerGlobalUV + float2(globalUVStep.x, 0.0f), includeAlpha, in21);
    float4 f02 = SampleFeature(uv0.xy + float2(-uvStep.x, uvStep.y), centerGlobalUV + float2(-globalUVStep.x, globalUVStep.y), includeAlpha, in02);
    float4 f12 = SampleFeature(uv0.xy + float2(0.0f, uvStep.y), centerGlobalUV + float2(0.0f, globalUVStep.y), includeAlpha, in12);
    float4 f22 = SampleFeature(uv0.xy + float2(uvStep.x, uvStep.y), centerGlobalUV + float2(globalUVStep.x, globalUVStep.y), includeAlpha, in22);

    const float kA = 3.0f / 16.0f;
    const float kB = 10.0f / 16.0f;

    float4 gx = (f20 * kA + f21 * kB + f22 * kA) - (f00 * kA + f01 * kB + f02 * kA);
    float4 gy = (f02 * kA + f12 * kB + f22 * kA) - (f00 * kA + f10 * kB + f20 * kA);

    float weightL = 1.0f;
    float weightAB = 0.5f;
    float weightAlpha = includeAlpha != 0 ? 1.0f : 0.0f;
    float4 w = float4(weightL, weightAB, weightAB, weightAlpha);

    float4 gxW = gx * w;
    float4 gyW = gy * w;

    float Jxx = dot(gxW, gxW);
    float Jyy = dot(gyW, gyW);
    float Jxy = dot(gxW, gyW);

    float trace = Jxx + Jyy;
    float det = Jxx * Jyy - Jxy * Jxy;
    float discriminant = max(trace * trace * 0.25f - det, 0.0f);
    float lambdaMax = trace * 0.5f + sqrt(discriminant);
    float edge = saturate(sqrt(max(lambdaMax, 0.0f)));

    float lower = threshold;
    float upper = min(threshold + max(softness, 1e-5f), 1.0f);
    float mask = smoothstep(lower, upper, edge);

    float centerInside;
    float4 center = SampleInput(uv0.xy, centerGlobalUV, centerInside);

    float kernelInside = in00 * in10 * in20 * in01 * centerInside * in21 * in02 * in12 * in22;
    float borderGate = ignoreImageBorder != 0 ? kernelInside : 1.0f;
    mask *= borderGate;

    float centerAlpha = saturate(center.a);
    float3 centerStraight = centerAlpha > 1e-5f ? center.rgb / centerAlpha : float3(0.0f, 0.0f, 0.0f);

    float3 baseColor = useSourceColor != 0 ? centerStraight : float3(colorR, colorG, colorB);
    float baseAlpha = useSourceColor != 0 ? centerAlpha : colorA;

    float strength = mask * intensity;
    float outAlpha = saturate(strength * baseAlpha);
    float3 outRgb = baseColor * outAlpha;

    return float4(outRgb, outAlpha);
}
