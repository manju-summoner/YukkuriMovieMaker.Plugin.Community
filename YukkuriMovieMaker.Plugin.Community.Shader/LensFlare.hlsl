// Procedural, physically motivated lens flare generator.
// Coordinate space: scene position in px, origin = canvas center (cropped downstream).
// Components:
//   1. Direct light PSF: gaussian diffraction core + Lorentzian^1.5 scattering
//      skirt (asymptotic ~r^-3 wings, matches measured lens PSFs).
//   2. Aperture diffraction starburst: an iris with N blades diffracts into
//      N spikes when N is even (opposite edges overlap) and 2N when odd.
//      Streak width is roughly constant in px; intensity ripples like sinc^2
//      and scales with wavelength (dispersion).
//   3. Inter-element reflection ghosts: each lens surface pair images the
//      light along the axis through the optical center. Ghosts are aperture
//      shaped (N-gon), tinted by AR coatings, brighten off-axis (Fresnel),
//      get clipped into cat-eye shapes by mechanical vignetting and show
//      transverse chromatic aberration.
//   4. Coating halo: circular rainbow ring around the optical center
//      (red outside, blue inside).
// Output is premultiplied alpha with alpha = max(rgb), so the flare
// composites near-additively over the background with normal blending.

cbuffer constants : register(b0)
{
	float2 lightPos : packoffset(c0);     // px, origin = canvas center
	float2 canvasSize : packoffset(c0.z); // px
	float intensity : packoffset(c1.x);   // 1 = 100%
	float scale : packoffset(c1.y);       // 1 = 100%
	float blades : packoffset(c1.z);      // aperture blade count (>= 3)
	float rotation : packoffset(c1.w);    // aperture rotation, rad
	float ghostCount : packoffset(c2.x);
	float ghostBrightness : packoffset(c2.y); // 1 = 100%
	float haloRadius : packoffset(c2.z);      // 1 = 100% (x half of min dimension)
	float haloBrightness : packoffset(c2.w);  // 1 = 100%
	float dispersion : packoffset(c3.x);      // 1 = physical strength
	float starLength : packoffset(c3.y);      // 1 = 100%
	float starBrightness : packoffset(c3.z);  // 1 = 100%
	float seed : packoffset(c3.w);
	float4 lightColor : packoffset(c4);       // straight rgb
};

static const float PI = 3.14159265f;
// relative diffraction scale per channel: lambda(620, 540, 465nm) / 540nm
static const float3 LAMBDA = float3(1.148f, 1.0f, 0.861f);
static const int MAX_GHOSTS = 24;

float hash(float n)
{
	return frac(sin(n) * 43758.5453f);
}

// AR coating reflections cycle through magenta/green/blue/amber hues
float3 coatingTint(float t)
{
	return 0.6f + 0.4f * cos(2.0f * PI * t + float3(0.0f, 2.1f, 4.2f));
}

// normalized distance to a regular n-gon boundary (1 = edge), circumradius = size
float polygonDistance(float2 q, float n, float rot, float size)
{
	float ang = atan2(q.y, q.x) - rot;
	float m = 2.0f * PI / n;
	float a = ang - m * floor(ang / m) - m * 0.5f;
	return length(q) * cos(a) / (cos(m * 0.5f) * size);
}

