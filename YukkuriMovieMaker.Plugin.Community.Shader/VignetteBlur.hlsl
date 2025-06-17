Texture2D    InputTexture0 : register(t0);
Texture2D    InputTexture1 : register(t1);
Texture2D    InputTexture2 : register(t2);
Texture2D    InputTexture3 : register(t3);
Texture2D    InputTexture4 : register(t4);
Texture2D    InputTexture5 : register(t5);
Texture2D    InputTexture6 : register(t6);
Texture2D    InputTexture7 : register(t7);
Texture2D    InputTexture8 : register(t8);
Texture2D    InputTexture9 : register(t9);
Texture2D    InputTexture10 : register(t10);
Texture2D    InputTexture11 : register(t11);
SamplerState InputSampler0 : register(s0);
SamplerState InputSampler1 : register(s1);
SamplerState InputSampler2 : register(s2);
SamplerState InputSampler3 : register(s3);
SamplerState InputSampler4 : register(s4);
SamplerState InputSampler5 : register(s5);
SamplerState InputSampler6 : register(s6);
SamplerState InputSampler7 : register(s7);
SamplerState InputSampler8 : register(s8);
SamplerState InputSampler9 : register(s9);
SamplerState InputSampler10 : register(s10);
SamplerState InputSampler11 : register(s11);

cbuffer RadialCB : register(b0)
{
    float2 center : packoffset(c0.x);
    float radius : packoffset(c0.z);
	float aspect : packoffset(c0.w);

    float softness : packoffset(c1.x);
    float blur : packoffset(c1.y);
	float lightness : packoffset(c1.z);
	float shiftRate : packoffset(c1.w);
};

float4 GetColor(int index, float4 uv0, float4 uv1, float4 uv2, float4 uv3, float4 uv4, float4 uv5, float4 uv6, float4 uv7, float4 uv8, float4 uv9, float4 uv10, float4 uv11, float2 shift)
{
    switch (index)
    {
        case 0: return InputTexture0.Sample(InputSampler0, uv0.xy + shift * uv0.zw);
        case 1: return InputTexture1.Sample(InputSampler1, uv1.xy + shift * uv1.zw);
        case 2: return InputTexture2.Sample(InputSampler2, uv2.xy + shift * uv2.zw);
        case 3: return InputTexture3.Sample(InputSampler3, uv3.xy + shift * uv3.zw);
        case 4: return InputTexture4.Sample(InputSampler4, uv4.xy + shift * uv4.zw);
        case 5: return InputTexture5.Sample(InputSampler5, uv5.xy + shift * uv5.zw);
        case 6: return InputTexture6.Sample(InputSampler6, uv6.xy + shift * uv6.zw);
        case 7: return InputTexture7.Sample(InputSampler7, uv7.xy + shift * uv7.zw);
        case 8: return InputTexture8.Sample(InputSampler8, uv8.xy + shift * uv8.zw);
        case 9: return InputTexture9.Sample(InputSampler9, uv9.xy + shift * uv9.zw);
        case 10: return InputTexture10.Sample(InputSampler10, uv10.xy + shift * uv10.zw);
        case 11: return InputTexture11.Sample(InputSampler11, uv11.xy + shift * uv11.zw);
        default: return InputTexture11.Sample(InputSampler11, uv11.xy + shift * uv11.zw);
    }
}

float4 GetShiftColor(int index, float4 uv0, float4 uv1, float4 uv2, float4 uv3, float4 uv4, float4 uv5, float4 uv6, float4 uv7, float4 uv8, float4 uv9, float4 uv10, float4 uv11, float2 shift)
{
    float4 colorR = GetColor(index, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, -shift);
    float4 colorG = GetColor(index, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, float2(0, 0));
    float4 colorB = GetColor(index, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, shift);
	float a = max(max(colorR.a, colorG.a), colorB.a);
    float4 color = float4(colorR.r, colorG.g, colorB.b, a);
    return color;
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1,
    float4 uv2 : TEXCOORD2,
    float4 uv3 : TEXCOORD3,
    float4 uv4 : TEXCOORD4,
    float4 uv5 : TEXCOORD5,
    float4 uv6 : TEXCOORD6,
    float4 uv7 : TEXCOORD7,
    float4 uv8 : TEXCOORD8,
    float4 uv9 : TEXCOORD9,
    float4 uv10 : TEXCOORD10,
    float4 uv11 : TEXCOORD11) : SV_Target
{
    float2 dv = posScene.xy - center;
    dv.x *= max(0, 1 + aspect);
    dv.y *= max(0, 1 - aspect);

    float dist = length(dv);
    float rate = saturate((dist - radius) / softness);

    float blurPx = blur * rate;
    float lod = max(0.0, log2(blurPx)+1);

    float  lod0 = floor(lod);
    float  lod1 = lod0 + 1.0;
    float  w = saturate(lod - lod0);

    float2 shift = dv / max(dist, 1e-6) * blurPx * shiftRate;

    float4 color0 = GetShiftColor(lod0, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, shift);
    float4 color1 = GetShiftColor(lod1, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, shift);
    float4 result = lerp(color0, color1, w);
    
    if (lightness < 1.0)
    {
		result.rgb -= float3(1,1,1) * rate * (1 - lightness);
    }
    else
    {
		result.rgb += float3(1,1,1) * rate * (lightness - 1);
    }
	result = saturate(result);
    return result;
}