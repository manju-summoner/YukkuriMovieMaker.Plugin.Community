cbuffer CBPerFrame : register(b0)
{
    float4 CameraPosition;
    float4 LightPosition;
    float4 LightTarget;
    float LightType;
    float LightEnabled;
};

cbuffer CBPerMaterial : register(b2)
{
    float4 BaseColor;
    float Metallic;
    float Roughness;
    float AlphaCutoff;
    float ForceOpaque;
};

Texture2D BaseColorTexture : register(t0);
Texture2D MetallicRoughnessTexture : register(t1);
SamplerState BaseColorSampler : register(s0);

struct PSIn
{
    float4 clipPosition : SV_POSITION;
    float3 worldPosition : TEXCOORD1;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD2;
    float4 color : COLOR;
    bool isFrontFace : SV_IsFrontFace;
};

static const float Pi = 3.14159265359f;
static const float Gamma = 2.2f;
static const float Epsilon = 1e-4f;
static const float MinRoughness = 0.03f;
static const float3 DielectricF0 = float3(0.04f, 0.04f, 0.04f);

static const int SpotLightType = 1;
static const int SunLightType = 2;
static const int AreaLightType = 3;

static const float3 LightColor = float3(1.0f, 1.0f, 1.0f);
static const float LightIntensity = Pi;
static const float LightReferenceDistance = 1000.0f;
static const float SpotInnerCosine = 0.86f;
static const float SpotOuterCosine = 0.90f;
static const float AreaLightRadius = 80.0f;

static const float3 SkyColor = float3(0.18f, 0.25f, 0.36f);
static const float3 HorizonColor = float3(0.28f, 0.30f, 0.33f);
static const float3 GroundColor = float3(0.12f, 0.11f, 0.10f);
static const float3 WorldUp = float3(0.0f, 1.0f, 0.0f);
static const float AmbientSpecularIntensity = 0.6f;

float3 SafeNormalize(float3 value, float3 fallback)
{
    float lengthSquared = dot(value, value);
    return lengthSquared > Epsilon * Epsilon ? value * rsqrt(lengthSquared) : fallback;
}

float DistributionGGX(float3 n, float3 h, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float nDotH = max(dot(n, h), 0.0f);
    float denominator = nDotH * nDotH * (a2 - 1.0f) + 1.0f;
    denominator = Pi * denominator * denominator;
    return a2 / max(denominator, Epsilon);
}

float GeometrySchlickGGX(float nDotV, float roughness)
{
    float r = roughness + 1.0f;
    float k = r * r / 8.0f;
    return nDotV / max(nDotV * (1.0f - k) + k, Epsilon);
}

float GeometrySmith(float3 n, float3 v, float3 l, float roughness)
{
    return GeometrySchlickGGX(max(dot(n, v), 0.0f), roughness)
         * GeometrySchlickGGX(max(dot(n, l), 0.0f), roughness);
}

float3 FresnelSchlick(float cosTheta, float3 f0)
{
    return f0 + (1.0f - f0) * pow(saturate(1.0f - cosTheta), 5.0f);
}

float3 FresnelSchlickRoughness(float cosTheta, float3 f0, float roughness)
{
    float3 f90 = max(1.0f - roughness, f0);
    return f0 + (f90 - f0) * pow(saturate(1.0f - cosTheta), 5.0f);
}

float2 EnvironmentBrdf(float nDotV, float roughness)
{
    const float4 c0 = float4(-1.0f, -0.0275f, -0.572f, 0.022f);
    const float4 c1 = float4(1.0f, 0.0425f, 1.04f, -0.04f);
    float4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28f * nDotV)) * r.x + r.y;
    return float2(-1.04f, 1.04f) * a004 + r.zw;
}

float3 SampleEnvironment(float3 direction)
{
    float upness = dot(direction, WorldUp);
    float3 upper = lerp(HorizonColor, SkyColor, saturate(upness));
    float3 lower = lerp(HorizonColor, GroundColor, saturate(-upness));
    return lerp(lower, upper, step(0.0f, upness));
}

float3 AcesToneMapping(float3 color)
{
    const float a = 2.51f;
    const float b = 0.03f;
    const float c = 2.43f;
    const float d = 0.59f;
    const float e = 0.14f;
    return saturate((color * (a * color + b)) / (color * (c * color + d) + e));
}