float4 main(
	float4 pos      : SV_POSITION,
	float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0
) : SV_Target
{
	float2 p = posScene.xy;
	float minDim = max(1.0f, min(canvasSize.x, canvasSize.y));
	float s = max(0.01f, scale);

	// per-channel wavelength scale, blended by dispersion amount
	float3 disp = 1.0f + (LAMBDA - 1.0f) * dispersion;

	float2 dl = p - lightPos;
	float r = length(dl);

	float lightDist = length(lightPos);
	float offAxis = lightDist / (0.5f * minDim);
	float2 axisDir = lightDist > 1e-3f ? lightPos / lightDist : float2(1.0f, 0.0f);

	float3 col = 0.0f;

	// ---- 1. direct light PSF ----
	{
		float sigma = 10.0f * s;
		float core = exp(-r * r / (2.0f * sigma * sigma));
		// red scatters wider than blue
		float3 t = (r / disp) / (3.0f * sigma);
		float3 skirt = 0.10f / pow(1.0f + t * t, 1.5f);
		col += lightColor.rgb * intensity * (2.5f * core + skirt);
	}

	// ---- 2. aperture diffraction starburst ----
	if (starBrightness > 0.0f)
	{
		float n = max(3.0f, blades);
		// even blade count: opposite edges overlap -> n spikes, odd -> 2n
		float m = (frac(n * 0.5f) < 0.25f) ? n : n * 2.0f;
		float phi = atan2(dl.y, dl.x) - rotation;
		float u = phi * m / (2.0f * PI);
		float a = (frac(u + 0.5f) - 0.5f) * (2.0f * PI / m); // rad to nearest spike
		float perp = abs(a) * r;                             // px offset from spike line
		float w = (1.2f + 0.004f * r) * s;                   // nearly constant streak width
		float spike = exp(-perp * perp / (w * w));
		float lStar = max(1.0f, 0.30f * minDim * starLength * s);
		float3 rStar = r / disp;
		float3 t = rStar / lStar * 3.0f;
		float3 radial = exp(-rStar / (2.0f * lStar)) / (1.0f + t * t);
		// sinc^2-like ripple, wavelength dependent -> spectral sparkle on spikes
		float3 ripple = 0.80f + 0.20f * cos(rStar * (2.0f * PI / (55.0f * s)));
		col += lightColor.rgb * (0.6f * intensity * starBrightness) * spike * radial * ripple;
	}

	// ---- 3. inter-element reflection ghosts ----
	int gCount = (int)clamp(ghostCount, 0.0f, (float)MAX_GHOSTS);
	if (ghostBrightness > 0.0f && gCount > 0)
	{
		// Fresnel reflectance rises for oblique incidence -> ghosts brighten off-axis
		float fresnel = 0.5f + 1.5f * pow(saturate(offAxis), 1.5f);
		float nBlade = max(3.0f, blades);
		[loop]
		for (int i = 0; i < MAX_GHOSTS; i++)
		{
			if (i >= gCount)
				break;
			float n0 = seed * 91.7f + (float)i * 13.73f;
			float h1 = hash(n0 + 1.0f);
			float h2 = hash(n0 + 2.0f);
			float h3 = hash(n0 + 3.0f);
			float h4 = hash(n0 + 4.0f);
			float h5 = hash(n0 + 5.0f);

			// each surface pair projects the light along the flare axis
			float si = lerp(-1.5f, 0.9f, h1);
			float2 center = lightPos * si;
			float size = minDim * s * (0.025f + 0.11f * h2 * h2);

			float2 q = p - center;
			float e = polygonDistance(q, nBlade, rotation, size);
			// transverse chromatic aberration scales the ghost per channel
			float3 e3 = e / (1.0f + (disp - 1.0f) * 0.35f);
			float aa = 2.5f / size;
			float3 disk = smoothstep(1.0f + aa, 1.0f - aa, e3);
			// defocus + spherical aberration concentrate light at the rim
			float ringness = smoothstep(0.35f, 0.85f, h3);
			float3 profile = disk * lerp(1.0f, 0.25f + 1.35f * smoothstep(0.2f, 1.0f, e3), ringness);

			// mechanical vignetting clips off-axis ghosts into cat-eye shapes
			float vd = length(q / size - axisDir * (0.9f * offAxis));
			float vign = 1.0f - smoothstep(1.1f, 1.8f, vd);

			// energy conservation: reflected light spreads over the ghost area
			// (softened exponent + clamp so large ghosts stay visible)
			float energyT = 0.05f * minDim * s / size;
			float energy = clamp(pow(energyT, 1.5f), 0.25f, 2.5f);
			float bright = 0.10f * ghostBrightness * intensity * fresnel * (0.4f + 0.6f * h5);

			col += lightColor.rgb * coatingTint(h4) * profile * (vign * bright * energy);
		}
	}

	// ---- 4. coating halo ring ----
	if (haloBrightness > 0.0f && haloRadius > 0.0f)
	{
		float rc = length(p);
		float baseR = 0.5f * minDim * haloRadius * s;
		// weaken the per-channel radius split below the ring width so the
		// channels overlap into a continuous rainbow instead of 3 separate rings
		float3 dispHalo = 1.0f + (LAMBDA - 1.0f) * (0.35f * dispersion);
		float3 rHalo = baseR * dispHalo; // red outside, blue inside
		float wh = max(4.0f, 0.12f * baseR);
		float3 t = (rc - rHalo) / wh;
		float3 ring = exp(-t * t);
		// slightly brighter on the far side of the optical center
		float angW = lightDist > 1e-3f && rc > 1e-3f
			? 0.55f + 0.45f * dot(p / rc, -axisDir)
			: 1.0f;
		col += lightColor.rgb * (0.25f * intensity * haloBrightness) * ring * angW;
	}

	// photographic tone mapping keeps the premultiplied output within [0,1]
	col = 1.0f - exp(-col);
	float alpha = max(col.r, max(col.g, col.b));
	return float4(col, alpha);
}
