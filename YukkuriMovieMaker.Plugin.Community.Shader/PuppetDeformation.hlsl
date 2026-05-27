Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

Texture2D<float4> PinTexture : register(t1);

cbuffer Constants : register(b0)
{
    float pinCount : packoffset(c0.x);
    float stiffness : packoffset(c0.y);
    float inputLeft : packoffset(c0.z);
    float inputTop : packoffset(c0.w);

    float inputWidth : packoffset(c1.x);
    float inputHeight : packoffset(c1.y);
};

static const int MaxPins = 256;
static const float Epsilon = 1e-6f;

float2 LocalToScene(float2 local)
{
    return local + float2(inputLeft + inputWidth * 0.5f, inputTop + inputHeight * 0.5f);
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
) : SV_TARGET
{
    int n = (int) clamp(pinCount, 0.0f, (float) MaxPins);
    float2 v = posScene.xy;

    if (n <= 0)
        return InputTexture.SampleLevel(InputSampler, uv0.xy, 0);

    float2 source;

    if (n == 1)
    {
        float2 r0 = GetRestScene(0);
        float2 c0 = GetCurrentScene(0);
        source = v - (c0 - r0);
    }
    else
    {
        float alpha = clamp(stiffness, 0.1f, 8.0f);
        float scale = max(inputWidth, inputHeight);
        float scaleInvSq = 1.0f / (scale * scale);

        float totalW = 0.0f;
        float2 pStar = float2(0.0f, 0.0f);
        float2 qStar = float2(0.0f, 0.0f);

        float minDistSq = 1e30f;
        float2 nearestRest = float2(0.0f, 0.0f);

        [loop]
        for (int i = 0; i < n; i++)
        {
            float2 ci = GetCurrentScene(i);
            float2 ri = GetRestScene(i);
            float2 d = ci - v;
            float distSq = dot(d, d) * scaleInvSq + Epsilon;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                nearestRest = ri;
            }
            float w = pow(distSq, -alpha);
            totalW += w;
            pStar += w * ci;
            qStar += w * ri;
        }

        if (minDistSq < Epsilon * 4.0f)
        {
            source = nearestRest;
        }
        else
        {
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

                float dotPV = dot(pHat, vHat);
                float dotPperpV = dot(pHatPerp, vHat);

                frX += w * (dotPV * qHat.x - dotPperpV * qHat.y);
                frY += w * (dotPV * qHat.y + dotPperpV * qHat.x);
            }

            float frLen = length(float2(frX, frY));

            if (frLen < Epsilon)
                source = float2(v.x - pStar.x + qStar.x, v.y - pStar.y + qStar.y);
            else
                source = (vHatLen / frLen) * float2(frX, frY) + qStar;
        }
    }

    float srcRight = inputLeft + inputWidth;
    float srcBottom = inputTop + inputHeight;

    if (source.x < inputLeft || source.x >= srcRight ||
        source.y < inputTop || source.y >= srcBottom)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    float2 uv = uv0.xy + (source - v) * uv0.zw;

    return InputTexture.SampleLevel(InputSampler, uv, 0);
}
