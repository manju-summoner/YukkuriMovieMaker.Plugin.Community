Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float2 center : packoffset(c0.x);
    float radius : packoffset(c0.z);
    float aspect : packoffset(c0.w);

    float softness : packoffset(c1.x);
    float blur : packoffset(c1.y);
    float lightness : packoffset(c1.z);
    float shiftRate : packoffset(c1.w);
};

float2 Rotate(float2 dv, float angle)
{
	float s = sin(angle);
	float c = cos(angle);
    float2 rdv = float2(
        dv.x * c - dv.y * s,
        dv.x * s + dv.y * c);
    return rdv;
}

static const float pi = 3.14159265358979323846;
float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_Target
{
    float2 dv = posScene.xy - center;
    dv.x *= max(0, 1 + aspect);
    dv.y *= max(0, 1 - aspect);

    float dist = length(dv);
    float rate = saturate((dist - radius) / softness);
    float blurRad = blur / 2 * rate;
    float2 dir = dv / max(dist, 1e-6);
    float2 shift = blurRad * shiftRate;

    float4 colorR = float4(0, 0, 0, 0);
    float4 colorG = float4(0, 0, 0, 0);
    float4 colorB = float4(0, 0, 0, 0);

    float arc = dist * 2 * pi;
	int maxSamples = blurRad / pi * 180 * 10;
    int samples = int(min(maxSamples, arc)) + 1;

    [loop]
    for (int i = -samples + 1; i < samples; i++)
    {
        colorR += InputTexture.Sample(InputSampler, uv0.xy + (Rotate(dv, blurRad / samples * i - shift) - dv) * uv0.zw) ;
        colorG += InputTexture.Sample(InputSampler, uv0.xy + (Rotate(dv, blurRad / samples * i ) - dv) * uv0.zw);
        colorB += InputTexture.Sample(InputSampler, uv0.xy + (Rotate(dv, blurRad / samples * i + shift) - dv) * uv0.zw);
    }
    colorR /= samples * 2 - 1;
    colorG /= samples * 2 - 1;
    colorB /= samples * 2 - 1;

    float a = max(max(colorR.a, colorG.a), colorB.a);
    float4 color = float4(colorR.r, colorG.g, colorB.b, a);

    if (lightness < 1.0)
    {
        color.rgb -= float3(1, 1, 1) * rate * (1 - lightness);
    }
    else
    {
        color.rgb += float3(1, 1, 1) * rate * (lightness - 1);
    }
    color = saturate(color);
    return color;
}