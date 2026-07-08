Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float amount : packoffset(c1.x);
    float cellSize : packoffset(c1.y);
    float irregularity : packoffset(c1.z);
    float relief : packoffset(c1.w);
    float rotation : packoffset(c2.x);
    float evolution : packoffset(c2.y);
    float refraction : packoffset(c2.z);
    float refractiveIndex : packoffset(c2.w);
    float dispersion : packoffset(c3.x);
    float reflection : packoffset(c3.y);
    float glint : packoffset(c3.z);
    float borderWidth : packoffset(c3.w);
    float lightAngle : packoffset(c4.x);
    float lightElevation : packoffset(c4.y);
    int seed : packoffset(c4.z);
};

static const float Sqrt3 = 1.7320508075688772;
static const float TwoPi = 6.283185307179586;
static const float WavelengthF = 486.1;
static const float WavelengthD = 587.6;
static const float WavelengthC = 656.3;

uint Hash32(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return value;
}

float CellHash(int2 cell, uint channel)
{
    uint value = asuint(cell.x) * 0x9e3779b9u;
    value ^= asuint(cell.y) * 0x85ebca6bu;
    value ^= asuint(seed) * 0xc2b2ae35u;
    value ^= channel * 0x27d4eb2fu;
    return Hash32(value) / 4294967295.0;
}

float2 Rotate(float2 coordinate, float angle)
{
    float cosine = cos(angle);
    float sine = sin(angle);
    return float2(coordinate.x * cosine - coordinate.y * sine, coordinate.x * sine + coordinate.y * cosine);
}

float2 SitePoint(int2 cell)
{
    float2 lattice = float2(cell.x + 0.5 * cell.y, 0.5 * Sqrt3 * cell.y) * cellSize;
    float radius = 0.45 * cellSize * irregularity * CellHash(cell, 0u);
    float angle = TwoPi * CellHash(cell, 1u);
    return lattice + radius * float2(cos(angle), sin(angle));
}

float3 FacetNormal(int2 cell)
{
    float azimuth = TwoPi * CellHash(cell, 2u) + evolution * (0.35 + 0.65 * CellHash(cell, 3u));
    float breathing = 0.6 + 0.4 * sin(evolution + TwoPi * CellHash(cell, 4u));
    float tilt = relief * 0.5 * breathing * (0.4 + 0.6 * CellHash(cell, 5u));
    float sine = sin(tilt);
    return float3(sine * cos(azimuth), sine * sin(azimuth), cos(tilt));
}

float CauchyIndex(float wavelength)
{
    float deltaFC = 0.04 * dispersion;
    float denominator = 1.0 / (WavelengthF * WavelengthF) - 1.0 / (WavelengthC * WavelengthC);
    float b = deltaFC / denominator;
    float a = refractiveIndex - b / (WavelengthD * WavelengthD);
    return a + b / (wavelength * wavelength);
}

float2 RefractionOffset(float3 normal, float index)
{
    float3 direction = refract(float3(0.0, 0.0, -1.0), normal, 1.0 / max(index, 1.0001));
    return direction.xy / max(abs(direction.z), 1e-4) * refraction;
}

float4 SampleAt(float2 uv, float2 pixelStep, float2 scenePosition, float2 offset)
{
    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = inputBounds.zw - 0.5;
    float2 target = clamp(scenePosition + offset, minimum, maximum);
    return SourceTexture.SampleLevel(SourceSampler, uv + (target - scenePosition) * pixelStep, 0);
}

float3 StraightColor(float4 sample, float3 fallback)
{
    if (sample.a <= 1e-5)
        return fallback;
    return sample.rgb / sample.a;
}

