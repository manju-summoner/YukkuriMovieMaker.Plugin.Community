// Spread page turn (book-style page turn).
// The input is treated as a two-page spread whose fold (spine) is fixed at the
// center. One half page turns over the spine and lands on the opposite half
// while staying attached at it, in one of two styles:
//   curl : the page bends over a cylinder parallel to the spine
//   fold : the page is a rigid flat board hinged at the spine, rotating by
//          theta = pi * progress with optional perspective foreshortening
// The curl radius follows a bell schedule (zero at both ends of the animation)
// so the page starts perfectly flat and lands perfectly flat; without it the
// roll would persist at the spine and pop out at progress 1.
// Layers (top to bottom):
//   1. fold-back : turned part lying flat past the crest (back face)
//   2. upper arc : back face of the page wrapping over the top of the cylinder
//   3. lower arc : front face of the page rising from the flat part
//   4. base      : flat "before" ahead of the crease / revealed "after" behind it
// Back face content: backMode 0 samples the "after" image mirrored about the
// spine (the next page printed on the same sheet; at progress 1 the landed page
// matches "after" pixel for pixel), backMode 1 shows the whitened front content
// (single-image effect where no "after" exists).
// Occlusion: in the transition (backMode 0) the sheet occludes what is below
// completely — a region the page has swept over has switched to "after", so
// "before" never blends through the turned page even where the content is
// transparent (translucent inputs would otherwise show both images doubled).
// The arc layers apply the same rule on the grounds that the sheet is opaque
// paper, so the roll does not double-expose the revealed "after" either.
// The single-image effect (backMode 1) keeps the physical behavior instead:
// the sheet only exists where the front face has content.
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
    float backLightness : packoffset(c0.w); // 0-1, whiten amount of the back face (backMode 1)
    float direction     : packoffset(c1.x); // 0:right 1:left 2:bottom 3:top page turns
    float backMode      : packoffset(c1.y); // 0: mirrored "after", 1: whitened front
    float inputLeft     : packoffset(c1.z);
    float inputTop      : packoffset(c1.w);
    float inputWidth    : packoffset(c2.x);
    float inputHeight   : packoffset(c2.y);
    float style         : packoffset(c2.z); // 0: curl, 1: fold (rigid flap)
    float invDistance   : packoffset(c2.w); // 1 / camera distance in px (fold style; 0 = orthographic)
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

// premultiplied-safe whitening for the back face of the page
float3 Whiten(float4 color)
{
    return lerp(color.rgb, color.aaa, backLightness);
}

