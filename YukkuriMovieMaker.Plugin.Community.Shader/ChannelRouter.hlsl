Texture2D CurrentTexture : register(t0);
SamplerState CurrentSampler : register(s0);
Texture2D BranchTexture : register(t1);
SamplerState BranchSampler : register(s1);

cbuffer constants : register(b0)
{
    int srcR : packoffset(c0.x);
    int srcG : packoffset(c0.y);
    int srcB : packoffset(c0.z);
    int srcA : packoffset(c0.w);
};

static const int SRC_CURRENT_R         = 0;
static const int SRC_CURRENT_G         = 1;
static const int SRC_CURRENT_B         = 2;
static const int SRC_CURRENT_A         = 3;
static const int SRC_CURRENT_LUMINANCE = 4;
static const int SRC_BRANCH_R          = 5;
static const int SRC_BRANCH_G          = 6;
static const int SRC_BRANCH_B          = 7;
static const int SRC_BRANCH_A          = 8;
static const int SRC_BRANCH_LUMINANCE  = 9;
static const int SRC_ONE               = 10;
static const int SRC_ZERO              = 11;

float GetLuminance(float3 rgb)
{
    return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float4 UnpremultiplyAlpha(float4 c)
{
    if (c.a <= 1e-6f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);
    return float4(c.rgb / c.a, c.a);
}

float ResolveChannel(int src, float4 cur, float curLum, float4 br, float brLum)
{
    if (src == SRC_CURRENT_R)         return cur.r;
    if (src == SRC_CURRENT_G)         return cur.g;
    if (src == SRC_CURRENT_B)         return cur.b;
    if (src == SRC_CURRENT_A)         return cur.a;
    if (src == SRC_CURRENT_LUMINANCE) return curLum;
    if (src == SRC_BRANCH_R)          return br.r;
    if (src == SRC_BRANCH_G)          return br.g;
    if (src == SRC_BRANCH_B)          return br.b;
    if (src == SRC_BRANCH_A)          return br.a;
    if (src == SRC_BRANCH_LUMINANCE)  return brLum;
    if (src == SRC_ONE)               return 1.0f;
    return 0.0f;
}

float4 main(
    float4 pos      : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0      : TEXCOORD0,
    float4 uv1      : TEXCOORD1
) : SV_Target
{
    float4 curPremul = CurrentTexture.SampleLevel(CurrentSampler, uv0.xy, 0);
    float4 brPremul  = BranchTexture.SampleLevel(BranchSampler, uv1.xy, 0);

    float4 cur = UnpremultiplyAlpha(curPremul);
    float4 br  = UnpremultiplyAlpha(brPremul);

    float curLum = GetLuminance(cur.rgb);
    float brLum  = GetLuminance(br.rgb);

    float outA = ResolveChannel(srcA, cur, curLum, br, brLum);
    float outR = ResolveChannel(srcR, cur, curLum, br, brLum);
    float outG = ResolveChannel(srcG, cur, curLum, br, brLum);
    float outB = ResolveChannel(srcB, cur, curLum, br, brLum);

    float4 result;
    result.rgb = saturate(float3(outR, outG, outB)) * saturate(outA);
    result.a   = saturate(outA);
    return result;
}
