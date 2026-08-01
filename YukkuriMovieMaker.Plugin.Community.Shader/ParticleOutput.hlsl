Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

//パーティクル出力エフェクトのピクセルシェーダー。
//頂点シェーダー（ParticleOutputVertex.hlsl）が粒子クアッドをラスタライズし、
//補間済みのUV（xy）と粒子のアルファ（z）を渡してくるので、サンプリングしてアルファを乗算するだけ。
//入力は乗算済みアルファなので、全体にアルファを掛ければそのままsource-over合成になる。
float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0) : SV_Target
{
	return InputTexture.Sample(InputSampler, uv0.xy) * saturate(uv0.z);
}
