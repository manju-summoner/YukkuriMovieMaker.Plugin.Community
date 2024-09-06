Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float waveLength : packoffset(c0.x);
    float phase : packoffset(c0.y);
    float amplitude : packoffset(c0.z);
    float x : packoffset(c0.w);
    float y : packoffset(c1.x);
};

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    const float Pi = 3.141592653589f;
    float2 v = posScene.xy - float2(x,y);
    float distance = length(v);
    float2 direction = normalize(v);
    float wave = waveLength == 0 ? 0 : sin((distance / waveLength) * 2 * Pi + phase);

    float2 uv = uv0.xy + (wave * amplitude) * direction * uv0.zw;
    uv.x = clamp(uv.x, 0, 1);
    uv.y = clamp(uv.y, 0, 1);
    float4 color = InputTexture.Sample(InputSampler, uv);

    return color;
}