Texture2D InputTexture0 : register(t0);
SamplerState InputSampler0 : register(s0);
Texture2D InputTexture1 : register(t1);
SamplerState InputSampler1 : register(s1);

cbuffer constants : register(b0)
{
    float strength : packoffset(c0.x);
    int mode : packoffset(c0.y);
}

static float GetLuma(float3 rgb)
{
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

static float4 Blend(float4 src, float4 dst)
{
    float outA = src.a + dst.a * (1 - src.a);
    float3 outRGB = src.rgb + dst.rgb * (1 - src.a);
    return float4(outRGB, outA);
}


float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0,
	float4 uv1 : TEXCOORD1
) : SV_Target
{
    float4 current = InputTexture0.Sample(InputSampler0, uv0.xy);
    float4 feedBack = InputTexture1.Sample(InputSampler1, uv1.xy);
    
    float4 outColor = lerp(current, feedBack, strength);

    
    if (mode == 0)// 残像を手前に表示
        return Blend(outColor, current);
    else if (mode == 1) // 残像を奥に表示
        return Blend(current, outColor);
    else //残像のみ
        return outColor;
}
