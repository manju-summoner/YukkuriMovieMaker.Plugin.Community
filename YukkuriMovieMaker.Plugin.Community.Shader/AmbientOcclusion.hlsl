Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);
Texture2D FieldTexture : register(t1);
SamplerState FieldSampler : register(s1);

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

    float suppression    : packoffset(c2.x);
    float pad0           : packoffset(c2.y);
    float pad1           : packoffset(c2.z);
    float pad2           : packoffset(c2.w);
};

#define MAX_DIRECTIONS 16
#define MAX_STEPS 12

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 SampleField(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return FieldTexture.SampleLevel(FieldSampler, uv, 0);
}

// 高さ差は生の差分ではなく、区分ごとに塗り境界重み(w)でゲートした
// 経路積分として累積する。抑制0では望遠鏡和により生の差分と一致する。
float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (strength <= 0.0f || source.a <= 0.0f)
        return source;

    float4 f0 = SampleField(uv1.xy);
    float centerLum = (f0.a > 1e-3f) ? saturate(f0.g / f0.a) : 0.0f;
    float centerW = (f0.a > 1e-3f) ? saturate(f0.r / f0.a) : 1.0f;

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
        float hRel = 0.0f;
        float prevLum = centerLum;
        float prevW = centerW;
        float prevT = 0.0f;

        [loop]
        for (int j = 0; j < steps; j++)
        {
            float t = radius * ((float)j + 0.5f) / (float)steps;
            float4 fm = SampleField(uv1.xy + dir * ((prevT + t) * 0.5f) * uv1.zw);
            float4 fs = SampleField(uv1.xy + dir * t * uv1.zw);
            float wMid = (fm.a > 1e-3f) ? saturate(fm.r / fm.a) : 1.0f;
            float ws = (fs.a > 1e-3f) ? saturate(fs.r / fs.a) : 1.0f;
            float lum = (fs.a > 1e-3f) ? saturate(fs.g / fs.a) : prevLum;

            float seg = lum - prevLum;
            float wSeg = min(ws, min(prevW, wMid));
            hRel += seg * lerp(1.0f, wSeg, suppression);

            float slope = hRel * amp / t;
            horizon = max(horizon, slope);

            prevLum = lum;
            prevW = ws;
            prevT = t;
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
