Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);
Texture2D RadianceTexture : register(t1);
SamplerState RadianceSampler : register(s1);

cbuffer Constants : register(b0)
{
    float strength : packoffset(c0.x);
    float diffuse  : packoffset(c0.y);
    float ambient  : packoffset(c0.z);
    float pad0     : packoffset(c0.w);
};

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 SampleRadiance(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return RadianceTexture.SampleLevel(RadianceSampler, uv, 0);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    float3 light = SampleRadiance(uv1.xy).rgb * strength;

    float3 surface = float3(0.0f, 0.0f, 0.0f);
    if (source.a > 1e-3f)
    {
        float3 albedo = source.rgb / source.a;
        surface = light * albedo * diffuse * source.a;
    }

    float3 airGlow = light * (1.0f - diffuse);
    float glowAlpha = saturate(max(airGlow.r, max(airGlow.g, airGlow.b)));

    float alpha = saturate(source.a + glowAlpha * (1.0f - source.a));
    float3 rgb = min(saturate(source.rgb * ambient + surface + airGlow), alpha);

    return float4(rgb, alpha);
}
