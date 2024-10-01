Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    int mode : packoffset(c0.x);
	float strength : packoffset(c0.y);
};

static float3x3 toLmsMat = {
    0.31394f, 0.63957f, 0.04652f,
    0.15530f, 0.75796f, 0.08673f,
    0.01772f, 0.10945f, 0.87277f
};
static float3x3 pMsMat = {
    0.0f, 1.20800f, -0.20797f,
    0.0f, 1.0f, 0.0f,
    0.0f, 0.0f, 1.0f
};
static float3x3 pSmMat = {
    0.0f, 1.22023f, -0.22020f,
    0.0f, 1.0f, 0.0f,
    0.0f, 0.0f, 1.0f
};
static float3x3 dLsMat = {
        1.0f, 0.0f, 0.0f,
    0.82781f, 0.0f, 0.17216f,
    0.0f, 0.0f, 1.0f
};
static float3x3 dSlMat = {
    1.0f, 0.0f, 0.0f,
    0.81951f, 0.0f, 0.18046f,
    0.0f, 0.0f, 1.0f
};
static float3x3 tLmMat = {
    1.0f, 0.0f, 0.0f,
    0.0f, 1.0f, 0.0f,
    -0.52543f, 1.52540f, 0.0f
};
static float3x3 tMlMat = {
    1.0f, 0.0f, 0.0f,
    0.0f, 1.0f, 0.0f,
    -0.87504f, 1.87503f, 0.0f
};
static float3x3 toRgbMat = {
    5.47213f, -4.64189f, 0.16958f,
    -1.12464f, 2.29255f, -0.16786f,
    0.02993f, -0.19325f, 1.16339f
};

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv.xy);
    float3 rgb = color.rgb / max(color.a, 0.0001);

    float3 lms = mul(toLmsMat, rgb);
	float3 lmsOrig = lms;
    if (mode == 1)
    {
        //PŒ^
        if (lms.z <= lms.y)
            lms = mul(pMsMat, lms);
        else
            lms = mul(pSmMat, lms);
    }
    else if (mode == 2)
    {
        //DŒ^
        if (lms.z <= lms.x)
            lms = mul(dLsMat, lms);
        else
            lms = mul(dSlMat, lms);
    }
    else if(mode == 3)
    {
        //TŒ^
        if (lms.y <= lms.x)
            lms = mul(tLmMat, lms);
        else
            lms = mul(tMlMat, lms);
    }
	lms = lerp(lmsOrig, lms, strength);
    rgb = mul(toRgbMat, lms);

    return float4(rgb, 1) * color.a;
}