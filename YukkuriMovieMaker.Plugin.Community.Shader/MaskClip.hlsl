Texture2D InputTexture : register(t0);
Texture2D MaskTexture : register(t1);
SamplerState InputSampler : register(s0);
SamplerState MaskSampler : register(s1);

cbuffer constants : register(b0)
{
    int _isInvert : packoffset(c0.x);
};

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0,
            float4 uv1 : TEXCOORD1) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv0.xy);
    float4 mask = MaskTexture.Sample(MaskSampler, uv1.xy);

    float alpha = mask.x;
    if (_isInvert == 1) alpha = 1 - alpha;

    return color * alpha;
}
