cbuffer CBPerObject : register(b1)
{
    matrix WorldViewProjection;
    matrix World;
};

struct VSIn
{
    float3 position : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct VSOut
{
    float4 clipPosition : SV_POSITION;
    float3 worldPosition : TEXCOORD1;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

VSOut main(VSIn input)
{
    VSOut output;

    output.clipPosition = mul(float4(input.position, 1.0f), WorldViewProjection);
    output.worldPosition = mul(float4(input.position, 1.0f), World).xyz;
    output.normal = mul(float4(input.normal, 0.0f), World).xyz;
    output.uv = input.uv;
    output.color = input.color;

    return output;
}
