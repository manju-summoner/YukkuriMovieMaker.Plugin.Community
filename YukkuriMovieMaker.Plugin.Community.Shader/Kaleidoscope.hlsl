Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float segments : packoffset(c1.x);
    float rotation : packoffset(c1.y);
    float zoom : packoffset(c1.z);
    float centerX : packoffset(c1.w);
    float centerY : packoffset(c2.x);
    float mirror : packoffset(c2.y);
    float amount : packoffset(c2.z);
    float pad0 : packoffset(c2.w);
};

static const float TWO_PI = 6.28318530718f;

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (amount <= 0.0)
        return source;

    float2 halfSize = (inputBounds.zw - inputBounds.xy) * 0.5;
    float2 imageCenter = (inputBounds.xy + inputBounds.zw) * 0.5;
    float2 center = imageCenter + float2(centerX, centerY) * halfSize;

    float2 delta = scenePosition.xy - center;
    float radius = length(delta);
    float segmentAngle = TWO_PI / max(segments, 1.0);

    float angle = atan2(delta.y, delta.x) - rotation;
    float folded = angle - segmentAngle * floor(angle / segmentAngle);
    if (mirror >= 0.5)
    {
        float halfSegment = segmentAngle * 0.5;
        folded = halfSegment - abs(folded - halfSegment);
    }
    folded += rotation;

    float sampledRadius = radius / max(zoom, 1e-3);
    float2 direction = float2(cos(folded), sin(folded));
    float2 samplePosition = center + direction * sampledRadius;

    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = inputBounds.zw - 0.5;
    float2 clamped = clamp(samplePosition, minimum, maximum);
    float2 uv = uv0.xy + (clamped - scenePosition.xy) * uv0.zw;

    float4 kaleido = SourceTexture.SampleLevel(SourceSampler, uv, 0);
    return lerp(source, kaleido, amount);
}
