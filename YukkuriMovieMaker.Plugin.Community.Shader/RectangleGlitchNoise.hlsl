#include "Hash.hlsli"

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    int seed : packoffset(c0.x);
    int rectangles : packoffset(c0.y);
    float inputLeft : packoffset(c0.z);
    float inputTop : packoffset(c0.w);
    
    float inputWidth : packoffset(c1.x);
    float inputHeight : packoffset(c1.y);
    float maxRectangleWidth : packoffset(c1.z);
    float maxRectangleHeight : packoffset(c1.w);
    
    float maxRectangleXShift : packoffset(c2.x);
    float maxRectangleYShift : packoffset(c2.y);
    float maxColorShift : packoffset(c2.z);
    int repeatCount : packoffset(c2.w);
    
    float rectangleWidthAttenuationRate : packoffset(c3.x);
    float rectangleHeightAttenuationRate : packoffset(c3.y);
    float rectangleXShiftAttenuationRate : packoffset(c3.z);
    float rectangleYShiftAttenuationRate : packoffset(c3.w);
    
    bool isClipping : packoffset(c4.x);
};

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float2 p = posScene.xy;
    
    float2 offset = float2(0, 0);
    bool doneClipping = false;
    bool doneOverlap = false;
    float2 inputPos = float2(inputLeft, inputTop);
    float2 inputSize = float2(inputWidth, inputHeight);
    float2 maxRectangleSize = float2(maxRectangleWidth, maxRectangleHeight);
    float2 maxRectangleShift = float2(maxRectangleXShift, maxRectangleYShift);
    float2 rectangleSizeAttenuationRate = float2(rectangleWidthAttenuationRate, rectangleHeightAttenuationRate);
    float2 rectangleShiftAttenuationRate = float2(rectangleXShiftAttenuationRate, rectangleYShiftAttenuationRate);
    [loop]
    for (int repeat = 0; repeat < repeatCount; repeat++)
    {
        float2 amp = pow(abs(rectangleShiftAttenuationRate), float(repeat));
        float2 rectangleSizeAttenuation = pow(abs(rectangleSizeAttenuationRate), repeat);
        [loop]
        for (int i = 0; i < rectangles; i++)
        {
            int rectangleSeed = seed + (repeat + 1) * rectangles + i * 100;
            float2 rectangleSize = min(maxRectangleSize * hash21(rectangleSeed) * rectangleSizeAttenuation, inputSize);
            float2 rectanglePos = inputPos + (inputSize - rectangleSize) * hash21(rectangleSeed + 100);
            float2 rectangleShift = maxRectangleShift * (hash21(rectangleSeed + 200) - 0.5) * 2 * amp;

            if (rectanglePos.x - rectangleShift.x < p.x && p.x < rectanglePos.x + rectangleSize.x - rectangleShift.x
                && rectanglePos.y - rectangleShift.y < p.y && p.y < rectanglePos.y + rectangleSize.y - rectangleShift.y)
            {
                doneOverlap = true;
                offset = rectangleShift;
            }
            
            if (rectanglePos.x < p.x && p.x < rectanglePos.x + rectangleSize.x && rectanglePos.y < p.y && p.y < rectanglePos.y + rectangleSize.y)
            {
                doneClipping = true;
            }
        }
    }
    if (isClipping && !doneOverlap && doneClipping)
        return float4(0, 0, 0, 0);
    
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