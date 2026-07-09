Texture2D PrevTexture : register(t0);
SamplerState PrevSampler : register(s0);
Texture2D EmissionTexture : register(t1);
SamplerState EmissionSampler : register(s1);

cbuffer Constants : register(b0)
{
    float worldL     : packoffset(c0.x);
    float worldT     : packoffset(c0.y);
    float worldW     : packoffset(c0.z);
    float worldH     : packoffset(c0.w);

    float buildLevel : packoffset(c1.x);
    float pad0       : packoffset(c1.y);
    float pad1       : packoffset(c1.z);
    float pad2       : packoffset(c1.w);
};

#define OCC_LEVELS 9

uint BlockSize(uint worldSize, uint level)
{
    return max((worldSize + (1u << level) - 1u) >> level, 1u);
}

float SamplePrev(float4 uv0, float2 scenePos, float2 q)
{
    float2 uv = uv0.xy + (q - scenePos) * uv0.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return 0.0f;
    return PrevTexture.SampleLevel(PrevSampler, uv, 0).r;
}

float EmissionOccupancy(float4 uv1, float2 scenePos, float2 q)
{
    float2 uv = uv1.xy + (q - scenePos) * uv1.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return 0.0f;
    float4 f = EmissionTexture.SampleLevel(EmissionSampler, uv, 0);
    return (f.a > 0.003f || max(f.r, max(f.g, f.b)) > 0.003f) ? 1.0f : 0.0f;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    uint ww = (uint)worldW;
    uint wh = (uint)worldH;
    uint level = (uint)buildLevel;

    float2 origin = float2(worldL, worldT);
    float2 local = posScene.xy - origin;
    uint ix = (uint)max((int)floor(local.x), 0);
    uint iy = (uint)max((int)floor(local.y), 0);

    uint top = 0u;
    uint blockOf = 0u;
    uint rowInBlock = 0u;
    [unroll]
    for (uint m = 1u; m <= OCC_LEVELS; m++)
    {
        uint bh = BlockSize(wh, m);
        if (blockOf == 0u && iy < top + bh)
        {
            blockOf = m;
            rowInBlock = iy - top;
        }
        top += bh;
    }

    if (blockOf == 0u || ix >= BlockSize(ww, blockOf) || blockOf > level)
        return float4(0.0f, 0.0f, 0.0f, 1.0f);

    if (blockOf < level)
        return float4(SamplePrev(uv0, posScene.xy, posScene.xy), 0.0f, 0.0f, 1.0f);

    uint childW = level == 1u ? ww : BlockSize(ww, level - 1u);
    uint childH = level == 1u ? wh : BlockSize(wh, level - 1u);
    uint childTop = 0u;
    [unroll]
    for (uint n = 1u; n <= (uint)OCC_LEVELS; n++)
    {
        if (n + 2u <= level)
            childTop += BlockSize(wh, n);
    }

    float occ = 0.0f;
    [unroll]
    for (uint cy = 0u; cy < 2u; cy++)
    {
        [unroll]
        for (uint cx = 0u; cx < 2u; cx++)
        {
            uint jx = min(2u * ix + cx, childW - 1u);
            uint jy = min(2u * rowInBlock + cy, childH - 1u);
            if (level == 1u)
            {
                float2 q = origin + float2((float)jx + 0.5f, (float)jy + 0.5f);
                occ = max(occ, EmissionOccupancy(uv1, posScene.xy, q));
            }
            else
            {
                float2 q = origin + float2((float)jx + 0.5f, (float)(childTop + jy) + 0.5f);
                occ = max(occ, SamplePrev(uv0, posScene.xy, q));
            }
        }
    }

    return float4(occ, 0.0f, 0.0f, 1.0f);
}
