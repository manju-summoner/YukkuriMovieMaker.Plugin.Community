Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

static const int MaxPoints = 32;
static const int MaxIntegrationSteps = 16;
static const float Epsilon = 1e-6f;

cbuffer Constants : register(b0)
{
    int pointCount : packoffset(c0.x);
    int integrationSteps : packoffset(c0.y);
    float amount : packoffset(c0.z);
    float maxDisplacement : packoffset(c0.w);
    float inputLeft : packoffset(c1.x);
    float inputTop : packoffset(c1.y);
    float inputWidth : packoffset(c1.z);
    float inputHeight : packoffset(c1.w);
    float4 points[MaxPoints * 2] : packoffset(c2);
};

float2 EvaluateField(float2 position)
{
    float2 velocity = (float2) 0;
    int count = clamp(pointCount, 0, MaxPoints);

    [loop]
    for (int index = 0; index < count; index++)
    {
        float4 parameters = points[index * 2];
        float radius = max(points[index * 2 + 1].x, 1.0f);
        //制御点はシーン座標（アイテムのローカル原点基準）。
        //入力矩形の中心を基準にすると、前段エフェクトで境界が非対称に広がったときに
        //タイムライン上のコントローラーと場の中心がズレるため使用しない
        float2 delta = position - parameters.xy;
        float denominator = max(dot(delta, delta) + radius * radius, Epsilon);
        float factor = radius / denominator;
        float2 perpendicular = float2(-delta.y, delta.x);
        velocity += factor * (parameters.z * delta + parameters.w * perpendicular);
    }

    float velocityLength = length(velocity);
    if (velocityLength > maxDisplacement && velocityLength > Epsilon)
        velocity *= maxDisplacement / velocityLength;
    return velocity;
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    int count = clamp(pointCount, 0, MaxPoints);
    int steps = clamp(integrationSteps, 1, MaxIntegrationSteps);
    if (count == 0 || amount <= 0.0f || maxDisplacement <= 0.0f)
        return InputTexture.SampleLevel(InputSampler, uv0.xy, 0);

    float stepSize = amount / steps;
    float2 source = scenePosition.xy;

    [loop]
    for (int index = 0; index < MaxIntegrationSteps; index++)
    {
        if (index >= steps)
            break;
        float2 firstVelocity = EvaluateField(source);
        float2 midpoint = source - firstVelocity * (stepSize * 0.5f);
        source -= EvaluateField(midpoint) * stepSize;
    }

    float inputRight = inputLeft + inputWidth;
    float inputBottom = inputTop + inputHeight;
    if (source.x < inputLeft || source.x >= inputRight || source.y < inputTop || source.y >= inputBottom)
        return (float4) 0;

    float2 uv = uv0.xy + (source - scenePosition.xy) * uv0.zw;
    return InputTexture.SampleLevel(InputSampler, uv, 0);
}
