float SpectrumLane(float4 packed, int lane)
{
    float2 pair = lane < 2 ? packed.xy : packed.zw;
    return (lane & 1) == 0 ? pair.x : pair.y;
}

float LineCoverage(float distance, float halfWidth, float antialias)
{
    return 1.0 - smoothstep(max(halfWidth - antialias, 0.0), halfWidth + antialias, distance);
}

float4 PremultipliedTint(float4 tint, float coverage)
{
    float alpha = coverage * tint.a;
    return float4(tint.rgb * alpha, alpha);
}
