Texture2D EmissionTexture : register(t0);
SamplerState EmissionSampler : register(s0);

cbuffer Constants : register(b0)
{
    float originL : packoffset(c0.x);
    float originT : packoffset(c0.y);
    float pad0    : packoffset(c0.z);
    float pad1    : packoffset(c0.w);
};

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
    float2 uv = uv0.xy;
    float4 f = float4(0.0f, 0.0f, 0.0f, 0.0f);
    if (uv.x >= 0.0f && uv.x <= 1.0f && uv.y >= 0.0f && uv.y <= 1.0f)
        f = EmissionTexture.SampleLevel(EmissionSampler, uv, 0);

    bool nonEmpty = f.a > 0.003f || max(f.r, max(f.g, f.b)) > 0.003f;
    if (!nonEmpty)
        return float4(1.0f, 1.0f, 1.0f, 1.0f);

    float2 index = floor(posScene.xy - float2(originL, originT));
    return EncodeIndex(clamp(index, 0.0f, 65279.0f));
}
