Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);
Texture2D GradientTexture : register(t1);
SamplerState GradientSampler : register(s1);

cbuffer constants : register(b0)
{
    float opacity : packoffset(c0.x);
    int blendMode : packoffset(c0.y);
    int isHorizontal : packoffset(c0.z);
    float _pad : packoffset(c0.w);
};

float GetLuminance(float3 c)
{
    return dot(c, float3(0.299f, 0.587f, 0.114f));
}

float3 RgbToHsl(float3 c)
{
    float maxC = max(c.r, max(c.g, c.b));
    float minC = min(c.r, min(c.g, c.b));
    float delta = maxC - minC;
    float l = (maxC + minC) * 0.5f;
    float s = (delta > 1e-6f) ? delta / (1.0f - abs(2.0f * l - 1.0f)) : 0.0f;
    float h = 0.0f;
    [flatten]
    if (delta > 1e-6f)
    {
        [flatten]
        if (maxC == c.r)
            h = ((c.g - c.b) / delta) + (c.g < c.b ? 6.0f : 0.0f);
        else if (maxC == c.g)
            h = (c.b - c.r) / delta + 2.0f;
        else
            h = (c.r - c.g) / delta + 4.0f;
        h /= 6.0f;
    }
    return float3(h, s, l);
}

float HueToRgb(float p, float q, float t)
{
    t -= floor(t);
    return (t < 1.0f / 6.0f) ? p + (q - p) * 6.0f * t
         : (t < 0.5f) ? q
         : (t < 2.0f / 3.0f) ? p + (q - p) * (2.0f / 3.0f - t) * 6.0f
                             : p;
}

float3 HslToRgb(float3 hsl)
{
    float h = hsl.x, s = hsl.y, l = hsl.z;
    [flatten]
    if (s < 1e-6f)
        return float3(l, l, l);
    float q = (l < 0.5f) ? l * (1.0f + s) : l + s - l * s;
    float p = 2.0f * l - q;
    return float3(
        HueToRgb(p, q, h + 1.0f / 3.0f),
        HueToRgb(p, q, h),
        HueToRgb(p, q, h - 1.0f / 3.0f));
}

float3 SoftLightPerChannel(float3 s, float3 g)
{
    float3 d = (s <= 0.25f)
        ? ((16.0f * s - 12.0f) * s + 4.0f) * s
        : sqrt(s);
    return (g <= 0.5f)
        ? s - (1.0f - 2.0f * g) * s * (1.0f - s)
        : s + (2.0f * g - 1.0f) * (d - s);
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1
) : SV_Target
{
    float4 src = InputTexture.Sample(InputSampler, uv0.xy);

    [branch]
    if (src.a <= 0.0f)
        return src;

    float3 s = src.rgb / src.a;
    float lum = saturate(GetLuminance(s));

    float2 gradUV = (isHorizontal != 0)
        ? float2(lum, 0.5f)
        : float2(0.5f, 1.0f - lum);

    float4 gs = GradientTexture.Sample(GradientSampler, gradUV);
    float3 g = (gs.a > 1e-6f) ? gs.rgb / gs.a : float3(0.0f, 0.0f, 0.0f);

    float3 safeInvG = 1.0f / max(g, 1e-6f);
    float3 safeInvS = 1.0f / max(s, 1e-6f);

    float3 results[27];

    results[0] = g;
    results[1] = saturate(s + (GetLuminance(g) - lum));
    {
        float gl = length(g);
        results[2] = (gl > 1e-6f) ? saturate(normalize(g) * lum) : s;
    }
    results[3] = min(s, g);
    results[4] = s * g;
    results[5] = saturate(1.0f - (1.0f - s) * safeInvG);
    results[6] = saturate(s + g - 1.0f);
    results[7] = saturate(s - g);
    results[8] = max(s, g);
    results[9] = 1.0f - (1.0f - s) * (1.0f - g);
    results[10] = saturate(s * safeInvG / max(1.0f - g, 1e-6f));
    results[11] = saturate(s + g);
    results[12] = min(s + g, 1.0f);
    results[13] = (s < 0.5f) ? 2.0f * s * g : 1.0f - 2.0f * (1.0f - s) * (1.0f - g);
    results[14] = saturate(SoftLightPerChannel(s, g));
    results[15] = (g < 0.5f) ? 2.0f * g * s : 1.0f - 2.0f * (1.0f - g) * (1.0f - s);
    results[16] = abs(s - g);
    {
        float3 burn = saturate(1.0f - (1.0f - s) / max(2.0f * g, 1e-6f));
        float3 dodge = saturate(s / max(2.0f * (1.0f - g), 1e-6f));
        results[17] = (g <= 0.5f) ? burn : dodge;
    }
    results[18] = saturate(2.0f * g + s - 1.0f);
    results[19] = (g > 0.5f) ? max(s, 2.0f * (g - 0.5f)) : min(s, 2.0f * g);
    results[20] = step(1.0f, s + g);
    results[21] = s + g - 2.0f * s * g;
    {
        float lumS = GetLuminance(s), lumG = GetLuminance(g);
        results[22] = (lumG <= lumS) ? g : s;
        results[23] = (lumG >= lumS) ? g : s;
    }
    results[24] = saturate(s * safeInvG);
    {
        float3 hslS = RgbToHsl(s), hslG = RgbToHsl(g);
        results[25] = saturate(HslToRgb(float3(hslS.x, hslG.y, hslS.z)));
        results[26] = saturate(HslToRgb(float3(hslG.x, hslG.y, hslS.z)));
    }

    float3 blended = results[clamp(blendMode, 0, 26)];
    float3 result = lerp(s, blended, opacity);
    return saturate(float4(result * src.a, src.a));
}
