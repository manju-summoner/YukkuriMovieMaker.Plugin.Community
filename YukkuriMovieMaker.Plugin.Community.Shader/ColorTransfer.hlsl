Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

#define LUT_SIZE 128
#define GAMUT_ITERATIONS 10

cbuffer constants : register(b0)
{
    float3 domainMin : packoffset(c0.x);
    float lightnessAmount : packoffset(c0.w);
    float3 domainScale : packoffset(c1.x);
    float colorAmount : packoffset(c1.w);
    float4 transferLut[LUT_SIZE] : packoffset(c2);
};

float3 SrgbToLinear(float3 c)
{
    float3 lo = c / 12.92f;
    float3 hi = pow(max(c + 0.055f, 0.0f) / 1.055f, 2.4f);
    return (c <= 0.04045f) ? lo : hi;
}

float3 LinearToSrgb(float3 c)
{
    c = max(c, 0.0f);
    float3 lo = c * 12.92f;
    float3 hi = 1.055f * pow(c, 1.0f / 2.4f) - 0.055f;
    return (c <= 0.0031308f) ? lo : hi;
}

float3 LinearToOklab(float3 c)
{
    float l = 0.4122214708f * c.r + 0.5363325363f * c.g + 0.0514459929f * c.b;
    float m = 0.2119034982f * c.r + 0.6806995451f * c.g + 0.1073969566f * c.b;
    float s = 0.0883024619f * c.r + 0.2817188376f * c.g + 0.6299787005f * c.b;

    float lRoot = pow(max(l, 0.0f), 1.0f / 3.0f);
    float mRoot = pow(max(m, 0.0f), 1.0f / 3.0f);
    float sRoot = pow(max(s, 0.0f), 1.0f / 3.0f);

    return float3(
        0.2104542553f * lRoot + 0.7936177850f * mRoot - 0.0040720468f * sRoot,
        1.9779984951f * lRoot - 2.4285922050f * mRoot + 0.4505937099f * sRoot,
        0.0259040371f * lRoot + 0.7827717662f * mRoot - 0.8086757660f * sRoot
    );
}

float3 OklabToLinear(float3 lab)
{
    float lRoot = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
    float mRoot = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
    float sRoot = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;

    float l = lRoot * lRoot * lRoot;
    float m = mRoot * mRoot * mRoot;
    float s = sRoot * sRoot * sRoot;

    return float3(
         4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
        -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
        -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s
    );
}

float3 SampleTransfer(float3 lab)
{
    float3 position = saturate((lab - domainMin) * domainScale) * (float)(LUT_SIZE - 1);
    float3 lowIndex = floor(position);
    float3 weight = position - lowIndex;

    int3 i0 = (int3)lowIndex;
    int3 i1 = min(i0 + 1, LUT_SIZE - 1);

    float3 low = float3(transferLut[i0.x].x, transferLut[i0.y].y, transferLut[i0.z].z);
    float3 high = float3(transferLut[i1.x].x, transferLut[i1.y].y, transferLut[i1.z].z);
    return lerp(low, high, weight);
}

bool IsInGamut(float3 linearRgb)
{
    return all(linearRgb >= -1e-4f) && all(linearRgb <= 1.0f + 1e-4f);
}

float3 ToGamut(float3 lab)
{
    lab.x = saturate(lab.x);

    float3 linearRgb = OklabToLinear(lab);
    if (IsInGamut(linearRgb))
        return saturate(linearRgb);

    float low = 0.0f;
    float high = 1.0f;
    [unroll]
    for (int i = 0; i < GAMUT_ITERATIONS; i++)
    {
        float middle = 0.5f * (low + high);
        if (IsInGamut(OklabToLinear(float3(lab.x, lab.yz * middle))))
            low = middle;
        else
            high = middle;
    }
    return saturate(OklabToLinear(float3(lab.x, lab.yz * low)));
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float4 src = InputTexture.Sample(InputSampler, uv0.xy);

    [branch]
    if (src.a <= 0.0f || (lightnessAmount <= 0.0f && colorAmount <= 0.0f))
        return src;

    float3 straightSrgb = saturate(src.rgb / src.a);
    float3 lab = LinearToOklab(SrgbToLinear(straightSrgb));
    float3 mapped = SampleTransfer(lab);

    float3 transferred = float3(
        lerp(lab.x, mapped.x, lightnessAmount),
        lerp(lab.y, mapped.y, colorAmount),
        lerp(lab.z, mapped.z, colorAmount));

    float3 result = saturate(LinearToSrgb(ToGamut(transferred)));
    return float4(result * src.a, src.a);
}
