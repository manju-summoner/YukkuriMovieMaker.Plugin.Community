// Reel spin transition.
// The "before" and "after" images sit on one reel that spins toward the given
// angle. The reel is divided into cells of one screen each; cell k=0 holds the
// image shown at travel=0 and the reel lands on cell k=-travel. Two cell
// layouts are selectable:
//   pattern 0 (alternate): cells alternate A,B,A,B,... (ABAB)
//   pattern 1 (grouped)  : cells 0..-(laps-1) are A, the rest are B (AAABBB)
// travel = eased progress * (laps * 2 - 1), so the landing cell is always B.
// Two tiling layouts are selectable (see ReelSpin.hlsl for the derivation):
//   tile 0 (brick): cells line up along the spin direction with sheared rows,
//     tiling the plane with no gaps at any angle. The per-cell shift is a
//     lattice vector, so the transition starts exactly on A and lands exactly
//     on B at any angle.
//   tile 1 (tiled): each image tiles the plane on a fixed upright XY lattice
//     and slides rigidly. A is anchored at travel=0 and B at travel=laps*2-1,
//     so start and landing stay exact at any angle.
// The reel rect (inputLeft/Top/Width/Height) is the screen rect; the valid
// rects of the inputs may be smaller, and the transparent margins around the
// content spin as part of the screen image.
// All colors are premultiplied alpha.

Texture2D BeforeTexture    : register(t0);
Texture2D AfterTexture     : register(t1);
SamplerState BeforeSampler : register(s0);
SamplerState AfterSampler  : register(s1);

cbuffer Constants : register(b0)
{
    float travel      : packoffset(c0.x); // reel travel in extent units
    float angle       : packoffset(c0.y); // spin direction in radians (0 = right, 90deg = down)
    float blur        : packoffset(c0.z); // blur length in extent units (>=0)
    float laps        : packoffset(c0.w); // lap count (>=1)
    float pattern     : packoffset(c1.x); // 0: alternate (ABAB), 1: grouped (AAABBB)
    float inputLeft   : packoffset(c1.y);
    float inputTop    : packoffset(c1.z);
    float inputWidth  : packoffset(c1.w);
    float inputHeight : packoffset(c2.x);
    float tile        : packoffset(c2.y); // 0: brick, 1: tiled
    // valid rect (left, top, right, bottom) of each input in scene px
    // relative to (inputLeft, inputTop). The texture may be larger than the
    // image (pooled/atlased intermediate), so uv range checks are NOT enough:
    // sampling outside these rects reads undefined memory.
    float4 input0Rect : packoffset(c3);
    float4 input1Rect : packoffset(c4);
};

// wrap x into [0, size)
float Wrap(float x, float size)
{
    return x - floor(x / size) * size;
}

float4 SampleBefore(float2 target, float2 current, float4 uv0)
{
    if (target.x < input0Rect.x || target.x > input0Rect.z ||
        target.y < input0Rect.y || target.y > input0Rect.w)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    // keep bilinear filtering from bleeding into undefined texels outside the image
    float2 clamped = clamp(target, input0Rect.xy + 0.5f, input0Rect.zw - 0.5f);
    float2 uv = uv0.xy + (clamped - current) * uv0.zw;
    return BeforeTexture.SampleLevel(BeforeSampler, uv, 0);
}

float4 SampleAfter(float2 target, float2 current, float4 uv1)
{
    if (target.x < input1Rect.x || target.x > input1Rect.z ||
        target.y < input1Rect.y || target.y > input1Rect.w)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    float2 clamped = clamp(target, input1Rect.xy + 0.5f, input1Rect.zw - 0.5f);
    float2 uv = uv1.xy + (clamped - current) * uv1.zw;
    return AfterTexture.SampleLevel(AfterSampler, uv, 0);
}

// A cells: even cells (alternate) or cells 0..-(laps-1) (grouped)
bool IsBeforeCell(float k)
{
    return pattern < 0.5f
        ? k - 2.0f * floor(k * 0.5f) < 0.5f // even, works for negative k
        : k > -laps + 0.5f;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float2 pNow = float2(posScene.x - inputLeft, posScene.y - inputTop);
    if (inputWidth < 1.0f || inputHeight < 1.0f)
        return SampleAfter(pNow, pNow, uv1);

    // content moves toward this direction as travel increases (y-down screen space)
    float2 dir = float2(cos(angle), sin(angle));

    // brick mode: one cell = the distance along the direction to the next copy
    // (a lattice vector). tiled mode: one cell = the projected screen length,
    // which covers every pixel's circumferential coordinate (|a| <= extent/2)
    // so the start/landing frames stay pure at any angle
    bool yBrick = abs(dir.y) * inputWidth >= abs(dir.x) * inputHeight;
    float slope = yBrick ? inputHeight * dir.x / dir.y : inputWidth * dir.y / dir.x;
    float extent = tile < 0.5f
        ? (yBrick ? inputHeight / abs(dir.y) : inputWidth / abs(dir.x))
        : abs(inputWidth * dir.x) + abs(inputHeight * dir.y);
    float a = dot(pNow - float2(inputWidth, inputHeight) * 0.5f, dir);

    float blurPx = blur * extent;
    float samples = floor(clamp(blurPx, 1.0f, 128.0f));
    float4 color = float4(0.0f, 0.0f, 0.0f, 0.0f);
    [loop]
    for (int i = 0; i < samples; i++)
    {
        float t = (i + 0.5f) / samples - 0.5f;
        if (tile < 0.5f)
        {
            // brick: the cell index is the (sheared) row/column index
            float2 q = pNow + dir * (blurPx * t - travel * extent);
            float k;
            float2 target;
            if (yBrick)
            {
                k = floor(q.y / inputHeight);
                target = float2(Wrap(q.x - k * slope, inputWidth), q.y - k * inputHeight);
            }
            else
            {
                k = floor(q.x / inputWidth);
                target = float2(q.x - k * inputWidth, Wrap(q.y - k * slope, inputHeight));
            }
            color += IsBeforeCell(k) ? SampleBefore(target, pNow, uv0) : SampleAfter(target, pNow, uv1);
        }
        else
        {
            // tiled: the cell index comes from the position along the direction.
            // each image's tiled plane shifts rigidly; A is anchored at travel=0,
            // B at travel=laps*2-1 (the landing position)
            float k = floor((a - travel * extent + blurPx * t) / extent + 0.5f);
            bool isBefore = IsBeforeCell(k);
            float baseTravel = isBefore ? travel : travel - (laps * 2.0f - 1.0f);
            float2 q = pNow + dir * (blurPx * t - baseTravel * extent);
            float2 target = float2(Wrap(q.x, inputWidth), Wrap(q.y, inputHeight));
            color += isBefore ? SampleBefore(target, pNow, uv0) : SampleAfter(target, pNow, uv1);
        }
    }
    color /= samples;

    return color;
}
