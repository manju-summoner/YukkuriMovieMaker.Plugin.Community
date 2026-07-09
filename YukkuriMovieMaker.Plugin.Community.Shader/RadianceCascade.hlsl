Texture2D EmissionTexture : register(t0);
SamplerState EmissionSampler : register(s0);
Texture2D UpperTexture : register(t1);
SamplerState UpperSampler : register(s1);
Texture2D OccupancyTexture : register(t2);
SamplerState OccupancySampler : register(s2);

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
    float worldW        : packoffset(c2.w);

    float worldH        : packoffset(c3.x);
    float gain          : packoffset(c3.y);
    float pad0          : packoffset(c3.z);
    float pad1          : packoffset(c3.w);
};

#define SIGMA 0.6f
#define FALLOFF_SOFT 2.0f
#define FINE_STEP 1.0f
#define FINE_GROW 0.03f
#define FINE_MAX 32.0f
#define MAX_ITER 128
#define OCC_LEVELS 9

float4 SampleEmissionWorld(float4 uv0, float2 scenePos, float2 q)
{
    float2 uv = uv0.xy + (q - scenePos) * uv0.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return EmissionTexture.SampleLevel(EmissionSampler, uv, 0);
}

uint OccBlockSize(uint worldSize, uint level)
{
    return max((worldSize + (1u << level) - 1u) >> level, 1u);
}

float OccupancyAt(float4 uv2, float2 scenePos, uint level, float2 q)
{
    uint ww = (uint)worldW;
    uint wh = (uint)worldH;
    uint bw = OccBlockSize(ww, level);
    uint bh = OccBlockSize(wh, level);

    float cell = (float)(1u << level);
    float2 origin = float2(worldL, worldT);
    int bx = (int)floor((q.x - worldL) / cell);
    int by = (int)floor((q.y - worldT) / cell);
    bx = clamp(bx, 0, (int)bw - 1);
    by = clamp(by, 0, (int)bh - 1);

    uint top = 0u;
    [unroll]
    for (uint n = 1u; n <= (uint)OCC_LEVELS; n++)
    {
        if (n < level)
            top += OccBlockSize(wh, n);
    }

    float2 pixel = origin + float2((float)bx + 0.5f, (float)(top + (uint)by) + 0.5f);
    float2 uv = uv2.xy + (pixel - scenePos) * uv2.zw;
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return 0.0f;
    return OccupancyTexture.SampleLevel(OccupancySampler, uv, 0).r;
}

float CellExit(float2 q, float2 dir, float2 invDir, uint level)
{
    float cell = (float)(1u << level);
    float2 local = (q - float2(worldL, worldT)) / cell;
    float2 frac = local - floor(local);

    float tx = 1e9f;
    if (dir.x > 1e-6f)
        tx = (1.0f - frac.x) * cell * invDir.x;
    else if (dir.x < -1e-6f)
        tx = frac.x * cell * (-invDir.x);

    float ty = 1e9f;
    if (dir.y > 1e-6f)
        ty = (1.0f - frac.y) * cell * invDir.y;
    else if (dir.y < -1e-6f)
        ty = frac.y * cell * (-invDir.y);

    return min(tx, ty) + 0.05f;
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
    uint ts = (uint)tilesSide;
    uint pw = (uint)probeW;
    uint ph = (uint)probeH;

    float2 local = posScene.xy - float2(worldL, worldT);
    uint ix = (uint)clamp((int)floor(local.x), 0, (int)(ts * pw) - 1);
    uint iy = (uint)clamp((int)floor(local.y), 0, (int)(ts * ph) - 1);

    uint tileX = ix / pw;
    uint tileY = iy / ph;
    uint probeX = ix - tileX * pw;
    uint probeY = iy - tileY * ph;

    uint dirs = ts * ts;
    uint d = tileY * ts + tileX;

    float ang = 6.2831853f * ((float)d + 0.5f) / (float)dirs;
    float2 dir;
    sincos(ang, dir.y, dir.x);
    float2 invDir = 1.0f / dir;

    float2 probeWorld = float2(worldL, worldT) + (float2((float)probeX, (float)probeY) + 0.5f) * spacing;

    float transmittance = 1.0f;
    float3 gather = float3(0.0f, 0.0f, 0.0f);
    float t = intervalStart;
    uint level = 3u;

    [loop]
    for (int j = 0; j < MAX_ITER; j++)
    {
        if (t >= intervalEnd || transmittance <= 0.004f)
            break;

        float2 q = probeWorld + dir * t;

        if (level > 0u)
        {
            if (OccupancyAt(uv2, posScene.xy, level, q) < 0.5f)
            {
                t += max(min(CellExit(q, dir, invDir, level), intervalEnd - t), 0.05f);
                level = min(level + 1u, (uint)OCC_LEVELS);
            }
            else
            {
                level--;
            }
            continue;
        }

        float step = clamp(t * FINE_GROW, FINE_STEP, FINE_MAX);
        float h = max(min(step, intervalEnd - t), 0.25f);
        float4 f = SampleEmissionWorld(uv0, posScene.xy, q);
        gather += transmittance * f.rgb * gain * (h / (t + FALLOFF_SOFT));
        transmittance *= exp(-f.a * SIGMA * h);
        t += h;
        level = (f.a > 0.003f || max(f.r, max(f.g, f.b)) > 0.003f) ? 0u : 1u;
    }

    float3 upper = float3(0.0f, 0.0f, 0.0f);
    if (isTop < 0.5f)
    {
        uint upTs = ts * 2u;
        float ux = clamp((probeWorld.x - worldL) / (spacing * 2.0f) - 0.5f, 0.0f, upProbeW - 1.0f);
        float uy = clamp((probeWorld.y - worldT) / (spacing * 2.0f) - 0.5f, 0.0f, upProbeH - 1.0f);

        [unroll]
        for (uint k = 0u; k < 4u; k++)
        {
            uint dc = 4u * d + k;
            uint tcX = dc % upTs;
            uint tcY = dc / upTs;
            float2 q = float2(worldL, worldT) + float2((float)tcX * upProbeW + ux + 0.5f, (float)tcY * upProbeH + uy + 0.5f);
            upper += SampleUpperAtlas(uv1, posScene.xy, q);
        }
        upper *= 0.25f;
    }

    float3 radiance = saturate(gather + transmittance * upper);
    return float4(radiance, 1.0f);
}
