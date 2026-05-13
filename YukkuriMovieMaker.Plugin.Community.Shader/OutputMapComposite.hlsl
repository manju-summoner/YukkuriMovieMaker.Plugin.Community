Texture2D CurrentTexture : register(t0);
SamplerState CurrentSampler : register(s0);
Texture2D TargetTexture : register(t1);
SamplerState TargetSampler : register(s1);
Texture2D MapTexture : register(t2);
SamplerState MapSampler : register(s2);

cbuffer constants : register(b0)
{
    int mapType : packoffset(c0.x);
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
    if (mapType == 1)
    {
        weight = saturate(map.a);
    }
    else
    {
        float3 straightRgb = (map.a > 1e-6f) ? map.rgb / map.a : float3(0.0f, 0.0f, 0.0f);
        weight = saturate(GetLuminance(straightRgb));
    }

    return lerp(current, target, weight);
}
