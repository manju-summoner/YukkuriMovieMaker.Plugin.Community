Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float amount : packoffset(c0.x);
    float density : packoffset(c0.y);
    float thickness : packoffset(c0.z);
    float shadowDepth : packoffset(c0.w);
    float flow : packoffset(c1.x);
    float outline : packoffset(c1.y);
    float toneSoftness : packoffset(c1.z);
    float grain : packoffset(c1.w);
    int seed : packoffset(c2.x);
    int style : packoffset(c2.y);
    int colorMode : packoffset(c2.z);
    int lineLayers : packoffset(c2.w);
    float4 inkColor : packoffset(c3);
    float4 paperColor : packoffset(c4);
    float4 inputBounds : packoffset(c5);
    float wobble : packoffset(c6.x);
};

static const float3 LumaWeight = float3(0.2126, 0.7152, 0.0722);
static const float GradientLow = 0.015;
static const float GradientHigh = 0.150;
static const float EdgeThreshold = 0.080;
static const float EdgeSoftness = 0.140;
static const float AlphaEdgeWeight = 1.4;
static const float PaperNoiseDepth = 0.06;
static const float WobbleAmplitude = 0.45;
static const float WobbleWidthDepth = 0.35;

uint Hash32(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return value;
}

float Hash01(int value)
{
    return Hash32(asuint(value)) * (1.0 / 4294967296.0);
}

float CellNoise(int2 cell)
{
    uint hash = Hash32(asuint(cell.x) * 0x9e3779b9u ^ Hash32(asuint(cell.y) * 0x85ebca6bu ^ asuint(seed)));
    return hash * (1.0 / 4294967296.0);
}

float ValueNoise(float2 position)
{
    float2 cell = floor(position);
    float2 fraction = position - cell;
    float2 weight = fraction * fraction * (3.0 - 2.0 * fraction);
    int2 base = int2(cell);
    float n00 = CellNoise(base);
    float n10 = CellNoise(base + int2(1, 0));
    float n01 = CellNoise(base + int2(0, 1));
    float n11 = CellNoise(base + int2(1, 1));
    return lerp(lerp(n00, n10, weight.x), lerp(n01, n11, weight.x), weight.y);
}

void GetStyle(int value, out float4 anglesDeg, out float4 thresholds, out float grainScale, out float outlineScale, out float crossBoost)
{
    if (value == 1)
    {
        anglesDeg = float4(20.0, -20.0, 70.0, -70.0);
        thresholds = float4(0.18, 0.36, 0.56, 0.76);
        grainScale = 1.0;
        outlineScale = 0.6;
        crossBoost = 0.35;
    }
    else if (value == 2)
    {
        anglesDeg = float4(-45.0, 45.0, 0.0, 90.0);
        thresholds = float4(0.30, 0.50, 0.70, 0.86);
        grainScale = 0.5;
        outlineScale = 1.4;
        crossBoost = 0.0;
    }
    else if (value == 3)
    {
        anglesDeg = float4(0.0, 90.0, 45.0, -45.0);
        thresholds = float4(0.22, 0.44, 0.66, 0.84);
        grainScale = 0.35;
        outlineScale = 1.4;
        crossBoost = 0.0;
    }
    else
    {
        anglesDeg = float4(-45.0, 45.0, 0.0, 90.0);
        thresholds = float4(0.25, 0.45, 0.65, 0.82);
        grainScale = 0.6;
        outlineScale = 1.0;
        crossBoost = 0.0;
    }
}

float4 SampleScene(float2 uv, float2 pixelStep, float2 scenePosition, float2 offset)
{
    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = max(inputBounds.zw - 0.5, minimum);
    float2 target = clamp(scenePosition + offset, minimum, maximum);
    return InputTexture.SampleLevel(InputSampler, uv + (target - scenePosition) * pixelStep, 0);
}

float2 Straighten(float4 premultiplied)
{
    float alpha = max(premultiplied.a, 1e-5);
    float luma = dot(saturate(premultiplied.rgb / alpha), LumaWeight);
    return float2(luma, saturate(premultiplied.a));
}

