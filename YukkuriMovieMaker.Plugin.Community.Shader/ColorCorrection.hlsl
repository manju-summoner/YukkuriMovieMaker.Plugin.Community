//http://blog.livedoor.jp/akinow/archives/52373780.html

Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float lightness : packoffset(c0.x);
	float contrast : packoffset(c0.y);

	float hue : packoffset(c0.z);
	float brightness : packoffset(c0.w);
	float saturation : packoffset(c1.x);
};

float3 ChangeLightness(float3 color, float lightness)
{
	return color + (lightness - 1);
}
float3 ChangeContrast(float3 color, float contrast)
{
	const float AvgLumR = 0.5;
	const float AvgLumG = 0.5;
	const float AvgLumB = 0.5;
	float3 AvgLumin = float3(AvgLumR, AvgLumG, AvgLumB);

	return lerp(AvgLumin, color, contrast);
}
float3 ChangeSaturation(float3 color, float saturation)
{
	const float3 LumCoeff = float3(0.2125, 0.7154, 0.0721);
	float intensityf = dot(color, LumCoeff);
	float3 intensity = float3(intensityf, intensityf, intensityf);

	return lerp(intensity, color, saturation);
}
float3 ChangeBrightness(float3 color, float brightness)
{
	return color.rgb * brightness;
}

float3 RGBToHSL(float3 color)
{
	float3 hsl; // init to 0 to avoid warnings ? (and reverse if + remove first part) 

	float fmin = min(min(color.r, color.g), color.b);    //Min. value of RGB 
	float fmax = max(max(color.r, color.g), color.b);    //Max. value of RGB 
	float delta = fmax - fmin;             //Delta RGB value

	hsl.z = (fmax + fmin) / 2.0; // Luminance

	if (delta == 0.0)   //This is a gray, no chroma... 
	{
		hsl.x = 0.0;    // Hue 
		hsl.y = 0.0;    // Saturation 
	}
	else                //Chromatic data... 
	{
		// Saturation 
		hsl.y = hsl.z < 0.5 ? delta / (fmax + fmin) : delta / (2.0 - fmax - fmin);

		float deltaR = (((fmax - color.r) / 6.0) + (delta / 2.0)) / delta;
		float deltaG = (((fmax - color.g) / 6.0) + (delta / 2.0)) / delta;
		float deltaB = (((fmax - color.b) / 6.0) + (delta / 2.0)) / delta;

		// Hue 
		hsl.x = color.r == fmax ? deltaB - deltaG :
			color.g == fmax ? (1.0 / 3.0) + deltaR - deltaB :
			color.b == fmax ? (2.0 / 3.0) + deltaG - deltaR :
			0;
		hsl.x %= 1;
		hsl.x += hsl.x < 0 ? 1 : 0;
	}

	return hsl;
}
float HueToRGB(float f1, float f2, float hue)
{
	hue %= 1;
	hue += hue < 0 ? 1 : 0;

	return (6.0 * hue) < 1.0 ? f1 + (f2 - f1) * 6.0 * hue :
		(2.0 * hue) < 1.0 ? f2 :
		(3.0 * hue) < 2.0 ? f1 + (f2 - f1) * ((2.0 / 3.0) - hue) * 6.0 :
		f1;
}
float3 HSLToRGB(float3 hsl)
{
	float f2 = hsl.z < 0.5 ? hsl.z * (1.0 + hsl.y) : (hsl.z + hsl.y) - (hsl.y * hsl.z);
	float f1 = 2.0 * hsl.z - f2;

	return hsl.y == 0.0 ?
		float3(hsl.z, hsl.z, hsl.z) :
		float3(HueToRGB(f1, f2, hsl.x + (1.0 / 3.0)),
			HueToRGB(f1, f2, hsl.x),
			HueToRGB(f1, f2, hsl.x - (1.0 / 3.0)));
}

float3 ChangeHurRotation(float3 color, float rotation) {
	float3 hsl = RGBToHSL(color);
	hsl.x += rotation;
	hsl.x %= 1;

	return HSLToRGB(hsl);
}

float3 clampRGB(float3 color) {
	return clamp(color, 0, 1);
}


float4 main(
	float4 pos : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float4 color = InputTexture.Sample(InputSampler, uv0.xy);
	color.rgb = color.a == 0 ? 0 : color.rgb / color.a;

	color.rgb = lightness == 1 ? color.rgb : clampRGB(ChangeLightness(color.rgb, lightness));
	color.rgb = contrast == 1 ? color.rgb : clampRGB(ChangeContrast(color.rgb, contrast));

	color.rgb = hue == 0 ? color.rgb : clampRGB(ChangeHurRotation(color.rgb, hue));
	color.rgb = saturation == 1 ? color.rgb : clampRGB(ChangeSaturation(color.rgb, saturation));
	color.rgb = brightness == 1 ? color.rgb : clampRGB(ChangeBrightness(color.rgb, brightness));

	color.rgb = color.rgb * color.a;
	return color;
}