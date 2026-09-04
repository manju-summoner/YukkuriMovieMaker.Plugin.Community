#include "Spectrum.hlsli"

cbuffer Constants : register(b0)
{
    float4 stringShape : packoffset(c0);
    float4 tint : packoffset(c1);
    float4 modeAmplitude[16] : packoffset(c2);
};

static const float Pi = 3.14159265358979;

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    int modes = (int)stringShape.z;
    if (modes < 1)
        return float4(0.0, 0.0, 0.0, 0.0);

    float width = stringShape.x;
    float amplitude = stringShape.y;
    float halfThickness = stringShape.w * 0.5;
    float halfWidth = width * 0.5;

    float2 local = scenePosition.xy;
    float antialias = max(max(fwidth(local.x), fwidth(local.y)), 0.5);
    float sideMask = saturate((halfWidth - abs(local.x)) / antialias + 0.5);
    if (sideMask <= 0.0)
        return float4(0.0, 0.0, 0.0, 0.0);

    float baseSin;
    float baseCos;
    sincos(Pi * (local.x + halfWidth) / width, baseSin, baseCos);

    float sinValue = 0.0;
    float cosValue = 1.0;
    float displacement = 0.0;
    float slope = 0.0;
    float modeNumber = 1.0;
    int blocks = (modes + 3) >> 2;

    [loop]
    for (int block = 0; block < blocks; block++)
    {
        float4 packed = modeAmplitude[block];

        [unroll]
        for (int lane = 0; lane < 4; lane++)
        {
            float nextSin = sinValue * baseCos + cosValue * baseSin;
            float nextCos = cosValue * baseCos - sinValue * baseSin;
            sinValue = nextSin;
            cosValue = nextCos;

            float weight = packed[lane];
            displacement += weight * sinValue;
            slope += weight * modeNumber * cosValue;
            modeNumber += 1.0;
        }
    }

    displacement *= amplitude;
    slope *= amplitude * Pi / width;

    float distance = abs(local.y + displacement) * rsqrt(1.0 + slope * slope);
    return PremultipliedTint(tint, LineCoverage(distance, halfThickness, antialias) * sideMask);
}
