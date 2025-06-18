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
Texture2D    InputTexture12 : register(t12);
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
SamplerState InputSampler12 : register(s12);

cbuffer constants : register(b0)
{
    float blur : packoffset(c0.x);
};

float4 GetColor(int index, float4 uv0, float4 uv1, float4 uv2, float4 uv3, float4 uv4, float4 uv5, float4 uv6, float4 uv7, float4 uv8, float4 uv9, float4 uv10, float4 uv11, float4 uv12)
{
    switch (index)
    {
    case 0: return InputTexture0.Sample(InputSampler0, uv0.xy);
    case 1: return InputTexture1.Sample(InputSampler1, uv1.xy);
    case 2: return InputTexture2.Sample(InputSampler2, uv2.xy);
    case 3: return InputTexture3.Sample(InputSampler3, uv3.xy);
    case 4: return InputTexture4.Sample(InputSampler4, uv4.xy);
    case 5: return InputTexture5.Sample(InputSampler5, uv5.xy);
    case 6: return InputTexture6.Sample(InputSampler6, uv6.xy);
    case 7: return InputTexture7.Sample(InputSampler7, uv7.xy);
    case 8: return InputTexture8.Sample(InputSampler8, uv8.xy);
    case 9: return InputTexture9.Sample(InputSampler9, uv9.xy);
    case 10: return InputTexture10.Sample(InputSampler10, uv10.xy);
    case 11: return InputTexture11.Sample(InputSampler11, uv11.xy);
    case 12: return InputTexture12.Sample(InputSampler11, uv12.xy);
    default: return InputTexture12.Sample(InputSampler11, uv12.xy);
    }
}

float GetLuminance(float4 color)
{
    return dot(color.rgb, float3(0.299, 0.587, 0.114));
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
    float4 uv11 : TEXCOORD11,
    float4 uv12 : TEXCOORD12) : SV_Target
{
    float4 mapColor = InputTexture0.Sample(InputSampler0, uv0.xy);
    float lightness = GetLuminance(mapColor);

    float blurPx = blur * lightness;
    float lod = max(0.0, log2(blurPx) + 1) + 1;

    float  lod0 = floor(lod);
    float  lod1 = lod0 + 1.0;
    float  w = saturate(lod - lod0);

    float4 color0 = GetColor(lod0, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, uv12);
    float4 color1 = GetColor(lod1, uv0, uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, uv12);
    float4 result = lerp(color0, color1, w);

    result = saturate(result);
    return result;
}