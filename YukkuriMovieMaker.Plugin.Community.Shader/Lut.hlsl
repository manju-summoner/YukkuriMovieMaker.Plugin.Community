Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);
Texture2D LutTexture : register(t1);
SamplerState LutSampler : register(s1);

cbuffer constants : register(b0)
{
    float LutSize : packoffset(c0.x);
    float AtlasWidth : packoffset(c0.y);
    float AtlasHeight : packoffset(c0.z);
    int InterpolationMode : packoffset(c0.w);
    float3 DomainMin : packoffset(c1.x);
    float Pad0 : packoffset(c1.w);
    float3 DomainScale : packoffset(c2.x);
    float Pad1 : packoffset(c2.w);
};

float3 SampleSlice(int ri, int gi, int sliceIndex)
{
    float u = (sliceIndex * LutSize + ri + 0.5f) / AtlasWidth;
    float v = (gi + 0.5f) / AtlasHeight;
    return LutTexture.SampleLevel(LutSampler, float2(u, v), 0).rgb;
}

float3 SampleTrilinear(float3 rgb)
{
    float lastIndex = LutSize - 1.0f;
    float3 scaled = rgb * lastIndex;
    int3 base0 = (int3) floor(scaled);
    float3 f = scaled - floor(scaled);

    int r0 = base0.x;
    int g0 = base0.y;
    int b0 = base0.z;
    int r1 = min(r0 + 1, (int) lastIndex);
    int g1 = min(g0 + 1, (int) lastIndex);
    int b1 = min(b0 + 1, (int) lastIndex);

    float3 s000 = SampleSlice(r0, g0, b0);
    float3 s100 = SampleSlice(r1, g0, b0);
    float3 s010 = SampleSlice(r0, g1, b0);
    float3 s110 = SampleSlice(r1, g1, b0);
    float3 s001 = SampleSlice(r0, g0, b1);
    float3 s101 = SampleSlice(r1, g0, b1);
    float3 s011 = SampleSlice(r0, g1, b1);
    float3 s111 = SampleSlice(r1, g1, b1);

    float3 c00 = lerp(s000, s100, f.x);
    float3 c10 = lerp(s010, s110, f.x);
    float3 c01 = lerp(s001, s101, f.x);
    float3 c11 = lerp(s011, s111, f.x);
    float3 c0 = lerp(c00, c10, f.y);
    float3 c1 = lerp(c01, c11, f.y);
    return lerp(c0, c1, f.z);
}

float3 SampleTetrahedral(float3 rgb)
{
    float lastIndex = LutSize - 1.0f;
    float3 scaled = rgb * lastIndex;
    int3 base0 = (int3) floor(scaled);
    float3 f = scaled - floor(scaled);

    int r0 = base0.x;
    int g0 = base0.y;
    int b0 = base0.z;
    int r1 = min(r0 + 1, (int) lastIndex);
    int g1 = min(g0 + 1, (int) lastIndex);
    int b1 = min(b0 + 1, (int) lastIndex);

    float3 c000 = SampleSlice(r0, g0, b0);
    float3 c111 = SampleSlice(r1, g1, b1);

    float3 result;
    if (f.r > f.g)
    {
        if (f.g > f.b)
        {
            float3 c100 = SampleSlice(r1, g0, b0);
            float3 c110 = SampleSlice(r1, g1, b0);
            result = (1.0f - f.r) * c000 + (f.r - f.g) * c100 + (f.g - f.b) * c110 + f.b * c111;
        }
        else if (f.r > f.b)
        {
            float3 c100 = SampleSlice(r1, g0, b0);
            float3 c101 = SampleSlice(r1, g0, b1);
            result = (1.0f - f.r) * c000 + (f.r - f.b) * c100 + (f.b - f.g) * c101 + f.g * c111;
        }
        else
        {
            float3 c001 = SampleSlice(r0, g0, b1);
            float3 c101 = SampleSlice(r1, g0, b1);
            result = (1.0f - f.b) * c000 + (f.b - f.r) * c001 + (f.r - f.g) * c101 + f.g * c111;
        }
    }
    else
    {
        if (f.b > f.g)
        {
            float3 c001 = SampleSlice(r0, g0, b1);
            float3 c011 = SampleSlice(r0, g1, b1);
            result = (1.0f - f.b) * c000 + (f.b - f.g) * c001 + (f.g - f.r) * c011 + f.r * c111;
        }
        else if (f.b > f.r)
        {
            float3 c010 = SampleSlice(r0, g1, b0);
            float3 c011 = SampleSlice(r0, g1, b1);
            result = (1.0f - f.g) * c000 + (f.g - f.b) * c010 + (f.b - f.r) * c011 + f.r * c111;
        }
        else
        {
            float3 c010 = SampleSlice(r0, g1, b0);
            float3 c110 = SampleSlice(r1, g1, b0);
            result = (1.0f - f.g) * c000 + (f.g - f.r) * c010 + (f.r - f.b) * c110 + f.b * c111;
        }
    }
    return result;
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

    float3 straight = src.rgb / src.a;
    float3 normalized = saturate((straight - DomainMin) * DomainScale);
    float3 mapped = (InterpolationMode == 0)
        ? SampleTetrahedral(normalized)
        : SampleTrilinear(normalized);
    mapped = saturate(mapped);
    return float4(mapped * src.a, src.a);
}
