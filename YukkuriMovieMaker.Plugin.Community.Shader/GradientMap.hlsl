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

    float3 blended;
    [branch] switch (blendMode)
    {
        case 0:
            blended = g;
            break;
        case 1:
            blended = saturate(s + (GetLuminance(g) - lum));
            break;
        case 2:
        {
            float gl = length(g);
            blended = (gl > 1e-6f) ? saturate(normalize(g) * lum) : s;
            break;
        }
        case 3:
            blended = min(s, g);
            break;
        case 4:
            blended = s * g;
            break;
        case 5:
            blended = saturate(1.0f - (1.0f - s) * safeInvG);
            break;
        case 6:
            blended = saturate(s + g - 1.0f);
            break;
        case 7:
            blended = saturate(s - g);
            break;
        case 8:
            blended = max(s, g);
            break;
        case 9:
            blended = 1.0f - (1.0f - s) * (1.0f - g);
            break;
        case 10:
            blended = saturate(s * safeInvG / max(1.0f - g, 1e-6f));
            break;
        case 11:
            blended = saturate(s + g);
            break;
        case 12:
            blended = min(s + g, 1.0f);
            break;
        case 13:
            blended = (s < 0.5f) ? 2.0f * s * g : 1.0f - 2.0f * (1.0f - s) * (1.0f - g);
            break;
        case 14:
            blended = saturate(SoftLightPerChannel(s, g));
            break;
        case 15:
            blended = (g < 0.5f) ? 2.0f * g * s : 1.0f - 2.0f * (1.0f - g) * (1.0f - s);
            break;
        case 16:
            blended = abs(s - g);
            break;
        case 17:
        {
            float3 burn = saturate(1.0f - (1.0f - s) / max(2.0f * g, 1e-6f));
            float3 dodge = saturate(s / max(2.0f * (1.0f - g), 1e-6f));
            blended = (g <= 0.5f) ? burn : dodge;
            break;
        }
        case 18:
            blended = saturate(2.0f * g + s - 1.0f);
            break;
        case 19:
            blended = (g > 0.5f) ? max(s, 2.0f * (g - 0.5f)) : min(s, 2.0f * g);
            break;
        case 20:
            blended = step(1.0f, s + g);
            break;
        case 21:
            blended = s + g - 2.0f * s * g;
            break;
        case 22:
        {
            float lumS = GetLuminance(s), lumG = GetLuminance(g);
            blended = (lumG <= lumS) ? g : s;
            break;
        }
        case 23:
        {
            float lumS = GetLuminance(s), lumG = GetLuminance(g);
            blended = (lumG >= lumS) ? g : s;
            break;
        }
        case 24:
            blended = saturate(s * safeInvG);
            break;
        case 25:
        {
            float3 hslS = RgbToHsl(s), hslG = RgbToHsl(g);
            blended = saturate(HslToRgb(float3(hslS.x, hslG.y, hslS.z)));
            break;
        }
        case 26:
        {
            float3 hslS = RgbToHsl(s), hslG = RgbToHsl(g);
            blended = saturate(HslToRgb(float3(hslG.x, hslG.y, hslS.z)));
            break;
        }
        default:
            blended = g;
            break;
    }

    float3 result = lerp(s, blended, opacity);
    return saturate(float4(result * src.a, src.a));
}
