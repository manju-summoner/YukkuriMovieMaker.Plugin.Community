// Page turn (page curl) transition.
// The "before" page is peeled from a corner and rolled around a cylinder of
// the given radius. The curl axis sweeps diagonally toward the opposite corner.
// Layers (top to bottom):
//   1. fold-back : part of the page already flipped over, lying flat (back face)
//   2. upper arc : back face of the page wrapping over the top of the cylinder
//   3. lower arc : front face of the page rising from the flat part
//   4. base      : flat "before" ahead of the curl / revealed "after" behind it
// All colors are premultiplied alpha. Shadows multiply rgb only.

Texture2D BeforeTexture    : register(t0);
Texture2D AfterTexture     : register(t1);
SamplerState BeforeSampler : register(s0);
SamplerState AfterSampler  : register(s1);

cbuffer Constants : register(b0)
{
    float progress      : packoffset(c0.x); // 0-1
    float radius        : packoffset(c0.y); // curl radius in px
    float shadow        : packoffset(c0.z); // 0-1
    float backLightness : packoffset(c0.w); // 0-1, whiten amount of the back face
    float origin        : packoffset(c1.x); // 0:BR 1:BL 2:TL 3:TR 4:R 5:L 6:T 7:B
    float inputLeft     : packoffset(c1.y);
    float inputTop      : packoffset(c1.z);
    float inputWidth    : packoffset(c1.w);
    float inputHeight   : packoffset(c2.x);
    // valid rect (left, top, right, bottom) of each input in scene px
    // relative to (inputLeft, inputTop). The texture may be larger than the
    // image (pooled/atlased intermediate), so uv range checks are NOT enough:
    // sampling outside these rects reads undefined memory.
    float4 input0Rect   : packoffset(c3);
    float4 input1Rect   : packoffset(c4);
};

static const float PI = 3.14159265f;

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

// signed distance to the page ("before" image) rect edge (positive inside, in px)
float InsideDistance(float2 p)
{
    return min(min(p.x - input0Rect.x, input0Rect.z - p.x),
               min(p.y - input0Rect.y, input0Rect.w - p.y));
}

// premultiplied-safe whitening for the back face of the page
float3 Whiten(float4 color)
{
    return lerp(color.rgb, color.aaa, backLightness);
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_TARGET
{
    float w = inputWidth;
    float h = inputHeight;
    float2 pNow = float2(posScene.x - inputLeft, posScene.y - inputTop);

    // starting point (corner or edge), turn direction, and the distance the
    // curl has to travel across the page
    float diag = length(float2(w, h));
    if (diag < 1e-3f)
        return SampleAfter(pNow, pNow, uv1);
    float2 c;
    float2 dir;
    float extent;
    if (origin < 0.5f)      { c = float2(w, h);        dir = float2(-w, -h) / diag; extent = diag; } // bottom right corner
    else if (origin < 1.5f) { c = float2(0, h);        dir = float2(w, -h) / diag;  extent = diag; } // bottom left corner
    else if (origin < 2.5f) { c = float2(0, 0);        dir = float2(w, h) / diag;   extent = diag; } // top left corner
    else if (origin < 3.5f) { c = float2(w, 0);        dir = float2(-w, h) / diag;  extent = diag; } // top right corner
    else if (origin < 4.5f) { c = float2(w, 0.5f * h); dir = float2(-1, 0);         extent = w; }    // right edge
    else if (origin < 5.5f) { c = float2(0, 0.5f * h); dir = float2(1, 0);          extent = w; }    // left edge
    else if (origin < 6.5f) { c = float2(0.5f * w, 0); dir = float2(0, 1);          extent = h; }    // top edge
    else                    { c = float2(0.5f * w, h); dir = float2(0, -1);         extent = h; }    // bottom edge
    if (extent < 1e-3f)
        return SampleAfter(pNow, pNow, uv1);

    float r = max(radius, 1.0f);

    // travel up to extent + 2r so that both the roll (r) and its shadow (another r)
    // have fully left the screen when progress reaches 1
    float travel = progress * (extent + 2.0f * r);
    float d = dot(pNow - c, dir) - travel; // >0: the curl has not reached here yet

    // once the roll has left the page (travel > extent + r), fade out the turned
    // layers so that the landed page does not pop in/out at progress 0/1.
    // for the transition this region is always outside the page (= the screen),
    // so the fade is invisible there
    float fade = saturate((extent + 2.0f * r - travel) / r);

    // base layer
    float4 base;
    if (d > 0.0f)
    {
        base = SampleBefore(pNow, pNow, uv0);
    }
    else
    {
        base = SampleAfter(pNow, pNow, uv1);
        // shadow cast by the roll onto the revealed area
        float sh = shadow * saturate(1.0f - (-d) / (2.0f * r));
        base.rgb *= 1.0f - sh;
    }

    float4 top = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float4 mid = float4(0.0f, 0.0f, 0.0f, 0.0f);
    if (d >= 0.0f)
    {
        // fold-back: source point is mirrored about the top of the roll
        float2 pSrc = pNow - dir * (PI * r + 2.0f * d);
        float inside = InsideDistance(pSrc);
        if (inside > 0.0f)
        {
            float4 color = SampleBefore(pSrc, pNow, uv0);
            color.rgb = Whiten(color);
            color *= saturate(inside); // 1px edge anti-aliasing
            top = color * fade;
        }
        else
        {
            // soft shadow of the fold edge falling on the flat page
            float sh = shadow * 0.75f * saturate(1.0f + inside / r) * fade;
            base.rgb *= 1.0f - sh;
        }
    }
    else if (d >= -r)
    {
        float u = -d; // 0 at the top of the roll, r at its leading edge
        float arc = r * asin(saturate(u / r));
        float nz = sqrt(saturate(1.0f - (u / r) * (u / r)));
        float shade = lerp(0.6f, 1.0f, nz);

        // fold-back sheet hanging above the roll (mirror source extended below the crest)
        float2 pFold = pNow - dir * (PI * r + 2.0f * d);
        float foldInside = InsideDistance(pFold);

        // upper arc: back face going over the top of the cylinder
        {
            float2 pSrc = pNow + dir * (u + arc - PI * r);
            float4 color = SampleBefore(pSrc, pNow, uv0);
            color.rgb = Whiten(color);
            color.rgb *= shade;
            color *= saturate(r - u); // 1px edge anti-aliasing
            top = color * fade;
        }
        // lower arc: front face rising from the flat part
        {
            float2 pSrc = pNow + dir * (u - arc);
            float4 color = SampleBefore(pSrc, pNow, uv0);
            color.rgb *= shade;
            color *= saturate(r - u);

            // shadow of the fold-back sheet hanging above
            float sh = shadow * 0.75f * saturate(1.0f + foldInside / r);
            color.rgb *= 1.0f - sh;

            mid = color * fade;
        }
    }

    return top + (1.0f - top.a) * (mid + (1.0f - mid.a) * base);
}
