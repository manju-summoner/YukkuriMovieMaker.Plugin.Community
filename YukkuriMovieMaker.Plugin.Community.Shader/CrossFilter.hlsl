Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float strength   : packoffset(c0.x);
    float threshold  : packoffset(c0.y);
    float lengthPx   : packoffset(c0.z);
    float rayCount   : packoffset(c0.w);

    float angleRad   : packoffset(c1.x);
    float dispersion : packoffset(c1.y);
    float thickness  : packoffset(c1.z);
    int   lightOnly  : packoffset(c1.w);

    float lightR     : packoffset(c2.x);
    float lightG     : packoffset(c2.y);
    float lightB     : packoffset(c2.z);
    float pad0       : packoffset(c2.w);
};

#define SAMPLES 24

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    float4 source = SampleInput(uv0.xy);
    if (strength <= 0.0f && lightOnly == 0)
        return source;

    int rays = (int)clamp(rayCount, 1.0f, 16.0f);
    float len = max(lengthPx, 1.0f);
    float knee = max(1.0f - threshold, 0.05f);

    float fR = 1.0f + 0.4f * dispersion;
    float fB = max(1.0f - 0.35f * dispersion, 0.1f);

    float3 acc = float3(0.0f, 0.0f, 0.0f);
    float norm = 0.0f;

    [loop]
    for (int k = 0; k < rays; k++)
    {
        float ang = angleRad + 6.2831853f * (float)k / (float)rays;
        float2 dir;
        sincos(ang, dir.y, dir.x);
        float2 perp = float2(-dir.y, dir.x);

        [loop]
        for (int i = 0; i < SAMPLES; i++)
        {
            float t = len * ((float)i + 0.5f) / (float)SAMPLES;
            float2 basePx = posScene.xy + dir * t;

            float4 s;
            if (thickness > 0.25f)
            {
                s = SampleInput(uv0.xy + (basePx - posScene.xy) * uv0.zw) * 0.5f
                  + SampleInput(uv0.xy + (basePx + perp * thickness - posScene.xy) * uv0.zw) * 0.25f
                  + SampleInput(uv0.xy + (basePx - perp * thickness - posScene.xy) * uv0.zw) * 0.25f;
            }
            else
            {
                s = SampleInput(uv0.xy + (basePx - posScene.xy) * uv0.zw);
            }

            float lum = dot(s.rgb, float3(0.299f, 0.587f, 0.114f));
            float mask = saturate((lum - threshold) / knee);
            mask *= mask;

            float u = 3.0f * t / len;
            float wR = 1.0f / (1.0f + (u / fR) * (u / fR));
            float wG = 1.0f / (1.0f + u * u);
            float wB = 1.0f / (1.0f + (u / fB) * (u / fB));

            acc += s.rgb * mask * float3(wR, wG, wB);
            norm += wG;
        }
    }

    float3 light = acc / max(norm, 1e-4f) * float3(lightR, lightG, lightB) * (strength * 3.0f);

    if (lightOnly != 0)
    {
        float aL = saturate(max(light.r, max(light.g, light.b)));
        return float4(min(light, float3(aL, aL, aL)), aL);
    }

    float4 result;
    result.a = max(source.a, saturate(max(light.r, max(light.g, light.b))));
    result.rgb = min(source.rgb + light, float3(result.a, result.a, result.a));
    return result;
}
