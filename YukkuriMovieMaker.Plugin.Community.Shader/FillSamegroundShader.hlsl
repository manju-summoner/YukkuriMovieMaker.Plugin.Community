Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float4 targetColor  : packoffset(c0);
    float  tolerance    : packoffset(c1.x);
    float  invertMask   : packoffset(c1.y);
};

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv0.xy);
    float4 diff = abs(color - targetColor);
    float  dist = max(max(diff.r, diff.g), max(diff.b, diff.a));
    float  mask = (dist <= tolerance) ? 1.0 : 0.0;

    mask = (invertMask > 0.5) ? (1.0 - mask) : mask;
    return float4(1.0, 1.0, 1.0, mask);
}
