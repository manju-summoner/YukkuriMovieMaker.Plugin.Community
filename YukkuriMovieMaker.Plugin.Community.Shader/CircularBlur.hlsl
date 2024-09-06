Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float angle : packoffset(c0.x);
    float x : packoffset(c0.y);
    float y : packoffset(c0.z);

    float left : packoffset(c0.w);
    float top : packoffset(c1.x);
    float right : packoffset(c1.y);
    float bottom : packoffset(c1.z);
};

float4 GetColor(float4 uv0, float4 posScene, float2 pos) {
    if (pos.x < left || right < pos.x || pos.y < top || bottom < pos.y)
        return float4(0, 0, 0, 0);

    return InputTexture.SampleLevel(InputSampler, uv0.xy + (pos - posScene.xy) * uv0.zw, 0);
}

float2 Rotate(float2 v, float a) {
    return float2(
        v.x * cos(a) - v.y * sin(a),
        v.x * sin(a) + v.y * cos(a));
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float Pi = 3.14159265f;

    float2 v = posScene.xy - float2(x,y);
    float len = length(v) * 2 * Pi * angle / 360 / 2;//‰~ŒÊ‚Ì’·‚³‚Ì1/2
    float samples = floor(max(1, len));

    float4 color = float4(0, 0, 0, 0);
    [loop]
    for (int i = 0; i < samples; i++) {
        color += GetColor(uv0, posScene, Rotate(v, angle / 180 * Pi / 2 * i / samples) + float2(x, y));
        color += GetColor(uv0, posScene, Rotate(v, -angle / 180 * Pi / 2 * i / samples) + float2(x, y));
    }
    color /= samples * 2;

    return color;
}