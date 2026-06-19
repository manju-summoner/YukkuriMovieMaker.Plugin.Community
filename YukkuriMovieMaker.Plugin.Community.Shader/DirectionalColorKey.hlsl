Texture2D InputTexture : register(t0);
Texture2D ForegroundTexture : register(t1);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float4 clusters[4] : packoffset(c0);
    float3 backgroundLab : packoffset(c4.x);
    float noiseThreshold : packoffset(c4.w);
    float spillStrength : packoffset(c5.x);
    float edgeSoftness : packoffset(c5.y);
    float despillBias : packoffset(c5.z);
    float outputForeground : packoffset(c5.w);
    float3 backgroundChromaDir : packoffset(c6.x);
    int clusterCount : packoffset(c6.w);
};

float3 SrgbToLinear(float3 c)
{
    float3 lo = c / 12.92f;
    float3 hi = pow(max(c + 0.055f, 0.0f) / 1.055f, 2.4f);
    return (c <= 0.04045f) ? lo : hi;
}

float3 LinearToSrgb(float3 c)
{
    c = max(c, 0.0f);
    float3 lo = c * 12.92f;
    float3 hi = 1.055f * pow(c, 1.0f / 2.4f) - 0.055f;
    return (c <= 0.0031308f) ? lo : hi;
}

float3 LinearToOklab(float3 c)
{
    float l = 0.4122214708f * c.r + 0.5363325363f * c.g + 0.0514459929f * c.b;
    float m = 0.2119034982f * c.r + 0.6806995451f * c.g + 0.1073969566f * c.b;
    float s = 0.0883024619f * c.r + 0.2817188376f * c.g + 0.6299787005f * c.b;

    float l_ = pow(l, 1.0f / 3.0f);
    float m_ = pow(m, 1.0f / 3.0f);
    float s_ = pow(s, 1.0f / 3.0f);

    return float3(
        0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
        1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
        0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_
    );
}

float3 OklabToLinear(float3 lab)
{
    float l_ = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
    float m_ = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
    float s_ = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;

    float l = l_ * l_ * l_;
    float m = m_ * m_ * m_;
    float s = s_ * s_ * s_;

    return float3(
         4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
        -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
        -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s
    );
}

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1
) : SV_Target
{
    float4 src = InputTexture.Sample(InputSampler, uv0.xy);

    [branch]
    if (src.a <= 0.0f)
        return src;

    float3 straightSrgb = saturate(src.rgb / src.a);
    float3 colorLinear = SrgbToLinear(straightSrgb);
    float3 colorLab = LinearToOklab(colorLinear);

    float3 backgroundLinear = max(OklabToLinear(backgroundLab), 0.0f);

    float3 d = colorLab - backgroundLab;
    float dLen = length(d);
    float halfThreshold = noiseThreshold * 0.5f;

    [branch]
    if (dLen < halfThreshold)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    float noiseConfidence = smoothstep(halfThreshold, noiseThreshold, dLen);

    float3 backgroundShareRatio = (backgroundLinear > 1e-4f) ? (colorLinear / max(backgroundLinear, 1e-5f)) : 1e9f;
    float backgroundShare = saturate(min(backgroundShareRatio.x, min(backgroundShareRatio.y, backgroundShareRatio.z)));
    float3 foregroundResidual = colorLinear - backgroundShare * backgroundLinear;
    float chromaConfidence = smoothstep(halfThreshold, noiseThreshold, length(foregroundResidual) / max(length(colorLinear), 1e-5f));

    float alpha = 0.0f;
    float3 foregroundLinear = colorLinear;
    bool resolved = false;

    float4 foregroundSample = ForegroundTexture.Sample(InputSampler, uv1.xy);

    [branch]
    if (foregroundSample.a > 0.5f)
    {
        float3 seedSrgb = saturate(foregroundSample.rgb / max(foregroundSample.a, 1e-3f));
        float3 seedLinear = SrgbToLinear(seedSrgb);
        float3 fb = seedLinear - backgroundLinear;
        float denom = dot(fb, fb);

        [branch]
        if (denom > 1e-6f)
        {
            float seedAlpha = saturate(dot(colorLinear - backgroundLinear, fb) / denom);
            float3 residual = (colorLinear - backgroundLinear) - seedAlpha * fb;

            [branch]
            if (dot(residual, residual) <= 0.0625f * denom)
            {
                alpha = seedAlpha;
                foregroundLinear = seedLinear;
                resolved = true;
            }
        }
    }

    [branch]
    if (!resolved)
    {
        int bestCluster = 0;
        float bestProj = -1e9f;

        [loop]
        for (int c = 0; c < clusterCount; c++)
        {
            float proj = dot(d, clusters[c].xyz);
            if (proj > bestProj)
            {
                bestProj = proj;
                bestCluster = c;
            }
        }

        [branch]
        if (bestProj <= 0.0f)
            return float4(0.0f, 0.0f, 0.0f, 0.0f);

        float lambda = max(clusters[bestCluster].w, 1e-5f);
        float directionalAlpha = saturate(bestProj / lambda);

        float3 luma = float3(0.2126f, 0.7152f, 0.0722f);
        float3 backgroundChroma = backgroundLinear - dot(backgroundLinear, luma);
        float backgroundChromaLenSq = dot(backgroundChroma, backgroundChroma);

        float neutralAlpha = directionalAlpha;

        [branch]
        if (backgroundChromaLenSq > 1e-8f)
        {
            float3 colorChroma = colorLinear - dot(colorLinear, luma);
            float t = dot(colorChroma, backgroundChroma) / backgroundChromaLenSq;
            neutralAlpha = saturate(1.0f - t);
        }

        alpha = max(directionalAlpha, neutralAlpha);
        foregroundLinear = saturate((colorLinear - (1.0f - alpha) * backgroundLinear) / max(alpha, 1e-3f));
    }

    alpha = saturate((alpha - edgeSoftness) / max(1.0f - edgeSoftness, 1e-5f));

    [branch]
    if (alpha <= 0.0f)
        return float4(0.0f, 0.0f, 0.0f, 0.0f);

    [branch]
    if (outputForeground < 0.5f)
    {
        float maskAlpha = alpha * noiseConfidence * chromaConfidence * src.a;
        return float4(maskAlpha, maskAlpha, maskAlpha, maskAlpha);
    }

    [branch]
    if (spillStrength > 0.0f && length(backgroundChromaDir.yz) > 1e-5f)
    {
        float3 foregroundLab = LinearToOklab(foregroundLinear);
        float2 chromaDir = normalize(backgroundChromaDir.yz);
        float spill = dot(foregroundLab.yz, chromaDir) - despillBias;
        spill = max(0.0f, spill) * spillStrength;
        foregroundLab.yz -= chromaDir * spill;
        foregroundLinear = max(OklabToLinear(foregroundLab), 0.0f);
    }

    float3 foregroundSrgb = saturate(LinearToSrgb(foregroundLinear));
    float outAlpha = alpha * noiseConfidence * chromaConfidence * src.a;
    return float4(foregroundSrgb * outAlpha, outAlpha);
}
