Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float threshold : packoffset(c0.x);
    float gain      : packoffset(c0.y);
    float occlusion : packoffset(c0.z);
    float pad0      : packoffset(c0.w);

    float tintR     : packoffset(c1.x);
    float tintG     : packoffset(c1.y);
    float tintB     : packoffset(c1.z);
    float pad1      : packoffset(c1.w);
};

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (source.a <= 1e-3f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    float3 straight = source.rgb / source.a;
    float lum = dot(straight, float3(0.2126f, 0.7152f, 0.0722f));

    float e = saturate((lum - threshold) / max(1.0f - threshold, 1e-3f));
    e *= e;

    float3 emission = saturate(straight * e * float3(tintR, tintG, tintB));

    return float4(emission * source.a, occlusion * source.a);
}
