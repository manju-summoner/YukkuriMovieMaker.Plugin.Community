Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float angle : packoffset(c0.x);
	float angle2 : packoffset(c0.y);
	float amplitude : packoffset(c0.z);
	float waveLength : packoffset(c0.w);
	float phase : packoffset(c1.x);
};

static const float pi = 3.141592653589f;

//波の進行方向へposを投影し、波長で正規化した位相[rad]を返す。
//2*piを掛けているので、投影距離がwaveLengthだけ進むとちょうど1周期になる。
float GetPosition(float2 direction, float2 pos, float waveLength)
{
	return waveLength == 0 ? 0 : 2 * pi * dot(direction, pos) / waveLength;
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv : TEXCOORD0
) : SV_TARGET
{
	float freq = 1;
	float2 direction = float2(cos(angle), sin(angle));
	float2 direction2 = float2(cos(angle + angle2), sin(angle + angle2));
	float position = GetPosition(direction, posScene.xy, waveLength);
	float2 delta = direction2 * sin(2 * pi * phase + position) * amplitude * uv.zw;
	float4 color = InputTexture.Sample(InputSampler, uv.xy + delta);
	return color;
}
