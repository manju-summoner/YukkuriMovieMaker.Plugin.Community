Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer constants : register(b0)
{
    float thickness : packoffset(c0.x);
};

float4 main(
    float4 pos : SV_POSITION,
    float4 posScene : SCENE_POSITION,
    float4 uv0 : TEXCOORD0) : SV_TARGET
{

    float4 current = InputTexture.Sample(InputSampler, uv0.xy);

    int range = ceil(thickness);
    float a = 1;
    [loop]
    for (int dx = -range; dx <= range; dx++)
    {
        [loop]
        for (int dy = -range; dy <= range; dy++)
        {
            float2 delta = float2(dx, dy);
            float rate = clamp(1 - (length(delta) - thickness), 0, 1);
            if (rate == 0)
                continue;

            float4 c = InputTexture.Sample(InputSampler, uv0.xy + delta * uv0.zw);
            a = min(a, c.a);
            if (a == 0)
                break;
        }
        if (a == 0)
            break;
    }

    return float4(1, 1, 1, 1) * (1 - a);
}