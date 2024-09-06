#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    int seed : packoffset(c0.x);
    int lines : packoffset(c0.y);
    float inputTop : packoffset(c0.z);
    float inputHeight : packoffset(c0.w);

    float maxStripeWidth : packoffset(c1.x);
    float maxStripeXShift : packoffset(c1.y);
    float maxColorShift : packoffset(c1.z);
    int repeatCount : packoffset(c1.w);

    float stripeWidthAttenuationRate : packoffset(c2.x);
    float stripeXShiftAttenuationRate : packoffset(c2.y);

};

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float2 p = posScene.xy;

    float2 offset = float2(0, 0);
    float totalAmp = 0;
    [loop]
    for (int repeat = 0; repeat <= repeatCount; repeat++)
    {
        float2 currentOffset = float2(0, 0);
        float amp = pow(abs(stripeXShiftAttenuationRate), float(repeat));
        float stripeWidthAttenuation = pow(abs(stripeWidthAttenuationRate), repeat);
        [loop]
        for (int i = 0; i < lines; i++)
        {
            int stripeSeed = seed + (repeat + 1) * lines + i * 100;
            float stripeWidth = maxStripeWidth * hash11(stripeSeed) * stripeWidthAttenuation;
            float stripeY = inputTop + inputHeight * hash11(stripeSeed + 100);

            if (stripeY < p.y && p.y < stripeY + stripeWidth)
            {
                currentOffset.x = maxStripeXShift * (hash11(stripeSeed + 200) - 0.5) * 2 * amp;
            }
        }
        offset += currentOffset;
        totalAmp += amp;
    }
    if (totalAmp > 0)
        offset /= totalAmp;

    float4 result = InputTexture.Sample(InputSampler, uv0.xy + offset * uv0.zw);
    float colorShiftRate = hash11(seed);
    offset += maxColorShift * (hash21(seed + 300) - 0.5) * 2;
    if (colorShiftRate < 0.33)
    {
        result.r = InputTexture.Sample(InputSampler, uv0.xy + offset * uv0.zw).r;
    }
    else if (colorShiftRate < 0.66)
    {
        result.g = InputTexture.Sample(InputSampler, uv0.xy + offset * uv0.zw).g;
    }
    else
    {
        result.b = InputTexture.Sample(InputSampler, uv0.xy + offset * uv0.zw).b;
    }

    return result;
}