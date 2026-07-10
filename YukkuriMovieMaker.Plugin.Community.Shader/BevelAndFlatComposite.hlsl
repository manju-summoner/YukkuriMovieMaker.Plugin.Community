Texture2D InputTexture0 : register(t0);
SamplerState InputSampler0 : register(s0);
Texture2D InputTexture1 : register(t1);
SamplerState InputSampler1 : register(s1);
Texture2D InputTexture2 : register(t2);
SamplerState InputSampler2 : register(s2);

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0,
	float4 uv1 : TEXCOORD1,
	float4 uv2 : TEXCOORD2
) : SV_Target
{
	float minHeight = 1.0;
	float maxHeight = 0.0;
	float coverageWeight = 0.0;
	for (int yi = -1; yi <= 1; yi++)
	{
		for (int xi = -1; xi <= 1; xi++)
		{
			float2 uv = uv0.xy + float2(xi, yi) * uv0.zw;
			float4 color = InputTexture0.Sample(InputSampler0, uv);
			float height = saturate(color.a);
			minHeight = min(minHeight, height);
			maxHeight = max(maxHeight, height);
			coverageWeight += 4.0 * height * (1.0 - height);
		}
	}
	
	float4 color0 = InputTexture0.Sample(InputSampler0, uv0.xy);
	float4 color1 = InputTexture1.Sample(InputSampler1, uv1.xy);
	float4 color2 = InputTexture2.Sample(InputSampler2, uv2.xy);

	// 局所的な高さ変化とサブピクセルcoverageの両方からベベル度を求め、
	// ぼかし有無を二値選択せず連続的に合成する。
	float bevelWeight = saturate(max(maxHeight - minHeight, coverageWeight / 9.0));
	return lerp(color1, color2, bevelWeight);
}
