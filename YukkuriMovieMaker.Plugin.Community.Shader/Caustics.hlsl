#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float displacement : packoffset(c0.x);
    float invFeature   : packoffset(c0.y);
    float time         : packoffset(c0.z);
    float strength     : packoffset(c0.w);

    float sigma        : packoffset(c1.x);
    float dispersion   : packoffset(c1.y);
    float focus        : packoffset(c1.z);
    float seed         : packoffset(c1.w);

    float lightR       : packoffset(c2.x);
    float lightG       : packoffset(c2.y);
    float lightB       : packoffset(c2.z);
    int   lightOnly    : packoffset(c2.w);

    float absorbR      : packoffset(c3.x);
    float absorbG      : packoffset(c3.y);
    float absorbB      : packoffset(c3.z);
    float absorption   : packoffset(c3.w);

    float flowX        : packoffset(c4.x);
    float flowY        : packoffset(c4.y);
    float anisoScale   : packoffset(c4.z);
    float anisoAngle   : packoffset(c4.w);

    float boilSpeed    : packoffset(c5.x);
    float pad0         : packoffset(c5.y);
    float pad1         : packoffset(c5.z);
    float pad2         : packoffset(c5.w);
};

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

// 集束度は高さ場の2階微分から求めるため、補間はC2連続のquinticでなければ
// セル境界でHessianが不連続になり格子状の筋が出る。cubicでは不可。
void NoiseDeriv(float3 p, out float2 grad, out float3 hess)
{
    float3 i = floor(p);
    float3 f = frac(p);

    float3 w = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);
    float2 dw = 30.0f * f.xy * f.xy * (f.xy - 1.0f) * (f.xy - 1.0f);
    float2 ddw = 60.0f * f.xy * (2.0f * f.xy - 1.0f) * (f.xy - 1.0f);

    float c00 = lerp(hash13(i + float3(0.0f, 0.0f, 0.0f)), hash13(i + float3(0.0f, 0.0f, 1.0f)), w.z);
    float c10 = lerp(hash13(i + float3(1.0f, 0.0f, 0.0f)), hash13(i + float3(1.0f, 0.0f, 1.0f)), w.z);
    float c01 = lerp(hash13(i + float3(0.0f, 1.0f, 0.0f)), hash13(i + float3(0.0f, 1.0f, 1.0f)), w.z);
    float c11 = lerp(hash13(i + float3(1.0f, 1.0f, 0.0f)), hash13(i + float3(1.0f, 1.0f, 1.0f)), w.z);

    float k1 = c10 - c00;
    float k2 = c01 - c00;
    float k3 = c00 - c10 - c01 + c11;

    grad = 2.0f * float2(dw.x * (k1 + k3 * w.y), dw.y * (k2 + k3 * w.x));
    hess = 2.0f * float3(ddw.x * (k1 + k3 * w.y), ddw.y * (k2 + k3 * w.x), dw.x * dw.y * k3);
}

void FieldDeriv(float2 base, float z, float m00, float m01, float m10, float m11, out float2 grad, out float3 hess)
{
    grad = float2(0.0f, 0.0f);
    hess = float3(0.0f, 0.0f, 0.0f);
    float amp = 0.5f;
    float2 p = float2(m00 * base.x + m01 * base.y, m10 * base.x + m11 * base.y);
    float zz = z;

    const float rc = -0.7373688f;
    const float rs = 0.6754903f;

    [unroll]
    for (int oct = 0; oct < 4; oct++)
    {
        float2 g;
        float3 h;
        NoiseDeriv(float3(p, zz), g, h);

        grad += amp * float2(m00 * g.x + m10 * g.y, m01 * g.x + m11 * g.y);
        hess += amp * float3(
            m00 * m00 * h.x + 2.0f * m00 * m10 * h.z + m10 * m10 * h.y,
            m01 * m01 * h.x + 2.0f * m01 * m11 * h.z + m11 * m11 * h.y,
            m00 * m01 * h.x + (m00 * m11 + m10 * m01) * h.z + m10 * m11 * h.y);

        float2 pn = float2(rc * p.x - rs * p.y, rs * p.x + rc * p.y) * 2.0f;
        float n00 = 2.0f * (rc * m00 - rs * m10);
        float n01 = 2.0f * (rc * m01 - rs * m11);
        float n10 = 2.0f * (rs * m00 + rc * m10);
        float n11 = 2.0f * (rs * m01 + rc * m11);
        m00 = n00; m01 = n01; m10 = n10; m11 = n11;
        p = pn + hash21((float)oct + seed * 0.618f) * 37.0f;
        zz = zz * 2.0f + 17.0f;
        amp *= 0.5f;
    }
}

float CausticBrightness(float kappa, float3 hess)
{
    float detJ = (1.0f + kappa * hess.x) * (1.0f + kappa * hess.y) - kappa * kappa * hess.z * hess.z;
    return exp(-abs(detJ) / max(sigma, 1e-4f));
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (strength <= 0.0f && displacement <= 0.0f && absorption <= 0.0f && lightOnly == 0)
        return source;

    float2 seedOfs = hash21(seed) * 512.0f;
    float2 base = posScene.xy * invFeature + seedOfs - float2(flowX, flowY) * time;
    float z = time * boilSpeed + seed * 3.7f;

    float ca = cos(anisoAngle);
    float sa = sin(anisoAngle);
    float sA = anisoScale;
    float m00 = ca * ca + sA * sa * sa;
    float m11 = sa * sa + sA * ca * ca;
    float m01 = ca * sa * (1.0f - sA);

    float2 grad;
    float3 hess;
    FieldDeriv(base, z, m00, m01, m01, m11, grad, hess);

    grad *= 0.6f;
    hess *= 0.6f;
    float gLen = length(grad);
    if (gLen > 1.0f)
        grad /= gLen;

    float kappa = focus * 3.0f;
    float dR = 1.0f - dispersion;
    float dB = 1.0f + dispersion;

    float bR = CausticBrightness(kappa * dR, hess);
    float bG = CausticBrightness(kappa, hess);
    float bB = CausticBrightness(kappa * dB, hess);
    float3 light = float3(lightR * bR, lightG * bG, lightB * bB) * (strength * 1.5f);

    if (lightOnly != 0)
    {
        float3 clipped = light * source.a;
        float aL = saturate(max(clipped.r, max(clipped.g, clipped.b)));
        return float4(min(clipped, float3(aL, aL, aL)), aL);
    }

    float2 dpx = grad * displacement;
    float4 tapG = SampleInput(uv0.xy + dpx * uv0.zw);
    float4 col = tapG;
    if (dispersion > 0.0f)
    {
        col.r = SampleInput(uv0.xy + dpx * dR * uv0.zw).r;
        col.b = SampleInput(uv0.xy + dpx * dB * uv0.zw).b;
    }

    if (absorption > 0.0f)
    {
        float3 transmittance = pow(max(float3(absorbR, absorbG, absorbB), 0.001f), absorption * 2.0f);
        col.rgb *= transmittance;
    }

    col.rgb += light * col.a;
    col.rgb = min(col.rgb, float3(col.a, col.a, col.a));
    return col;
}
