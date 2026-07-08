// Reel spin effect.
// The image scrolls along an arbitrary angle and wraps around like a slot
// machine reel. Motion blur averages taps along the spin axis.
// Two layouts are selectable:
//   tile 0 (brick): copies line up along the spin direction. Rows (or columns,
//     whichever is closer to perpendicular to the direction) are sheared so the
//     next copy sits exactly one image ahead along the direction. This tiles
//     the plane with no gaps at any angle, and the shift for one turn is a
//     lattice vector, so rotation=1 always restores the original image.
//   tile 1 (tiled): the image tiles the plane on a fixed upright XY lattice and
//     the pattern slides rigidly. Diagonal shifts do not align with the
//     lattice, so rotation=1 restores the original image only for axis-aligned
//     angles.
// All colors are premultiplied alpha.

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float rotation    : packoffset(c0.x); // turns (1 = one full lap)
    float angle       : packoffset(c0.y); // spin direction in radians (0 = right, 90deg = down)
    float blur        : packoffset(c0.z); // blur length in turns (>=0)
    float tile        : packoffset(c0.w); // 0: brick, 1: tiled
    float inputLeft   : packoffset(c1.x);
    float inputTop    : packoffset(c1.y);
    float inputWidth  : packoffset(c1.z);
    float inputHeight : packoffset(c1.w);
};

// clamp 0.5px inside to keep bilinear filtering from bleeding into undefined texels
float4 SampleClamped(float2 target, float2 current, float4 uv0)
{
    float2 clamped = clamp(target, float2(0.5f, 0.5f), float2(inputWidth, inputHeight) - 0.5f);
    float2 uv = uv0.xy + (clamped - current) * uv0.zw;
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

// wrap x into [0, size)
float Wrap(float x, float size)
{
    return x - floor(x / size) * size;
}

// map a point of the brick tiling back into the image rect.
// yBrick: rows of height inputHeight, each row sheared by slope in x.
float2 BrickTarget(float2 q, bool yBrick, float slope)
{
    if (yBrick)
    {
        float row = floor(q.y / inputHeight);
        return float2(Wrap(q.x - row * slope, inputWidth), q.y - row * inputHeight);
    }
    else
    {
        float col = floor(q.x / inputWidth);
        return float2(q.x - col * inputWidth, Wrap(q.y - col * slope, inputHeight));
    }
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float2 pNow = float2(posScene.x - inputLeft, posScene.y - inputTop);
    if (inputWidth < 1.0f || inputHeight < 1.0f)
        return SampleClamped(pNow, pNow, uv0);

    // content moves toward this direction as rotation increases (y-down screen space)
    float2 dir = float2(cos(angle), sin(angle));

    // brick axis: shear rows (y) or columns (x), whichever keeps the shear finite.
    // one turn = the distance along the direction to the next copy (a lattice
    // vector), so rotation=1 is always the identity in brick mode
    bool yBrick = abs(dir.y) * inputWidth >= abs(dir.x) * inputHeight;
    float extent = yBrick ? inputHeight / abs(dir.y) : inputWidth / abs(dir.x);
    float slope = yBrick ? inputHeight * dir.x / dir.y : inputWidth * dir.y / dir.x;

    float blurPx = blur * extent;
    float samples = floor(clamp(blurPx, 1.0f, 128.0f));
    float4 color = float4(0.0f, 0.0f, 0.0f, 0.0f);
    [loop]
    for (int i = 0; i < samples; i++)
    {
        float t = (i + 0.5f) / samples - 0.5f;
        float2 q = pNow + dir * (blurPx * t - rotation * extent);
        float2 target = tile < 0.5f
            ? BrickTarget(q, yBrick, slope)
            : float2(Wrap(q.x, inputWidth), Wrap(q.y, inputHeight));
        color += SampleClamped(target, pNow, uv0);
    }
    color /= samples;

    return color;
}
