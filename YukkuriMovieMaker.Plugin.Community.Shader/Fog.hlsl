#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float density     : packoffset(c0.x);
    float invFeature  : packoffset(c0.y);
    float time        : packoffset(c0.z);
    float unevenness  : packoffset(c0.w);

    float flowX       : packoffset(c1.x);
    float flowY       : packoffset(c1.y);
    float boilSpeed   : packoffset(c1.z);
    float gradient    : packoffset(c1.w);

    float fogR        : packoffset(c2.x);
    float fogG        : packoffset(c2.y);
    float fogB        : packoffset(c2.z);
    float seed        : packoffset(c2.w);

    float sunIntensity : packoffset(c3.x);
    float sunAngle     : packoffset(c3.y);
    float inputTop     : packoffset(c3.z);
    float inputHeight  : packoffset(c3.w);

    float sunR        : packoffset(c4.x);
    float sunG        : packoffset(c4.y);
    float sunB        : packoffset(c4.z);
    float pad0        : packoffset(c4.w);
};

#define SUN_STEPS 6

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 w = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);

    float c00 = lerp(hash13(i + float3(0.0f, 0.0f, 0.0f)), hash13(i + float3(0.0f, 0.0f, 1.0f)), w.z);
    float c10 = lerp(hash13(i + float3(1.0f, 0.0f, 0.0f)), hash13(i + float3(1.0f, 0.0f, 1.0f)), w.z);
    float c01 = lerp(hash13(i + float3(0.0f, 1.0f, 0.0f)), hash13(i + float3(0.0f, 1.0f, 1.0f)), w.z);
    float c11 = lerp(hash13(i + float3(1.0f, 1.0f, 0.0f)), hash13(i + float3(1.0f, 1.0f, 1.0f)), w.z);

    float x0 = lerp(c00, c10, w.x);
    float x1 = lerp(c01, c11, w.x);
    return lerp(x0, x1, w.y);
}

float Fbm(float2 p, float z, int octaves)
{
    const float rc = -0.7373688f;
    const float rs = 0.6754903f;

    float value = 0.0f;
    float amp = 0.5f;
    float zz = z;

    [loop]
    for (int i = 0; i < octaves; i++)
    {
        value += amp * Noise3(float3(p, zz));
        p = float2(rc * p.x - rs * p.y, rs * p.x + rc * p.y) * 2.0f + 37.0f;
        zz = zz * 2.0f + 17.0f;
        amp *= 0.5f;
    }
    return value;
}

float DensityAt(float2 scenePos)
{
    float2 seedOfs = hash21(seed) * 512.0f;
    float2 p = scenePos * invFeature + seedOfs - float2(flowX, flowY) * time;
    float z = time * boilSpeed + seed * 3.7f;

    float wx = Fbm(p * 0.5f + float2(13.1f, 71.7f), z * 0.7f, 3);
    float wy = Fbm(p * 0.5f + float2(59.3f, 27.9f), z * 0.7f + 5.0f, 3);
    float2 warped = p + (float2(wx, wy) * 2.0f - 1.0f) * 1.6f;

    float m = Fbm(warped, z, 5);
    m = saturate(m * 1.6f - 0.25f);
    m = pow(m, 1.0f + 3.0f * unevenness);

    float yn = saturate((scenePos.y - inputTop) / max(inputHeight, 1.0f));
    float hf = saturate(1.0f + gradient * (yn * 2.0f - 1.0f));

    return m * hf;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (density <= 0.0f || source.a <= 0.0f)
        return source;

    float d = DensityAt(posScene.xy);
    float opticalDepth = d * density * 3.0f;
    float transmittance = exp(-opticalDepth);

    float3 airlight = float3(fogR, fogG, fogB);

    if (sunIntensity > 0.0f)
    {
        float2 sunDir;
        sincos(sunAngle, sunDir.y, sunDir.x);
        float stepPx = 0.35f / max(invFeature, 1e-5f);

        float tau = 0.0f;
        [unroll]
        for (int i = 1; i <= SUN_STEPS; i++)
        {
            tau += DensityAt(posScene.xy + sunDir * (stepPx * (float)i));
        }
        tau *= density * 3.0f * 0.35f;

        float sunTransmittance = exp(-tau);
        airlight += float3(sunR, sunG, sunB) * (sunIntensity * sunTransmittance);
    }

    airlight = saturate(airlight);

    float4 result;
    result.a = source.a;
    result.rgb = source.rgb * transmittance + airlight * (1.0f - transmittance) * source.a;
    result.rgb = min(result.rgb, float3(result.a, result.a, result.a));
    return result;
}
