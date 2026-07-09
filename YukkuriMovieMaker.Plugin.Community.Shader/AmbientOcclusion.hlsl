Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float strength       : packoffset(c0.x);
    float radiusPx       : packoffset(c0.y);
    float heightGain     : packoffset(c0.z);
    float directionCount : packoffset(c0.w);

    float stepCount      : packoffset(c1.x);
    float shadowR        : packoffset(c1.y);
    float shadowG        : packoffset(c1.z);
    float shadowB        : packoffset(c1.w);
};

#define MAX_DIRECTIONS 16
#define MAX_STEPS 12

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float LumAt(float2 uv, float2 texel, float2 offsetPx, float fallback)
{
    float4 s = SampleInput(uv + offsetPx * texel);
    if (s.a <= 1e-3f)
        return fallback;
    return dot(s.rgb / s.a, float3(0.2126f, 0.7152f, 0.0722f));
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (strength <= 0.0f || source.a <= 0.0f)
        return source;

    float2 texel = uv0.zw;
    float centerLum = dot(source.rgb / source.a, float3(0.2126f, 0.7152f, 0.0722f));

    int dirs = (int)clamp(directionCount, 2.0f, (float)MAX_DIRECTIONS);
    int steps = (int)clamp(stepCount, 1.0f, (float)MAX_STEPS);
    float radius = max(radiusPx, 1.0f);
    float amp = heightGain * 40.0f;

    float aoSum = 0.0f;

    [loop]
    for (int k = 0; k < dirs; k++)
    {
        float ang = 6.2831853f * (float)k / (float)dirs;
        float2 dir;
        sincos(ang, dir.y, dir.x);

        float horizon = 0.0f;
        [loop]
        for (int j = 0; j < steps; j++)
        {
            float t = radius * ((float)j + 0.5f) / (float)steps;
            float hs = LumAt(uv0.xy, texel, dir * t, centerLum);
            float slope = (hs - centerLum) * amp / t;
            horizon = max(horizon, slope);
        }

        float occ = horizon / (1.0f + horizon);
        aoSum += occ;
    }

    float ao = saturate(aoSum / (float)dirs * 1.5f);

    float3 shade = lerp(float3(1.0f, 1.0f, 1.0f), float3(shadowR, shadowG, shadowB), ao * strength);
    float4 result;
    result.rgb = source.rgb * shade;
    result.a = source.a;
    return result;
}
