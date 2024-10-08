Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
	float left : packoffset(c0.x);
	float top : packoffset(c0.y);
	float right : packoffset(c0.z);
	float bottom : packoffset(c0.w);

	float angle : packoffset(c1.x);
	int projection : packoffset(c1.y);
	float zoom : packoffset(c1.z);
	float colorAberration : packoffset(c1.w);
};

float convert(float t, float f, int mode)
{
	if (mode == 1)
	{
		//正射影
		return f * sin(t);
	}
	else if (mode == 2)
	{
		//立体射影
		return 2 * f * tan(t / 2);
	}
	else if (mode == 3)
	{
		//等距離射影
		return f * t;
	}
	else if (mode == 4)
	{
		//等立体角射影
		return 2 * f * sin(t / 2);
	}
	else if (mode < 0)
	{
		//逆変換
		return f * tan(t);
	}
	return 0;
}
float calcF(float d, float t, int mode)
{
	if (mode > 0)
	{
		//正変換
		return d / tan(t);
	}
	else if (mode == -1)
	{
		//正射影逆変換
		return d / sin(t);
	}
	else if (mode == -2)
	{
		//立体射影逆変換
		return d / 2 / tan(t / 2);
	}
	else if (mode == -3)
	{
		//等距離射影逆変換
		return d / t;
	}
	else if (mode == -4)
	{
		//等立体角射影逆変換
		return d / 2 / sin(t / 2);
	}

	return 0;
}
float calcT(float d, float f, int mode)
{
	if (mode == 1)
	{
		//正射影
		return asin(d / f);
	}
	else if (mode == 2)
	{
		//立体射影
		return 2 * atan(d / 2 / f);
	}
	else if (mode == 3)
	{
		//等距離射影
		return d / f;
	}
	else if (mode == 4)
	{
		//等立体角射影
		return 2 * asin(d / 2 / f);
	}
	else if (mode < 0)
	{
		//逆変換
		return atan(d / f);
	}
	return 0;
}

float4 getColor(float2 uv, float2 posScene)
{
	if(posScene.x < left || right < posScene.x 
		|| posScene.y < top || bottom < posScene.y 
		|| uv.x < 0 || 1 < uv.x
		|| uv.y < 0 || 1 < uv.y)
		return float4(0, 0, 0, 0);
	return InputTexture.Sample(InputSampler, uv);
}

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_Target
{
	const float pi = 3.141592;
	float maxAngle = angle;

	float2 lt = float2(left, top);
	float2 rb = float2(right, bottom);
	float size = max(length(lt), length(rb));
	int mode = projection;
	if (angle < 0)
	{
		maxAngle *= -1;
		mode *= -1;
		size /= sqrt(2);
	}
	if (pi / 2 <= maxAngle)
	{
		maxAngle = pi / 2 * 0.9999999;
	}
	if (maxAngle <= 0.04)
	{
		return getColor(
			uv.xy + (posScene.xy / zoom - posScene.xy) * uv.zw,
			posScene.xy / zoom
		);
	}

	float f = calcF(size, maxAngle, mode); //maxAngleの延長線上がsizeになるような半径
	float size2 = convert(maxAngle, f, mode);//魚眼変形後のsize

	float rate = size2 / size;//変化したサイズを元に戻す補正値
	if (mode < 0)
		rate = 1;
	rate /= zoom;

	float d = length(posScene.xy * rate);
	if (size2 < d && angle > 0)
		return float4(0, 0, 0, 0);

	float t = calcT(d, f, mode);
	float d2 = convert(t, f, -mode);

	float d3 = d2 - d;

	float2 posScene2 = normalize(posScene.xy) * d + normalize(posScene.xy) * d3;
	float2 uv2 = uv.xy + (posScene2 - posScene.xy) * uv.zw;
	float4 color = getColor(uv2, posScene2);
	return color;
};