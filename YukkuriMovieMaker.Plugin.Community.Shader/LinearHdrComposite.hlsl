Texture2D BaseTexture : register(t0);
SamplerState BaseSampler : register(s0);
Texture2D ReflectionTexture : register(t1);
SamplerState ReflectionSampler : register(s1);

cbuffer constants : register(b0)
{
    int blendMode : packoffset(c0.x);
};

float3 SrgbToLinear(float3 color)
{
    float3 low = color / 12.92;
    float3 high = pow(max((color + 0.055) / 1.055, 0.0), 2.4);
    return color <= 0.04045 ? low : high;
}

float3 LinearToSrgb(float3 color)
{
    color = max(color, 0.0);
    float3 low = color * 12.92;
    float3 high = 1.055 * pow(color, 1.0 / 2.4) - 0.055;
    return color <= 0.0031308 ? low : high;
}

float3 Overlay(float3 baseColor, float3 blendColor)
{
    return baseColor <= 0.5
        ? 2.0 * baseColor * blendColor
        : 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
}

float3 SoftLight(float3 baseColor, float3 blendColor)
{
    float3 d = baseColor <= 0.25
        ? ((16.0 * baseColor - 12.0) * baseColor + 4.0) * baseColor
        : sqrt(max(baseColor, 0.0));
    return blendColor <= 0.5
        ? baseColor - (1.0 - 2.0 * blendColor) * baseColor * (1.0 - baseColor)
        : baseColor + (2.0 * blendColor - 1.0) * (d - baseColor);
}

float3 BlendLinear(float3 baseColor, float3 reflection, int mode)
{
    //対応値はReflectionAndExtrusionEffectProcessor.IsLinearHdrBlendSupportedと同期すること。
    //未対応値はC#側でDirect2Dの非HDR合成へフォールバックする。
    if (mode == 0) return reflection;
    if (mode == 1 || mode == 104) return baseColor + reflection;
    if (mode == 2) return baseColor - reflection;
    if (mode == 3) return baseColor * reflection;
    if (mode == 4) return 1.0 - (1.0 - baseColor) * (1.0 - reflection);
    if (mode == 5) return Overlay(baseColor, reflection);
    if (mode == 6) return max(baseColor, reflection);
    if (mode == 7) return min(baseColor, reflection);
    if (mode == 10) return baseColor + reflection - 1.0;
    if (mode == 11) return baseColor + 2.0 * reflection - 1.0;
    if (mode == 12) return abs(baseColor - reflection);
    if (mode == 101) return 1.0 - (1.0 - baseColor) / max(reflection, 0.000001);
    if (mode == 103) return baseColor / max(1.0 - reflection, 0.000001);
    if (mode == 106) return SoftLight(baseColor, reflection);
    if (mode == 107) return Overlay(reflection, baseColor);
    if (mode == 108) return reflection <= 0.5
        ? 1.0 - (1.0 - baseColor) / max(2.0 * reflection, 0.000001)
        : baseColor / max(2.0 * (1.0 - reflection), 0.000001);
    if (mode == 109) return reflection <= 0.5
        ? min(baseColor, 2.0 * reflection)
        : max(baseColor, 2.0 * reflection - 1.0);
    if (mode == 110)
    {
        float3 vivid = reflection <= 0.5
            ? 1.0 - (1.0 - baseColor) / max(2.0 * reflection, 0.000001)
            : baseColor / max(2.0 * (1.0 - reflection), 0.000001);
        return step(0.5, vivid);
    }
    if (mode == 111) return baseColor + reflection - 2.0 * baseColor * reflection;
    return baseColor + reflection; //C#側の対応判定とずれた場合の防御用
}

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0, float4 uv1 : TEXCOORD1) : SV_Target
{
    float4 baseSample = BaseTexture.Sample(BaseSampler, uv0.xy);
    if (baseSample.a <= 0.000001)
        return float4(0, 0, 0, 0);

    float4 reflectionSample = ReflectionTexture.Sample(ReflectionSampler, uv1.xy);
    float3 baseLinear = SrgbToLinear(saturate(baseSample.rgb / baseSample.a));
    float3 reflectionLinear = reflectionSample.a > 0.000001
        ? SrgbToLinear(saturate(reflectionSample.rgb / reflectionSample.a))
        : 0.0;

    float3 blended = BlendLinear(baseLinear, reflectionLinear, blendMode);
    float3 hdr = max(lerp(baseLinear, blended, saturate(reflectionSample.a)), 0.0);
    float3 mapped = hdr / (1.0 + hdr);
    float3 outputSrgb = saturate(LinearToSrgb(mapped));
    return float4(outputSrgb * baseSample.a, baseSample.a);
}
