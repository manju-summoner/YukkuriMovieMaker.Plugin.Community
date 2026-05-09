Texture2D CurrentTexture : register(t0);
SamplerState CurrentSampler : register(s0);
Texture2D TargetTexture : register(t1);
SamplerState TargetSampler : register(s1);
Texture2D MapTexture : register(t2);
SamplerState MapSampler : register(s2);

cbuffer constants : register(b0)
{
    int weightSource : packoffset(c0.x);
    float3 _pad : packoffset(c0.y);
};

float4 SafeSample(Texture2D t, SamplerState s, float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return t.SampleLevel(s, uv, 0);
}

float GetLuminance(float3 c)
{
    return dot(c, float3(0.299f, 0.587f, 0.114f));
}

float ExtractWeight(float4 src, int source)
{
    float3 straightRgb = (src.a > 1e-6f) ? src.rgb / src.a : float3(0.0f, 0.0f, 0.0f);
    [branch]
    switch (source)
    {
        case 0:
            return saturate(GetLuminance(straightRgb));
        case 1:
            return saturate(src.a);
        default:
            return saturate(GetLuminance(straightRgb));
    }
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1,
    float4 uv2 : TEXCOORD2
) : SV_Target
{
    float4 current = SafeSample(CurrentTexture, CurrentSampler, uv0.xy);
    float4 target = SafeSample(TargetTexture, TargetSampler, uv1.xy);
    float4 map = SafeSample(MapTexture, MapSampler, uv2.xy);

    float weight;
    [branch]
    switch (weightSource)
    {
        case 0:
            weight = ExtractWeight(map, 0);
            break;
        case 1:
            weight = ExtractWeight(map, 1);
            break;
        case 2:
            weight = ExtractWeight(target, 0);
            break;
        case 3:
            weight = ExtractWeight(target, 1);
            break;
        case 4:
            weight = ExtractWeight(current, 0);
            break;
        case 5:
            weight = ExtractWeight(current, 1);
            break;
        default:
            weight = ExtractWeight(map, 0);
            break;
    }

    float4 result = lerp(current, target, weight);
    return result;
}
