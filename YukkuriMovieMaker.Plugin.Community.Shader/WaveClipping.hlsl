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
    float rotation;
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

    float cx = posScene.x - inputLeft - safeWidth * 0.5f;
    float cy = posScene.y - inputTop - safeHeight * 0.5f;

    float cr = cos(rotation);
    float sr = sin(rotation);

    float uPx = cx * cr - cy * sr;
    float vPx = cx * sr + cy * cr;

    float edgeSpan = max(abs(safeWidth * sr) + abs(safeHeight * cr), 1e-6f);

    float u = uPx / safeWidth + 0.5f;
    float v = vPx / edgeSpan + 0.5f;

    float scale = safeHeight / edgeSpan;
    float wave = ComputeWave(u) * scale;
    float halfBand = (mode == 0) ? 0.0f : bandWidth * 0.5f * scale;
    float e1 = edgePosition - halfBand + wave;
    float e2 = edgePosition + halfBand + wave;
    float eps = max(softness * scale, 1e-6f);

    float mask;

    [branch]
    if (mode == 0)
    {
        mask = 1.0f - smoothstep(e1 - eps, e1 + eps, v);
    }
    else if (mode == 1)
    {
        float above = smoothstep(e1 - eps, e1 + eps, v);
        float below = 1.0f - smoothstep(e2 - eps, e2 + eps, v);
        mask = above * below;
    }
    else
    {
        float above = smoothstep(e1 - eps, e1 + eps, v);
        float below = 1.0f - smoothstep(e2 - eps, e2 + eps, v);
        mask = 1.0f - above * below;
    }

    mask = lerp(mask, 1.0f - mask, isInverted);

    color.a *= mask;
    color.rgb *= mask;

    return color;
}
