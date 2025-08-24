Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    bool isCentering : packoffset(c0.x);
    float x : packoffset(c0.y);
    float y : packoffset(c0.z);
    float angle : packoffset(c0.w);
    
    float stretchLength : packoffset(c1.x);
    float range : packoffset(c1.y);
}; 

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv : TEXCOORD0) : SV_Target
{
    if (stretchLength + range <= 0)
        return InputTexture.Sample(InputSampler, uv.xy);
    float minClamp = isCentering ? -0.5 : 0;
    float maxClamp = isCentering ? 0.5 : 1;
    float rotatedY = -((posScene.x - x) * sin(-angle) + (posScene.y - y) * cos(-angle));
    float2 shift = clamp(rotatedY / (stretchLength + range), minClamp, maxClamp) * stretchLength * float2(sin(-angle), cos(-angle));
    float4 color = InputTexture.Sample(InputSampler, uv.xy + shift * uv.zw);
    return color;
};