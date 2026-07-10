Texture2D HeightTexture : register(t0);
SamplerState HeightSampler : register(s0);

cbuffer constants : register(b0)
{
	float4 light : packoffset(c0);
	float surfaceScale : packoffset(c1.x);
	float reflectionConstant : packoffset(c1.y);
	float exponent : packoffset(c1.z);
	int lightMode : packoffset(c1.w);
	int reflectionMode : packoffset(c2.x);
};

float Height(float2 uv)
{
	return saturate(HeightTexture.Sample(HeightSampler, uv).a);
}

float DistributionGgx(float noH, float roughness)
{
	float alpha = roughness * roughness;
	float alpha2 = alpha * alpha;
	float denominator = noH * noH * (alpha2 - 1.0) + 1.0;
	return alpha2 / max(3.14159265 * denominator * denominator, 0.0001);
}

float GeometrySchlickGgx(float noV, float roughness)
{
	float k = (roughness + 1.0);
	k = k * k / 8.0;
	return noV / max(noV * (1.0 - k) + k, 0.0001);
}

float3 SafeNormalize(float3 value, float3 fallback)
{
	float lengthSquared = dot(value, value);
	return lengthSquared > 0.000001 ? value * rsqrt(lengthSquared) : fallback;
}

float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float h00 = Height(uv0.xy + float2(-1, -1) * uv0.zw);
	float h10 = Height(uv0.xy + float2( 0, -1) * uv0.zw);
	float h20 = Height(uv0.xy + float2( 1, -1) * uv0.zw);
	float h01 = Height(uv0.xy + float2(-1,  0) * uv0.zw);
	float h21 = Height(uv0.xy + float2( 1,  0) * uv0.zw);
	float h02 = Height(uv0.xy + float2(-1,  1) * uv0.zw);
	float h12 = Height(uv0.xy + float2( 0,  1) * uv0.zw);
	float h22 = Height(uv0.xy + float2( 1,  1) * uv0.zw);

	// Scharr勾配で斜線・曲線の方向依存を抑え、SDFから滑らかな法線を作る。
	float gradientX = ((3.0 * h20 + 10.0 * h21 + 3.0 * h22) - (3.0 * h00 + 10.0 * h01 + 3.0 * h02)) / 32.0;
	float gradientY = ((3.0 * h02 + 10.0 * h12 + 3.0 * h22) - (3.0 * h00 + 10.0 * h10 + 3.0 * h20)) / 32.0;
	float3 normal = SafeNormalize(float3(-gradientX * surfaceScale, -gradientY * surfaceScale, 1.0), float3(0, 0, 1));

	float3 lightVector = lightMode == 0 ? light.xyz : light.xyz - float3(posScene.xy, 0.0);
	float lightLengthSquared = dot(lightVector, lightVector);
	float3 lightDirection = SafeNormalize(lightVector, float3(0, 0, 1));
	float noL = lightLengthSquared > 0.000001 ? saturate(dot(normal, lightDirection)) : 0.0;
	float intensity;
	if (reflectionMode == 0)
	{
		intensity = reflectionConstant * noL;
	}
	else
	{
		float3 viewDirection = float3(0, 0, 1);
		float3 halfVector = lightDirection + viewDirection;
		float halfLengthSquared = dot(halfVector, halfVector);
		float3 halfDirection = SafeNormalize(halfVector, viewDirection);
		float noV = saturate(dot(normal, viewDirection));
		float noH = saturate(dot(normal, halfDirection));
		float voH = saturate(dot(viewDirection, halfDirection));
		float roughness = clamp(sqrt(2.0 / (max(exponent, 1.0) + 2.0)), 0.04, 1.0);
		float distribution = DistributionGgx(noH, roughness);
		float geometry = GeometrySchlickGgx(noV, roughness) * GeometrySchlickGgx(noL, roughness);
		float fresnel = 0.04 + 0.96 * pow(1.0 - voH, 5.0);
		intensity = halfLengthSquared > 0.000001
			? reflectionConstant * distribution * geometry * fresnel * noL / max(4.0 * noV * noL, 0.0001)
			: 0.0;
	}

	intensity = reflectionMode == 0
		? saturate(intensity)
		: max(intensity, 0.0) / (1.0 + max(intensity, 0.0));
	return float4(intensity, intensity, intensity, intensity);
}
