#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius 4
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableCount 81
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableStride 9
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileCount 256
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileSize 16
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionFieldConstants__ValidLengthSquaredThreshold 0.25

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    float sigmaColorSq;
    int width;
    int height;
}

RWStructuredBuffer<float> sourceDirections : register(u0);

RWStructuredBuffer<float> colorLab : register(u1);

RWStructuredBuffer<float> targetDirections : register(u2);

groupshared float directionTile [768];

groupshared float colorTile [768];

groupshared float spaceTable [81];

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID, uint3 GroupIds : SV_GroupThreadID, uint __GroupIds__get_Index : SV_GroupIndex)
{
    int x = ThreadIds.x;
    int y = ThreadIds.y;
    float twoSigmaSpaceSq = 2.0 * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
    for (int slot = __GroupIds__get_Index; slot < __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableCount; slot += __GroupSize__get_X * __GroupSize__get_Y * __GroupSize__get_Z)
    {
        int ty = slot / __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableStride;
        int tx = slot - ty * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableStride;
        int offX = tx - __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
        int offY = ty - __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
        spaceTable[slot] = exp(-(offX * offX + offY * offY) / max(twoSigmaSpaceSq, 1E-06));
    }

    int originX = x - GroupIds.x - __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
    int originY = y - GroupIds.y - __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
    for (int slot = __GroupIds__get_Index; slot < __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileCount; slot += __GroupSize__get_X * __GroupSize__get_Y * __GroupSize__get_Z)
    {
        int localY = slot / __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileSize;
        int localX = slot - localY * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileSize;
        int sampleX = originX + localX;
        int sampleY = originY + localY;
        int tileTriple = slot * 3;
        if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
        {
            directionTile[tileTriple + 0] = 0.0;
            directionTile[tileTriple + 1] = 0.0;
            directionTile[tileTriple + 2] = 0.0;
            colorTile[tileTriple + 0] = 0.0;
            colorTile[tileTriple + 1] = 0.0;
            colorTile[tileTriple + 2] = 0.0;
            continue;
        }

        int sampleTriple = (sampleY * width + sampleX) * 3;
        directionTile[tileTriple + 0] = sourceDirections[sampleTriple + 0];
        directionTile[tileTriple + 1] = sourceDirections[sampleTriple + 1];
        directionTile[tileTriple + 2] = sourceDirections[sampleTriple + 2];
        colorTile[tileTriple + 0] = colorLab[sampleTriple + 0];
        colorTile[tileTriple + 1] = colorLab[sampleTriple + 1];
        colorTile[tileTriple + 2] = colorLab[sampleTriple + 2];
    }

    GroupMemoryBarrierWithGroupSync();
    if (x >= width || y >= height)
        return;
    int centerLocalX = GroupIds.x + __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
    int centerLocalY = GroupIds.y + __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius;
    int centerTile = (centerLocalY * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileSize + centerLocalX) * 3;
    int triple = (y * width + x) * 3;
    float nl = directionTile[centerTile + 0];
    float na = directionTile[centerTile + 1];
    float nb = directionTile[centerTile + 2];
    if (nl * nl + na * na + nb * nb < __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionFieldConstants__ValidLengthSquaredThreshold)
    {
        targetDirections[triple + 0] = 0.0;
        targetDirections[triple + 1] = 0.0;
        targetDirections[triple + 2] = 0.0;
        return;
    }

    float cl = colorTile[centerTile + 0];
    float ca = colorTile[centerTile + 1];
    float cb = colorTile[centerTile + 2];
    float sumL = 0.0;
    float sumA = 0.0;
    float sumB = 0.0;
    float sumW = 0.0;
    for (int dy = -__YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius; dy <= __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius; dy++)
    {
        int sy = y + dy;
        if (sy < 0 || sy >= height)
            continue;
        for (int dx = -__YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius; dx <= __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius; dx++)
        {
            int sx = x + dx;
            if (sx < 0 || sx >= width)
                continue;
            int sTile = ((centerLocalY + dy) * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__TileSize + (centerLocalX + dx)) * 3;
            float ml = directionTile[sTile + 0];
            float ma = directionTile[sTile + 1];
            float mb = directionTile[sTile + 2];
            if (ml * ml + ma * ma + mb * mb < __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionFieldConstants__ValidLengthSquaredThreshold)
                continue;
            float __reserved__dot = nl * ml + na * ma + nb * mb;
            if (__reserved__dot <= 0.0)
                continue;
            float wSpace = spaceTable[(dy + __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius) * __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__SpaceTableStride + (dx + __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionSmoothConstants__Radius)];
            float dcl = cl - colorTile[sTile + 0];
            float dca = ca - colorTile[sTile + 1];
            float dcb = cb - colorTile[sTile + 2];
            float colorDistSq = dcl * dcl + dca * dca + dcb * dcb;
            float wColor = exp(-colorDistSq / max(sigmaColorSq, 1E-06));
            float w = wSpace * wColor * __reserved__dot;
            sumL += ml * w;
            sumA += ma * w;
            sumB += mb * w;
            sumW += w;
        }
    }

    if (sumW > 1E-06)
    {
        float avgL = sumL / sumW;
        float avgA = sumA / sumW;
        float avgB = sumB / sumW;
        float norm = sqrt(avgL * avgL + avgA * avgA + avgB * avgB);
        if (norm > 1E-06)
        {
            float inv = 1.0 / norm;
            targetDirections[triple + 0] = avgL * inv;
            targetDirections[triple + 1] = avgA * inv;
            targetDirections[triple + 2] = avgB * inv;
            return;
        }
    }

    targetDirections[triple + 0] = nl;
    targetDirections[triple + 1] = na;
    targetDirections[triple + 2] = nb;
}
