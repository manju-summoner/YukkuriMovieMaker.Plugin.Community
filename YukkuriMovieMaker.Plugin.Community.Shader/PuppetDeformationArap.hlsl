//ARAPパペット変形のピクセルシェーダー。
//UVは頂点シェーダーで計算済みなので、入力をサンプリングするだけ。

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    return InputTexture.SampleLevel(InputSampler, uv0.xy, 0);
}