float Schlick(float index, float cosine)
{
    float f0 = (index - 1.0) / (index + 1.0);
    f0 *= f0;
    return f0 + (1.0 - f0) * pow(1.0 - saturate(cosine), 5.0);
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (amount <= 0.0 || source.a <= 0.0)
        return source;

    float2 center = 0.5 * (inputBounds.xy + inputBounds.zw);
    float rotationRadians = radians(rotation);
    float2 local = Rotate(scenePosition.xy - center, -rotationRadians);
    float latticeV = 2.0 * local.y / (Sqrt3 * cellSize);
    float latticeU = local.x / cellSize - 0.5 * latticeV;
    int2 baseCell = int2(floor(latticeU + 0.5), floor(latticeV + 0.5));

    int2 bestCell = baseCell;
    float2 bestPoint = SitePoint(baseCell);
    float bestDistance = dot(local - bestPoint, local - bestPoint);

    [loop]
    for (int du = -2; du <= 2; du++)
    {
        [loop]
        for (int dv = -2; dv <= 2; dv++)
        {
            int2 cell = baseCell + int2(du, dv);
            float2 site = SitePoint(cell);
            float2 delta = local - site;
            float distance = dot(delta, delta);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = cell;
                bestPoint = site;
            }
        }
    }

    float edgeDistance = 1e8;

    [loop]
    for (int eu = -2; eu <= 2; eu++)
    {
        [loop]
        for (int ev = -2; ev <= 2; ev++)
        {
            int2 cell = bestCell + int2(eu, ev);
            if (all(cell == bestCell))
                continue;
            float2 site = SitePoint(cell);
            float2 toSite = site - bestPoint;
            float lengthToSite = length(toSite);
            if (lengthToSite < 1e-4)
                continue;
            float distance = dot(local - 0.5 * (bestPoint + site), toSite / lengthToSite);
            edgeDistance = min(edgeDistance, abs(distance));
        }
    }

    float3 normal = FacetNormal(bestCell);
    normal.xy = Rotate(normal.xy, rotationRadians);

    float indexR = CauchyIndex(610.0);
    float indexG = CauchyIndex(550.0);
    float indexB = CauchyIndex(460.0);
    float3 sourceStraight = source.rgb / max(source.a, 1e-5);
    float4 sampleR = SampleAt(uv0.xy, uv0.zw, scenePosition.xy, RefractionOffset(normal, indexR));
    float4 sampleG = SampleAt(uv0.xy, uv0.zw, scenePosition.xy, RefractionOffset(normal, indexG));
    float4 sampleB = SampleAt(uv0.xy, uv0.zw, scenePosition.xy, RefractionOffset(normal, indexB));
    float3 refracted = float3(
        StraightColor(sampleR, sourceStraight).r,
        StraightColor(sampleG, sourceStraight).g,
        StraightColor(sampleB, sourceStraight).b);

    float angleRadians = radians(lightAngle);
    float elevationRadians = radians(lightElevation);
    float cosineElevation = cos(elevationRadians);
    float3 light = normalize(float3(cos(angleRadians) * cosineElevation, sin(angleRadians) * cosineElevation, sin(elevationRadians)));
    float3 view = float3(0.0, 0.0, 1.0);
    float diffuse = 0.78 + 0.22 * saturate(dot(normal, light));
    float fresnel = Schlick(indexG, dot(normal, view));
    float mirrored = saturate(dot(reflect(-light, normal), view));
    float specular = pow(mirrored, 48.0);
    float flicker = 0.5 + 0.5 * sin(evolution * 1.7 + TwoPi * CellHash(bestCell, 6u));
    float sparkle = glint * pow(mirrored, 160.0) * (0.35 + 0.65 * flicker);

    float antialias = max(fwidth(edgeDistance), 0.5);
    float border = borderWidth <= 0.0 ? 0.0 : 1.0 - smoothstep(borderWidth, borderWidth + antialias, edgeDistance);

    float reflectance = reflection * (fresnel + specular * 0.75 + border * 0.35) + sparkle;
    float3 glass = refracted * diffuse * (1.0 - saturate(fresnel * reflection * 0.35));
    glass += reflectance.xxx;
    glass = saturate(glass);

    float4 faceted = float4(glass * source.a, source.a);
    return lerp(source, faceted, amount);
}
