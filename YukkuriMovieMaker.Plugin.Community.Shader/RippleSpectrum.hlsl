#include "Spectrum.hlsli"

cbuffer Constants : register(b0)
{
    float4 ripple : packoffset(c0);
    float4 style : packoffset(c1);
    float4 response : packoffset(c2);
    float4 tint : packoffset(c3);
    float4 values[64] : packoffset(c4);
};

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    int count = (int)ripple.z;
    if (count < 1)
        return float4(0.0, 0.0, 0.0, 0.0);

    float innerRadius = ripple.x;
    float reach = ripple.y;
    float travelOffset = ripple.w;
    float minThickness = style.x;
    float maxThickness = style.y;
    int window = (int)style.z;
    float decay = style.w;
    float valueFollow = response.x;

    float radial = length(scenePosition.xy);
    float antialias = max(fwidth(radial), 0.5);

    float progress = (radial - innerRadius) / reach;
    int centreBand = (int)floor(frac(progress - travelOffset) * count + 0.5);
    if (centreBand >= count)
        centreBand -= count;

    float coverage = 0.0;

    [loop]
    for (int offset = -window; offset <= window; offset++)
    {
        int index = centreBand + offset;
        if (index < 0)
            index += count;
        if (index >= count)
            index -= count;

        float travel = frac(travelOffset + (float)index / count);
        float value = abs(SpectrumLane(values[index >> 2], index & 3));
        float halfWidth = lerp(minThickness, maxThickness, value) * 0.5;
        float distance = abs(radial - (innerRadius + travel * reach));
        float brightness = lerp(1.0, value, valueFollow) * lerp(1.0, 1.0 - decay, travel);

        coverage = max(coverage, LineCoverage(distance, halfWidth, antialias) * brightness);
    }

    return PremultipliedTint(tint, coverage);
}
