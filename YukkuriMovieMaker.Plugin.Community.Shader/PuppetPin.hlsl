Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

Texture2D<float4> PinTexture : register(t1);

cbuffer constants : register(b0)
{
    float PinCount : packoffset(c0.x);
    float Stiffness : packoffset(c0.y);
    float InputLeft : packoffset(c0.z);
    float InputTop : packoffset(c0.w);
    float InputWidth : packoffset(c1.x);
    float InputHeight : packoffset(c1.y);
};

static const int MaxPins = 256;
static const float Epsilon = 1e-6f;

float2 LocalToScene(float2 local)
{
    return local + float2(InputLeft + InputWidth * 0.5f, InputTop + InputHeight * 0.5f);
}

float2 GetRestScene(int index)
{
    return LocalToScene(PinTexture.Load(int3(index, 0, 0)).xy);
}

float2 GetCurrentScene(int index)
{
    return LocalToScene(PinTexture.Load(int3(index, 0, 0)).zw);
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    int n = (int) clamp(PinCount, 0.0f, (float) MaxPins);
    float2 v = posScene.xy;

    if (n <= 0)
    {
        return InputTexture.SampleLevel(InputSampler, uv0.xy, 0);
    }

    if (n == 1)
    {
        float2 r0 = GetRestScene(0);
        float2 c0 = GetCurrentScene(0);
        float2 source = v - (c0 - r0);
        float2 sampleUV = uv0.xy + (source - v) * uv0.zw;
        if (sampleUV.x < 0.0f || sampleUV.x > 1.0f || sampleUV.y < 0.0f || sampleUV.y > 1.0f)
            return float4(0.0f, 0.0f, 0.0f, 0.0f);
        return InputTexture.SampleLevel(InputSampler, sampleUV, 0);
    }

    float alpha = clamp(Stiffness, 0.1f, 8.0f);
    float scale = max(InputWidth, InputHeight);
    float scaleInvSq = 1.0f / (scale * scale);

    float totalW = 0.0f;
    float2 pStar = float2(0.0f, 0.0f);
    float2 qStar = float2(0.0f, 0.0f);

    float minDistSq = 1e30f;
    int nearestIndex = 0;

    [loop]
    for (int i = 0; i < n; i++)
    {
        float2 ci = GetCurrentScene(i);
        float2 d = ci - v;
        float distSq = dot(d, d) * scaleInvSq + Epsilon;
        if (distSq < minDistSq)
        {
            minDistSq = distSq;
            nearestIndex = i;
        }
        float w = pow(distSq, -alpha);
        totalW += w;
        pStar += w * ci;
        qStar += w * GetRestScene(i);
    }

    if (minDistSq < Epsilon * 4.0f)
    {
        float2 ri = GetRestScene(nearestIndex);
        float2 sampleUV = uv0.xy + (ri - v) * uv0.zw;
        if (sampleUV.x < 0.0f || sampleUV.x > 1.0f || sampleUV.y < 0.0f || sampleUV.y > 1.0f)
            return float4(0.0f, 0.0f, 0.0f, 0.0f);
        return InputTexture.SampleLevel(InputSampler, sampleUV, 0);
    }

    float invTotalW = 1.0f / totalW;
    pStar *= invTotalW;
    qStar *= invTotalW;

    float2 vHat = v - pStar;
    float vHatLen = length(vHat);

    float frX = 0.0f;
    float frY = 0.0f;

    [loop]
    for (int j = 0; j < n; j++)
    {
        float2 ci = GetCurrentScene(j);
        float2 ri = GetRestScene(j);
        float2 d = ci - v;
        float distSq = dot(d, d) * scaleInvSq + Epsilon;
        float w = pow(distSq, -alpha);

        float2 pHat = ci - pStar;
        float2 qHat = ri - qStar;

        float2 pHatPerp = float2(-pHat.y, pHat.x);

        frX += w * (dot(pHat, vHat) * qHat.x - dot(pHatPerp, vHat) * qHat.y);
        frY += w * (dot(pHat, vHat) * qHat.y + dot(pHatPerp, vHat) * qHat.x);
    }

    float frLen = length(float2(frX, frY));

    float2 source;
    if (frLen < Epsilon)
    {
        source = v - pStar + qStar;
    }
    else
    {
        source = (vHatLen / frLen) * float2(frX, frY) + qStar;
    }

    float2 sampleUV = uv0.xy + (source - v) * uv0.zw;
    if (sampleUV.x < 0.0f || sampleUV.x > 1.0f || sampleUV.y < 0.0f || sampleUV.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    return InputTexture.SampleLevel(InputSampler, sampleUV, 0);
}
