float LineCoverage(float distance, float halfWidth, float antialias)
{
    return 1.0 - smoothstep(max(halfWidth - antialias, 0.0), halfWidth + antialias, distance);
}

float4 PremultipliedTint(float4 tint, float coverage)
{
    float alpha = coverage * tint.a;
    return float4(tint.rgb * alpha, alpha);
}
