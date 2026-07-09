Texture2D EmissionTexture : register(t0);
SamplerState EmissionSampler : register(s0);
Texture2D UpperTexture : register(t1);
SamplerState UpperSampler : register(s1);

cbuffer Constants : register(b0)
{
    float intervalStart : packoffset(c0.x);
    float intervalEnd   : packoffset(c0.y);
    float phase         : packoffset(c0.z);
    float isTop         : packoffset(c0.w);
};

#define DIRECTIONS 8
#define STEPS 4
#define SIGMA 0.6f
#define FALLOFF_SOFT 2.0f
#define CONE_SPREAD 0.27f

float4 SampleEmission(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return EmissionTexture.SampleLevel(EmissionSampler, uv, 0);
}

float4 SampleUpper(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return UpperTexture.SampleLevel(UpperSampler, uv, 0);
}

float4 ConeTap(float2 uvBase, float2 texel, float2 center, float2 tangent, float halfWidth)
{
    float4 a = SampleEmission(uvBase + (center - tangent * halfWidth) * texel);
    float4 b = SampleEmission(uvBase + center * texel);
    float4 c = SampleEmission(uvBase + (center + tangent * halfWidth) * texel);
    return (a + b + c) / 3.0f;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float2 texel = uv0.zw;
    float dt = max(intervalEnd - intervalStart, 1e-3f) / (float)STEPS;

    float3 gather = float3(0.0f, 0.0f, 0.0f);
    float vsum = 0.0f;

    [unroll]
    for (int k = 0; k < DIRECTIONS; k++)
    {
        float ang = 6.2831853f * ((float)k + 0.5f) / (float)DIRECTIONS + phase;
        float2 dir;
        sincos(ang, dir.y, dir.x);
        float2 tangent = float2(-dir.y, dir.x);

        float transmittance = 1.0f;
        float3 ray = float3(0.0f, 0.0f, 0.0f);

        [unroll]
        for (int j = 0; j < STEPS; j++)
        {
            float t = intervalStart + dt * ((float)j + 0.5f);
            float4 f = ConeTap(uv0.xy, texel, dir * t, tangent, t * 0.4142f * CONE_SPREAD * 2.0f);
            ray += transmittance * f.rgb * (dt / (t + FALLOFF_SOFT));
            transmittance *= exp(-f.a * SIGMA * dt);
        }

        gather += ray;
        vsum += transmittance;
    }

    float v = vsum / (float)DIRECTIONS;
    float3 upper = (isTop > 0.5f) ? float3(0.0f, 0.0f, 0.0f) : SampleUpper(uv1.xy).rgb;
    float3 radiance = saturate(gather + v * upper);

    return float4(radiance, saturate(v));
}
