Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);
Texture2D GradientTexture : register(t1);
SamplerState GradientSampler : register(s1);

cbuffer constants : register(b0)
{
    int isHorizontal : packoffset(c0.x);
    float3 _pad : packoffset(c0.y);
};

float GetLuminance(float3 c)
{
    return dot(c, float3(0.299f, 0.587f, 0.114f));
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

    return float4(g * src.a, src.a);
}
