Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

//1フレーム前の位置までの変位を現フレームのローカル座標系で表すアフィン変換
//delta(q) = q * M + d （M, dはCPU側でブラー量を乗算済み）
cbuffer constants : register(b0)
{
    float m11 : packoffset(c0.x);
    float m12 : packoffset(c0.y);
    float m21 : packoffset(c0.z);
    float m22 : packoffset(c0.w);
    float dx : packoffset(c1.x);
    float dy : packoffset(c1.y);
};

float2 ClampVector(float2 v, float max) {
    float len = length(v);
    return len > max ? v / len * max : v;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float2 q = posScene.xy;
    float2 delta = float2(
        q.x * m11 + q.y * m21 + dx,
        q.x * m12 + q.y * m22 + dy);
    delta = ClampVector(delta, 2000);

    float samples = floor(clamp(length(delta), 1, 256));

    float4 color = float4(0, 0, 0, 0);
    [loop]
    for (int i = 0; i < samples; i++) {
        //移動の前後に均等に広げる（センター合わせ）
        float t = (i + 0.5) / samples - 0.5;
        color += InputTexture.SampleLevel(InputSampler, uv0.xy + delta * t * uv0.zw, 0);
    }
    color /= samples;

    return color;
}
