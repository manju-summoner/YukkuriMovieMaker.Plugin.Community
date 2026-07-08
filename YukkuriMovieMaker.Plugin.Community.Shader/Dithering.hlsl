Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer Constants : register(b0)
{
    float levels : packoffset(c0.x);
    float scale : packoffset(c0.y);
    float strength : packoffset(c0.z);
    int mode : packoffset(c0.w);
    float4 darkColor : packoffset(c1);
    float4 lightColor : packoffset(c2);
};

static const float3 LUMA = float3(0.299, 0.587, 0.114);

float BayerThreshold(uint2 cell)
{
    uint x = cell.x & 7u;
    uint y = cell.y & 7u;
    uint xc = x ^ y;
    uint v = 0u;
    v = (v << 1) | ((y >> 2) & 1u);
    v = (v << 1) | ((xc >> 2) & 1u);
    v = (v << 1) | ((y >> 1) & 1u);
    v = (v << 1) | ((xc >> 1) & 1u);
    v = (v << 1) | ((y >> 0) & 1u);
    v = (v << 1) | ((xc >> 0) & 1u);
    return (float(v) + 0.5) / 64.0;
}

float3 Quantize(float3 color, float threshold, float steps)
{
    float3 scaled = saturate(color) * steps;
    float3 low = floor(scaled);
    float3 fraction = scaled - low;
    return (low + step(threshold, fraction)) / steps;
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (source.a <= 0.0)
        return source;

    float2 grid = floor(scenePosition.xy / max(scale, 1.0));
    float threshold = lerp(0.5, BayerThreshold((uint2)(int2)grid), strength);
    float steps = max(levels - 1.0, 1.0);

    float3 straight = source.rgb / max(source.a, 1e-5);

    float3 result;
    if (mode == 2)
    {
        float luminance = dot(straight, LUMA);
        float quantized = Quantize(luminance.xxx, threshold, steps).x;
        result = lerp(darkColor.rgb, lightColor.rgb, quantized);
    }
    else if (mode == 1)
    {
        float luminance = dot(straight, LUMA);
        result = Quantize(luminance.xxx, threshold, steps).x;
    }
    else
    {
        result = Quantize(straight, threshold, steps);
    }

    return float4(result * source.a, source.a);
}
