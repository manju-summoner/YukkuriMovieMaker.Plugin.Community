Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float inputLeft;
    float inputTop;
    float inputWidth;
    float inputHeight;
    float amplitude;
    float frequency;
    float phase;
    float edgePosition;
    float bandWidth;
    float softness;
    int mode;
    float isInverted;
};

float ComputeWave(float normalizedX)
{
    return sin(normalizedX * frequency * 6.28318530718f + phase) * amplitude;
}

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_TARGET
{
    float4 color = InputTexture.Sample(InputSampler, uv.xy);

    float safeWidth = max(inputWidth, 1e-6f);
    float safeHeight = max(inputHeight, 1e-6f);

    float nx = (posScene.x - inputLeft) / safeWidth;
    float ny = (posScene.y - inputTop) / safeHeight;

    float wave = ComputeWave(nx);
    float halfBand = (mode == 0) ? 0.0f : bandWidth * 0.5f;
    float e1 = edgePosition - halfBand + wave;
    float e2 = edgePosition + halfBand + wave;
    float eps = max(softness, 1e-6f);

    float mask;

    [branch]
    if (mode == 0)
    {
        mask = 1.0f - smoothstep(e1 - eps, e1 + eps, ny);
    }
    else if (mode == 1)
    {
        float above = smoothstep(e1 - eps, e1 + eps, ny);
        float below = 1.0f - smoothstep(e2 - eps, e2 + eps, ny);
        mask = above * below;
    }
    else
    {
        float above = smoothstep(e1 - eps, e1 + eps, ny);
        float below = 1.0f - smoothstep(e2 - eps, e2 + eps, ny);
        mask = 1.0f - above * below;
    }

    mask = lerp(mask, 1.0f - mask, isInverted);

    color.a *= mask;
    color.rgb *= mask;

    return color;
}