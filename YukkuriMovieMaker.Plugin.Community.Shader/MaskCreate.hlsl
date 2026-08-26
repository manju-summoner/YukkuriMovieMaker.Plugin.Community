Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    int _mode : packoffset(c0.x); // 0=Hue,1=S,2=L,3=R,4=G,5=B,6=A
    float _offset : packoffset(c0.y); // 始点
    int _isInvert : packoffset(c0.z); // 反転
};

// RGB→HSL変換 (Hueは0〜1で返す)
float3 RGBtoHSL(float3 color)
{
    float r = color.r;
    float g = color.g;
    float b = color.b;

    float maxc = max(r, max(g, b));
    float minc = min(r, min(g, b));
    float delta = maxc - minc;

    float h;
    if (delta == 0.0)
        h = 0.0;
    else if (maxc == r)
        h = 60 * (g - b) / delta + (g < b ? 360 : 0);
    else if (maxc == g)
        h = 60 * (b - r) / delta + 120;
    else
        h = 60 * (r - g) / delta + 240;

    float l = maxc;

    float s = delta;

    return float3(h, s, l);
}

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv.xy);

    float value = 0.0;

    if (_mode == 0) // Hue (0〜360° → 0〜1 正規化)
    {
        float3 hsl = RGBtoHSL(color.rgb);
        value = hsl.x / 360.0; // = hsl.x
    }
    else if (_mode == 1) // Saturation
    {
        float3 hsl = RGBtoHSL(color.rgb);
        value = hsl.y;
    }
    else if (_mode == 2) // Lightness
    {
        float3 hsl = RGBtoHSL(color.rgb);
        value = hsl.z;
    }
    else if (_mode == 3) // Red
    {
        value = color.r;
    }
    else if (_mode == 4) // Green
    {
        value = color.g;
    }
    else if (_mode == 5) // Blue
    {
        value = color.b;
    }
    else if (_mode == 6) // Alpha
    {
        value = color.a;
    }

    value = saturate(value + _offset);
    if (_isInvert == 1) value = 1 - value;

    return float4(value, value, value, 1.0);
}