float2 Rotate(float2 direction, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return float2(direction.x * c - direction.y * s, direction.x * s + direction.y * c);
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = InputTexture.SampleLevel(InputSampler, uv0.xy, 0);
    if (amount <= 0.0 || source.a <= 0.0)
        return source;

    float2 scene = scenePosition.xy;
    float2 pixelStep = uv0.zw;

    float2 n00 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(-1.0, -1.0)));
    float2 n10 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(0.0, -1.0)));
    float2 n20 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(1.0, -1.0)));
    float2 n01 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(-1.0, 0.0)));
    float2 n21 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(1.0, 0.0)));
    float2 n02 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(-1.0, 1.0)));
    float2 n12 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(0.0, 1.0)));
    float2 n22 = Straighten(SampleScene(uv0.xy, pixelStep, scene, float2(1.0, 1.0)));

    float2 center = Straighten(source);
    float luma = center.x;

    const float kA = 3.0 / 16.0;
    const float kB = 10.0 / 16.0;
    float2 gradX = (n20 * kA + n21 * kB + n22 * kA) - (n00 * kA + n01 * kB + n02 * kA);
    float2 gradY = (n02 * kA + n12 * kB + n22 * kA) - (n00 * kA + n10 * kB + n20 * kA);

    float lumaGradient = length(float2(gradX.x, gradY.x));
    float2 flowTangent = lumaGradient > 1e-5 ? normalize(float2(-gradY.x, gradX.x)) : float2(1.0, 0.0);
    float flowWeight = flow * smoothstep(GradientLow, GradientHigh, lumaGradient);

    float4 anglesDeg;
    float4 thresholds;
    float grainScale;
    float outlineScale;
    float crossBoost;
    GetStyle(style, anglesDeg, thresholds, grainScale, outlineScale, crossBoost);

    float angles[4] = { radians(anglesDeg.x), radians(anglesDeg.y), radians(anglesDeg.z), radians(anglesDeg.w) };
    float thresholdArray[4] = { thresholds.x, thresholds.y, thresholds.z, thresholds.w };
    float layerOffset[4] = { 0.0, radians(90.0), radians(45.0), radians(-45.0) };

    float2 seedOffset = float2(Hash01(seed * 2 + 1), Hash01(seed * 2 + 3)) * 1024.0;
    float2 patternPosition = scene + seedOffset;
    float shade = saturate((1.0 - luma) * shadowDepth);
    float safeDensity = max(density, 1e-3);
    float wobbleFrequency = 0.35 / safeDensity;

    float hatchMax = 0.0;
    float hatchSum = 0.0;

    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        float2 fixedDir = float2(cos(angles[i]), sin(angles[i]));
        float2 flowDir = Rotate(flowTangent, layerOffset[i]);
        flowDir = dot(flowDir, fixedDir) < 0.0 ? -flowDir : flowDir;
        float2 direction = normalize(lerp(fixedDir, flowDir, flowWeight));

        float wobbleNoise = ValueNoise(patternPosition * wobbleFrequency + i * 17.0) - 0.5;
        float projection = dot(patternPosition, direction) + Hash01(seed * 4 + i) * safeDensity;
        projection += wobble * wobbleNoise * safeDensity * WobbleAmplitude;
        float halfWidth = thickness * 0.5 * (1.0 + wobble * wobbleNoise * WobbleWidthDepth);
        float coordinate = projection / safeDensity;
        float distancePx = abs(frac(coordinate) - 0.5) * safeDensity;
        float aa = max(fwidth(projection), 1.0) * 0.5;
        float lineMask = 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, distancePx);

        bool active = (lineLayers == 0) || (i < lineLayers);
        float layerWeight = active ? smoothstep(thresholdArray[i], thresholdArray[i] + toneSoftness + 1e-4, shade) : 0.0;
        float contribution = lineMask * layerWeight;

        hatchMax = max(hatchMax, contribution);
        hatchSum += contribution;
    }

    float hatch = saturate(hatchMax + crossBoost * max(hatchSum - hatchMax, 0.0));

    float effectiveGrain = saturate(grain * grainScale);
    float noise = CellNoise(int2(floor(patternPosition)));
    float inkBreak = lerp(1.0, smoothstep(0.15, 0.95, noise), effectiveGrain);
    float paperNoise = (noise - 0.5) * effectiveGrain * PaperNoiseDepth;

    float alphaGradient = length(float2(gradX.y, gradY.y));
    float edgeMagnitude = sqrt(lumaGradient * lumaGradient + alphaGradient * alphaGradient * AlphaEdgeWeight);
    float edge = smoothstep(EdgeThreshold, EdgeThreshold + EdgeSoftness, edgeMagnitude) * saturate(outline * outlineScale);

    float inkMask = saturate(max(hatch * inkBreak, edge));

    float3 sourceRgb = source.rgb / max(source.a, 1e-5);
    float3 styledRgb;
    if (colorMode == 0)
    {
        float3 paperRgb = saturate(paperColor.rgb + paperNoise);
        styledRgb = lerp(paperRgb, inkColor.rgb, inkMask);
    }
    else
    {
        styledRgb = lerp(sourceRgb, inkColor.rgb, inkMask);
    }

    float3 outRgb = lerp(sourceRgb, styledRgb, amount);
    float outAlpha = source.a;
    return float4(saturate(outRgb) * outAlpha, outAlpha);
}
