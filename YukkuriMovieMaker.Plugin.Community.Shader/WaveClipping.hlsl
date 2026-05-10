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
    float randomSeed;
    float useRandom;
    float randomSpeed;
};

static const float TWO_PI = 6.28318530718f;
static const float HASH_FACTOR = 0.10306f;
static const float HASH_BIAS = 33.33f;
static const float NOISE_SEED_SCALE = 1.73205f;
static const float NOISE_PERSISTENCE = 0.55f;
static const float LACUNARITY = 2.137f;

float Hash(float x)
{
    x = frac(x * HASH_FACTOR);
    x *= x + HASH_BIAS;
    x *= x + x;
    return frac(x);
}

float CubicInterp(float a, float b, float t)
{
    float s = t * t * (3.0f - 2.0f * t);
    return lerp(a, b, s);
}

float ValueNoise(float x)
{
    float i = floor(x);
    float f = frac(x);
    return CubicInterp(Hash(i), Hash(i + 1.0f), f);
}

float FractalNoise(float x, float seed)
{
    float xs = x + seed * NOISE_SEED_SCALE;
    float v = 0.0f;
    float amp = 0.5f;
    float freq = 1.0f;
    float norm = 0.0f;

    [unroll]
    for (int i = 0; i < 5; i++)
    {
        v += ValueNoise(xs * freq + seed * (float(i) * TWO_PI + 0.5f)) * amp;
        norm += amp;
        amp *= NOISE_PERSISTENCE;
        freq *= LACUNARITY;
    }

    return (v / norm) * 2.0f - 1.0f;
}

float ComputeWave(float normalizedX)
{
    return sin(normalizedX * frequency * TWO_PI + phase) * amplitude;
}

float ComputeRandomWave(float normalizedX)
{
    return FractalNoise(normalizedX * max(frequency, 0.01f) + randomSpeed, randomSeed) * amplitude;
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

    float wave;
    [branch]
    if (useRandom != 0.0f)
        wave = ComputeRandomWave(u) * scale;
    else
        wave = ComputeWave(u) * scale;

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

    [branch]
    if (isInverted != 0.0f)
        mask = 1.0f - mask;

    color.a *= mask;
    color.rgb *= mask;

    return color;
}
