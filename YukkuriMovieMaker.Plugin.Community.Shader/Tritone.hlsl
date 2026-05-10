Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float3 shadowColor : packoffset(c0.x);
    float midPosition : packoffset(c0.w);
    float3 midtoneColor : packoffset(c1.x);
    float _pad1 : packoffset(c1.w);
    float3 highlightColor : packoffset(c2.x);
    float _pad2 : packoffset(c2.w);
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

    float l_ = sign(l) * pow(abs(l), 1.0f / 3.0f);
    float m_ = sign(m) * pow(abs(m), 1.0f / 3.0f);
    float s_ = sign(s) * pow(abs(s), 1.0f / 3.0f);

    return float3(
        0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
        1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
        0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_
    );
}

float3 OklabToLinear(float3 lab)
{
    float l_ = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
    float m_ = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
    float s_ = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;

    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    return float3(
         4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
        -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
        -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s
    );
}

float GetPerceptualLuminance(float3 linearRgb)
{
    return dot(linearRgb, float3(0.2126f, 0.7152f, 0.0722f));
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float4 src = InputTexture.Sample(InputSampler, uv0.xy);

    [branch]
    if (src.a <= 0.0f)
        return src;

    float3 straightSrgb = saturate(src.rgb / src.a);
    float3 straightLinear = SrgbToLinear(straightSrgb);
    float lum = saturate(GetPerceptualLuminance(straightLinear));

    float3 shadowLinear = SrgbToLinear(shadowColor);
    float3 midtoneLinear = SrgbToLinear(midtoneColor);
    float3 highlightLinear = SrgbToLinear(highlightColor);

    float3 shadowLab = LinearToOklab(shadowLinear);
    float3 midtoneLab = LinearToOklab(midtoneLinear);
    float3 highlightLab = LinearToOklab(highlightLinear);

    float m = clamp(midPosition, 1e-4f, 1.0f - 1e-4f);

    float3 mappedLab;
    [branch]
    if (lum <= m)
    {
        float t = lum / m;
        mappedLab = lerp(shadowLab, midtoneLab, t);
    }
    else
    {
        float t = (lum - m) / (1.0f - m);
        mappedLab = lerp(midtoneLab, highlightLab, t);
    }

    float3 mappedLinear = OklabToLinear(mappedLab);
    float3 mappedSrgb = saturate(LinearToSrgb(mappedLinear));

    return float4(mappedSrgb * src.a, src.a);
}
