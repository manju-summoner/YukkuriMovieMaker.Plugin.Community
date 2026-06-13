Texture2D BeforeTexture    : register(t0);
Texture2D AfterTexture     : register(t1);
SamplerState BeforeSampler : register(s0);
SamplerState AfterSampler  : register(s1);

cbuffer Constants : register(b0)
{
    float progress    : packoffset(c0.x);
    float maxBlockPx  : packoffset(c0.y);
    float inputLeft   : packoffset(c0.z);
    float inputTop    : packoffset(c0.w);
    float inputWidth  : packoffset(c1.x);
    float inputHeight : packoffset(c1.y);
};

float4 SampleBefore(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return BeforeTexture.SampleLevel(BeforeSampler, uv, 0);
}

float4 SampleAfter(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return AfterTexture.SampleLevel(AfterSampler, uv, 0);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float t = (progress <= 0.5f)
        ? progress * 2.0f
        : (1.0f - progress) * 2.0f;

    float blockPx = max(1.0f, maxBlockPx * t);

    float sceneX = posScene.x - inputLeft;
    float sceneY = posScene.y - inputTop;

    float snappedX = clamp(floor(sceneX / blockPx) * blockPx + blockPx * 0.5f, 0.0f, inputWidth  - 0.5f);
    float snappedY = clamp(floor(sceneY / blockPx) * blockPx + blockPx * 0.5f, 0.0f, inputHeight - 0.5f);

    float2 sampleUV0 = float2(
        uv0.x + (snappedX - sceneX) * uv0.z,
        uv0.y + (snappedY - sceneY) * uv0.w
    );

    float2 sampleUV1 = float2(
        uv1.x + (snappedX - sceneX) * uv1.z,
        uv1.y + (snappedY - sceneY) * uv1.w
    );

    float4 colorBefore = SampleBefore(sampleUV0);
    float4 colorAfter  = SampleAfter(sampleUV1);

    float blend = saturate((progress - 0.5f) * 2.0f);

    return lerp(colorBefore, colorAfter, blend);
}
