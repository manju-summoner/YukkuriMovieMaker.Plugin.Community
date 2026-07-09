Texture2D EmissionTexture : register(t0);
SamplerState EmissionSampler : register(s0);
Texture2D UpperTexture : register(t1);
SamplerState UpperSampler : register(s1);
Texture2D JfaTexture : register(t2);
SamplerState JfaSampler : register(s2);

cbuffer Constants : register(b0)
{
    float worldL        : packoffset(c0.x);
    float worldT        : packoffset(c0.y);
    float spacing       : packoffset(c0.z);
    float tilesSide     : packoffset(c0.w);

    float probeW        : packoffset(c1.x);
    float probeH        : packoffset(c1.y);
    float intervalStart : packoffset(c1.z);
    float intervalEnd   : packoffset(c1.w);

    float upProbeW      : packoffset(c2.x);
    float upProbeH      : packoffset(c2.y);
    float isTop         : packoffset(c2.z);
    float pad0          : packoffset(c2.w);
};

#define SIGMA 0.6f
#define FALLOFF_SOFT 2.0f
#define FINE_STEP 1.5f
#define MAX_ITER 48
#define SKIP_CAP 256.0f
#define SDF_FAR 4096.0f

float4 SampleEmissionWorld(float4 uv0, float2 scenePos, float2 q)
{
    float2 uv = uv0.xy + (q - scenePos) * uv0.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return EmissionTexture.SampleLevel(EmissionSampler, uv, 0);
}

float SdfAt(float4 uv2, float2 scenePos, float2 q)
{
    float2 origin = float2(worldL, worldT);
    float2 snapped = floor(q - origin) + 0.5f + origin;
    float2 uv = uv2.xy + (snapped - scenePos) * uv2.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return SDF_FAR;

    float4 f = JfaTexture.SampleLevel(JfaSampler, uv, 0);
    float hiX = round(f.r * 255.0f);
    float loX = round(f.g * 255.0f);
    float hiY = round(f.b * 255.0f);
    float loY = round(f.a * 255.0f);
    if (hiX >= 254.5f && hiY >= 254.5f)
        return SDF_FAR;

    float2 seed = origin + float2(hiX * 256.0f + loX, hiY * 256.0f + loY) + 0.5f;
    return length(q - seed);
}

float3 SampleUpperAtlas(float4 uv1, float2 scenePos, float2 q)
{
    float2 uv = uv1.xy + (q - scenePos) * uv1.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float3(0.0f, 0.0f, 0.0f);
    return UpperTexture.SampleLevel(UpperSampler, uv, 0).rgb;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1,
    float4 uv2      : TEXCOORD2
) : SV_TARGET
{
    int ts = (int)tilesSide;
    int pw = (int)probeW;
    int ph = (int)probeH;

    float2 local = posScene.xy - float2(worldL, worldT);
    int ix = clamp((int)floor(local.x), 0, ts * pw - 1);
    int iy = clamp((int)floor(local.y), 0, ts * ph - 1);

    int tileX = ix / pw;
    int tileY = iy / ph;
    int probeX = ix - tileX * pw;
    int probeY = iy - tileY * ph;

    int dirs = ts * ts;
    int d = tileY * ts + tileX;

    float ang = 6.2831853f * ((float)d + 0.5f) / (float)dirs;
    float2 dir;
    sincos(ang, dir.y, dir.x);

    float2 probeWorld = float2(worldL, worldT) + (float2((float)probeX, (float)probeY) + 0.5f) * spacing;

    float transmittance = 1.0f;
    float3 gather = float3(0.0f, 0.0f, 0.0f);
    float t = intervalStart;

    [loop]
    for (int j = 0; j < MAX_ITER; j++)
    {
        if (t >= intervalEnd || transmittance <= 0.004f)
            break;

        float2 q = probeWorld + dir * t;
        float sdf = SdfAt(uv2, posScene.xy, q) - 1.0f;

        if (sdf > FINE_STEP)
        {
            t += max(min(min(sdf, SKIP_CAP), intervalEnd - t), 0.25f);
            continue;
        }

        float h = max(min(FINE_STEP, intervalEnd - t), 0.25f);
        float4 f = SampleEmissionWorld(uv0, posScene.xy, q);
        gather += transmittance * f.rgb * (h / (t + FALLOFF_SOFT));
        transmittance *= exp(-f.a * SIGMA * h);
        t += h;
    }

    float3 upper = float3(0.0f, 0.0f, 0.0f);
    if (isTop < 0.5f)
    {
        int upTs = ts * 2;
        float ux = clamp((probeWorld.x - worldL) / (spacing * 2.0f) - 0.5f, 0.0f, upProbeW - 1.0f);
        float uy = clamp((probeWorld.y - worldT) / (spacing * 2.0f) - 0.5f, 0.0f, upProbeH - 1.0f);

        [unroll]
        for (int k = 0; k < 4; k++)
        {
            int dc = 4 * d + k;
            int tcX = dc % upTs;
            int tcY = dc / upTs;
            float2 q = float2(worldL, worldT) + float2((float)tcX * upProbeW + ux + 0.5f, (float)tcY * upProbeH + uy + 0.5f);
            upper += SampleUpperAtlas(uv1, posScene.xy, q);
        }
        upper *= 0.25f;
    }

    float3 radiance = saturate(gather + transmittance * upper);
    return float4(radiance, 1.0f);
}