// back face of the sheet at paper arc length s (measured from the spine):
// premultiplied color and the sheet's occupancy
void BackFace(float s, float u, float2 axis, float2 pNow, float4 uv0, float4 uv1,
              out float4 color, out float occ)
{
    if (backMode < 0.5f)
    {
        float2 posBack = pNow + axis * (-s - u);
        color = SampleAfter(posBack, pNow, uv1);
        occ = 1.0f;
    }
    else
    {
        float2 posFront = pNow + axis * (s - u);
        float4 front = SampleBefore(posFront, pNow, uv0);
        color = float4(Whiten(front), front.a);
        occ = front.a;
    }
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

    float2 center = 0.5f * float2(w, h);
    float2 axis; // unit vector from the spine into the turning half
    float S;     // half extent along the axis
    if (direction < 0.5f)      { axis = float2(1, 0);  S = 0.5f * w; } // right page turns left
    else if (direction < 1.5f) { axis = float2(-1, 0); S = 0.5f * w; } // left page turns right
    else if (direction < 2.5f) { axis = float2(0, 1);  S = 0.5f * h; } // bottom page turns up
    else                       { axis = float2(0, -1); S = 0.5f * h; } // top page turns down
    if (S < 0.5f)
        return SampleAfter(pNow, pNow, uv1);

    float u = dot(pNow - center, axis);

    float4 base;
    float4 top = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float topOcc = 0.0f;

    if (style >= 0.5f)
    {
        // ---- fold: a rigid flat board hinged at the spine.
        // Screen coord of paper point (s, y): u' = s*cos(theta) / shrink,
        // v' = y / shrink with shrink = 1 - s*sin(theta)*q. q = 1/D comes from
        // the camera field of view with the same convention as the camera
        // effects (D = screenHeight/2 / tan(fov/2)); points at or behind the
        // camera plane (shrink <= 0) are rejected below. The inverse is
        // closed-form, so the board is rendered by per-pixel inverse mapping.
        float theta = PI * progress;
        float sinT = sin(theta);
        float cosT = cos(theta);
        // single precision sin(pi) is not exactly 0 and the error gets
        // amplified by strong perspective on huge inputs (the landed edge
        // falls short by up to a few px); snap the endpoints so progress 0/1
        // stay pixel exact
        if (progress <= 0.0f || progress >= 1.0f)
        {
            sinT = 0.0f;
            cosT = progress >= 1.0f ? -1.0f : 1.0f;
        }
        float q = invDistance;

        float2 perp = float2(-axis.y, axis.x);
        float v = dot(pNow - center, perp);
        float C = abs(perp.x) * w + abs(perp.y) * h; // cross extent of the page

        // the whole turning half is uncovered the moment the board lifts;
        // the board itself hides it again by occlusion
        base = (u >= 0.0f) ? SampleAfter(pNow, pNow, uv1) : SampleBefore(pNow, pNow, uv0);

        // soft shadow ahead of the board's projected free edge; its reach
        // scales with the board's lift so it vanishes at both endpoints
        float uEdge = S * cosT / max(1.0f - S * sinT * q, 0.1f);
        float range = 0.4f * S * sinT;
        if (range > 0.5f)
        {
            float dist = (cosT >= 0.0f) ? (u - uEdge) : (uEdge - u);
            if (dist >= 0.0f)
            {
                float sh = shadow * 0.75f * saturate(1.0f - dist / range);
                base.rgb *= 1.0f - sh;
            }
        }

        // rim coverage forcing at both endpoints so that progress 0/1 match
        // before/after exactly including the 1px anti-aliased page edges.
        // The front (rising) and back (descending) faces are mutually
        // exclusive in theta, so one shared rim/feather path serves both
        float rim = max(saturate((0.02f - progress) / 0.02f),
                        saturate((progress - 0.98f) / 0.02f));

        float denom = cosT + u * sinT * q;
        if (abs(denom) > 1e-4f)
        {
            float s = u / denom; // paper arc length from the spine
            float shrink = 1.0f - s * sinT * q;
            // reject points at or beyond 10x magnification toward the camera so
            // the drawn reach matches the output-rect expansion cap; the cut is
            // faded below so no hard edge appears
            if (s >= 0.0f && s <= S && shrink >= 0.1f)
            {
                float vPage = v * shrink;
                // 1px anti-aliasing at the free edge and the cross edges,
                // both measured in screen pixels
                float featherEdge = saturate((S - s) * abs(cosT) / (shrink * shrink));
                float featherCross = saturate((0.5f * C - abs(vPage)) / shrink);
                float featherNear = saturate((shrink - 0.1f) * 50.0f);
                float feather = max(min(featherEdge, min(featherCross, featherNear)), rim);
                if (feather > 0.0f)
                {
                    float4 color;
                    float occ;
                    if (cosT >= 0.0f)
                    {
                        // front face while rising
                        float2 posFront = center + axis * s + perp * vPage;
                        color = SampleBefore(posFront, pNow, uv0);
                        occ = backMode < 0.5f ? 1.0f : color.a;
                    }
                    else if (backMode < 0.5f)
                    {
                        // back face while descending (see BackFace for the
                        // occlusion rules)
                        float2 posBack = center - axis * s + perp * vPage;
                        color = SampleAfter(posBack, pNow, uv1);
                        occ = 1.0f;
                    }
                    else
                    {
                        float2 posFront = center + axis * s + perp * vPage;
                        float4 front = SampleBefore(posFront, pNow, uv0);
                        color = float4(Whiten(front), front.a);
                        occ = front.a;
                    }
                    // simple tilt lighting; sin(theta) is 0 at both endpoints
                    // so progress 0/1 stay pixel exact
                    color.rgb *= lerp(1.0f, 0.6f, sinT);
                    top = color * feather;
                    topOcc = occ * feather;
                }
            }
        }

        return top + (1.0f - topOcc) * base;
    }

    // ---- curl
    float4 mid = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float midOcc = 0.0f;

    // the radius peaks mid-turn (capped so the roll fits within the half page)
    // and returns to zero at both ends
    float r = min(radius, S / PI) * sin(PI * progress);
    // place the crease so that the fold-back's free edge sweeps linearly across
    // the spread (+S at progress 0 -> -S at progress 1); without the pi*r/2
    // lead the fold-over would lag by the length wrapped around the roll and
    // compress into the end of the animation. Once t is clamped to 0 the edge
    // motion is driven by the shrinking radius alone (intentionally
    // non-linear: the page settles onto the spine at the end)
    float t = max(S * (1.0f - progress) - 0.5f * PI * r, 0.0f);

    // base layer
    if (u >= t)
    {
        base = SampleAfter(pNow, pNow, uv1);
        // shadow cast by the roll onto the revealed area
        float sh = shadow * saturate(1.0f - (u - t) / max(2.0f * r, 1e-3f)) * saturate(r);
        base.rgb *= 1.0f - sh;
    }
    else
    {
        base = SampleBefore(pNow, pNow, uv0);
    }

    // rim coverage forcing near the end: the 1px anti-aliasing at the free
    // edge would otherwise leave a blend column with "before" on the final
    // frame; force full coverage there so progress 1 matches "after" exactly
    float wEnd = backMode < 0.5f ? saturate((progress - 0.98f) / 0.02f) : 0.0f;

    if (u <= t)
    {
        // fold-back: the sheet past the crest, lying flat on the other side
        float s = 2.0f * t + PI * r - u;
        if (s <= S)
        {
            float4 color;
            float occ;
            BackFace(s, u, axis, pNow, uv0, uv1, color, occ);
            float feather = max(saturate(S - s), wEnd); // 1px free edge anti-aliasing
            top = color * feather;
            topOcc = occ * feather;
        }
        else
        {
            // soft shadow of the approaching free edge on the flat page
            // (the sheet floats ~2r above the surface, so scale with r)
            float sh = shadow * 0.75f * saturate(1.0f - (s - S) / max(r, 1.0f)) * saturate(r);
            base.rgb *= 1.0f - sh;
        }
    }
    else if (r > 0.5f && u <= t + r)
    {
        float x = (u - t) / r;
        float nz = sqrt(saturate(1.0f - x * x));
        float shade = lerp(0.6f, 1.0f, nz);
        float silhouette = saturate(t + r - u); // 1px AA at the roll's outer edge
        float arc = r * asin(saturate(x));

        // upper arc: back face going over the top of the cylinder
        float sUp = t + PI * r - arc;
        if (sUp <= S)
        {
            float4 color;
            float occ;
            BackFace(sUp, u, axis, pNow, uv0, uv1, color, occ);
            color.rgb *= shade;
            float feather = saturate(S - sUp) * silhouette;
            top = color * feather;
            topOcc = occ * feather;
        }

        // lower arc: front face rising from the flat part
        float sLo = t + arc;
        if (sLo <= S)
        {
            float2 posFront = pNow + axis * (sLo - u);
            float4 color = SampleBefore(posFront, pNow, uv0);
            // occlusion follows the same rule as BackFace: the transition's
            // sheet occludes completely, the effect's only where the front has
            // content
            float occ = backMode < 0.5f ? 1.0f : color.a;
            color.rgb *= shade;

            // shadow of the upper arc hanging above
            float sh = shadow * 0.75f * saturate(1.0f + (S - sUp) / max(r, 1.0f));
            color.rgb *= 1.0f - sh;

            float feather = saturate(S - sLo) * silhouette;
            mid = color * feather;
            midOcc = occ * feather;
        }
    }

    return top + (1.0f - topOcc) * (mid + (1.0f - midOcc) * base);
}
