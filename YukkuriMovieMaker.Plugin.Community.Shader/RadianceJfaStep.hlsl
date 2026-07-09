Texture2D SeedTexture : register(t0);
SamplerState SeedSampler : register(s0);

cbuffer Constants : register(b0)
{
    float stepPx  : packoffset(c0.x);
    float originL : packoffset(c0.y);
    float originT : packoffset(c0.z);
    float pad0    : packoffset(c0.w);
};

bool DecodeIndex(float4 f, out float2 index)
{
    float hiX = round(f.r * 255.0f);
    float loX = round(f.g * 255.0f);
    float hiY = round(f.b * 255.0f);
    float loY = round(f.a * 255.0f);
    index = float2(hiX * 256.0f + loX, hiY * 256.0f + loY);
    return !(hiX >= 254.5f && hiY >= 254.5f);
}

float4 EncodeIndex(float2 index)
{
    float hiX = floor(index.x / 256.0f);
    float loX = index.x - hiX * 256.0f;
    float hiY = floor(index.y / 256.0f);
    float loY = index.y - hiY * 256.0f;
    return float4(hiX, loX, hiY, loY) / 255.0f;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float2 origin = float2(originL, originT);
    float2 own = posScene.xy;

    float bestDist = 1e18f;
    float2 bestIndex = float2(0.0f, 0.0f);
    bool found = false;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 uv = uv0.xy + float2((float)i, (float)j) * stepPx * uv0.zw;
            if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
                continue;

            float4 f = SeedTexture.SampleLevel(SeedSampler, uv, 0);
            float2 index;
            if (!DecodeIndex(f, index))
                continue;

            float2 seedWorld = origin + index + 0.5f;
            float2 diff = seedWorld - own;
            float d = dot(diff, diff);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = index;
                found = true;
            }
        }
    }

    if (!found)
        return float4(1.0f, 1.0f, 1.0f, 1.0f);
    return EncodeIndex(bestIndex);
}
