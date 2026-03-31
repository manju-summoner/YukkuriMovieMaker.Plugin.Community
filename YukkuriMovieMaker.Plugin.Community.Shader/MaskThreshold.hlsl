Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float _min : packoffset(c0.x);
    float _max : packoffset(c0.y);
    int _isInvert : packoffset(c0.z);
};

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv0.xy);
    int alpha = 1;
    if (_isInvert == 0 && (color.r < _min || color.r > _max)) alpha = 0;
    if (_isInvert == 1 && color.r > _min && color.r < _max) alpha = 0;

    return color * alpha;
}
