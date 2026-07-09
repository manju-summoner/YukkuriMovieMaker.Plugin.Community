Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float sensitivity : packoffset(c0.x);
    float pad0        : packoffset(c0.y);
    float pad1        : packoffset(c0.z);
    float pad2        : packoffset(c0.w);
};

float4 SampleInput(float2 uv)
{
    if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}

float LumAt(float2 uv, float2 texel, float2 offsetPx, float fallback)
{
    float4 s = SampleInput(uv + offsetPx * texel);
    if (s.a <= 1e-3f)
        return fallback;
    return dot(s.rgb / s.a, float3(0.2126f, 0.7152f, 0.0722f));
}

float ScaleResponse(float2 uv, float2 texel, float c, float s)
{
    float lxp = LumAt(uv, texel, float2(s, 0.0f), c);
    float lxm = LumAt(uv, texel, float2(-s, 0.0f), c);
    float lyp = LumAt(uv, texel, float2(0.0f, s), c);
    float lym = LumAt(uv, texel, float2(0.0f, -s), c);
    float2 odd = float2(lxp - lxm, lyp - lym) * 0.5f;
    float even = (lxp - 2.0f * c + lxm) + (lyp - 2.0f * c + lym);
    return (length(odd) + 0.5f * abs(even)) / s;
}

// 塗り分け(ステップ)と細かい模様はスケール正規化応答が細スケールに局在し、
// 形状由来の陰影(ランプ/広い窪み)はスケール間で応答が一致する。
// この差を照明・コントラスト不変な比で判定する。
float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleInput(uv0.xy);
    if (source.a <= 1e-3f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    float2 texel = uv0.zw;
    float c = dot(source.rgb / source.a, float3(0.2126f, 0.7152f, 0.0722f));

    float m1 = ScaleResponse(uv0.xy, texel, c, 1.0f);
    float m2 = ScaleResponse(uv0.xy, texel, c, 2.0f);
    float m4 = ScaleResponse(uv0.xy, texel, c, 4.0f);

    float fine = (m1 + m2) * 0.5f;
    float coarse = m4;

    float localization = saturate(2.0f * (fine - coarse) / (fine + coarse + 1e-4f));
    float energyGate = saturate((fine + coarse) * 60.0f - 1.0f);
    float albedoEdge = saturate(localization * energyGate * sensitivity);

    // 半透明の縁でバイリニア補間がアルファ重み付き平均になるよう事前乗算で保持する
    return float4((1.0f - albedoEdge) * source.a, c * source.a, albedoEdge * source.a, source.a);
}