float3 CalculateDirectLighting(float3 albedo, float3 n, float3 v, float3 worldPosition, float metallic, float roughness, float3 f0)
{
    int type = (int) LightType;

    float3 toLight = LightPosition.xyz - worldPosition;
    float lightDistance = max(length(toLight), Epsilon);
    float3 l = toLight / lightDistance;
    float attenuation = 1.0f;

    if (type == SunLightType)
    {
        l = SafeNormalize(LightPosition.xyz, WorldUp);
    }
    else
    {
        float reference = LightReferenceDistance * LightReferenceDistance;
        attenuation = reference / (reference + lightDistance * lightDistance);
    }

    if (type == SpotLightType)
    {
        float3 spotDirection = SafeNormalize(LightTarget.xyz - LightPosition.xyz, -l);
        attenuation *= smoothstep(SpotInnerCosine, SpotOuterCosine, dot(-l, spotDirection));
    }

    float3 specularDirection = l;
    float specularRoughness = roughness;
    float specularEnergy = 1.0f;

    if (type == AreaLightType)
    {
        float3 reflection = reflect(-v, n);
        float3 centerToRay = dot(toLight, reflection) * reflection - toLight;
        float3 closestPoint = toLight + centerToRay * saturate(AreaLightRadius / max(length(centerToRay), Epsilon));
        specularDirection = SafeNormalize(closestPoint, l);

        float alpha = roughness * roughness;
        float alphaPrime = saturate(alpha + AreaLightRadius / (lightDistance * 2.0f));
        specularRoughness = sqrt(alphaPrime);
        specularEnergy = alpha / alphaPrime;
        specularEnergy *= specularEnergy;
    }

    float nDotL = max(dot(n, l), 0.0f);
    float nDotV = max(dot(n, v), Epsilon);

    float3 h = SafeNormalize(v + specularDirection, n);
    float distribution = DistributionGGX(n, h, specularRoughness);
    float geometry = GeometrySmith(n, v, specularDirection, specularRoughness);
    float3 fresnel = FresnelSchlick(max(dot(h, v), 0.0f), f0);

    float3 specular = distribution * geometry * fresnel * specularEnergy / max(4.0f * nDotV * nDotL, Epsilon);
    float3 diffuse = (1.0f - fresnel) * (1.0f - metallic) * albedo / Pi;

    return (diffuse + specular) * LightColor * LightIntensity * attenuation * nDotL;
}

float3 CalculateAmbientLighting(float3 albedo, float3 n, float3 v, float metallic, float roughness, float3 f0)
{
    float nDotV = max(dot(n, v), Epsilon);
    float3 fresnel = FresnelSchlickRoughness(nDotV, f0, roughness);
    float3 diffuseRatio = (1.0f - fresnel) * (1.0f - metallic);

    float3 irradiance = SampleEnvironment(n);
    float3 diffuse = diffuseRatio * irradiance * albedo;

    float3 reflection = reflect(-v, n);
    float3 prefiltered = lerp(SampleEnvironment(reflection), irradiance, roughness);
    float2 brdf = EnvironmentBrdf(nDotV, roughness);
    float3 specular = prefiltered * (fresnel * brdf.x + brdf.y) * AmbientSpecularIntensity;

    return diffuse + specular;
}

float4 main(PSIn input) : SV_Target
{
    float4 texSample = BaseColorTexture.Sample(BaseColorSampler, input.uv);
    float4 surface = texSample * BaseColor * input.color;
    float alpha = surface.a;

    if (AlphaCutoff > 0.0f)
    {
        clip(alpha - AlphaCutoff);
        alpha = 1.0f;
    }
    else if (ForceOpaque > 0.5f)
    {
        alpha = 1.0f;
    }

    if (LightEnabled < 0.5f)
        return float4(saturate(surface.rgb) * alpha, alpha);

    float3 albedo = pow(abs(texSample.rgb), Gamma) * saturate(BaseColor.rgb * input.color.rgb);
    float4 metallicRoughness = MetallicRoughnessTexture.Sample(BaseColorSampler, input.uv2);
    float metallic = saturate(Metallic * metallicRoughness.b);
    float roughness = clamp(Roughness * metallicRoughness.g, MinRoughness, 1.0f);

    float3 v = SafeNormalize(CameraPosition.xyz - input.worldPosition, WorldUp);
    float3 n = SafeNormalize(input.normal, v);
    n = input.isFrontFace ? n : -n;
    float3 f0 = lerp(DielectricF0, albedo, metallic);

    float3 ambient = CalculateAmbientLighting(albedo, n, v, metallic, roughness, f0);
    float3 direct = CalculateDirectLighting(albedo, n, v, input.worldPosition, metallic, roughness, f0);

    float3 color = AcesToneMapping(ambient + direct);
    color = pow(abs(color), 1.0f / Gamma);

    return float4(saturate(color) * alpha, alpha);
}
