#include "DirectionalColorKeyCS.hlsli"

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

groupshared float directionTile [SMOOTH_TILE_COUNT * 3];

groupshared float colorTile [SMOOTH_TILE_COUNT * 3];

groupshared float spaceTable [SMOOTH_SPACE_TABLE_COUNT];

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID, uint3 groupThreadId : SV_GroupThreadID, uint groupIndex : SV_GroupIndex)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    float twoSigmaSpaceSq = 2.0 * SMOOTH_RADIUS * SMOOTH_RADIUS;
    for (uint slot = groupIndex; slot < SMOOTH_SPACE_TABLE_COUNT; slot += GROUP_THREADS)
    {
        int ty = slot / SMOOTH_SPACE_TABLE_STRIDE;
        int tx = slot - ty * SMOOTH_SPACE_TABLE_STRIDE;
        int offX = tx - SMOOTH_RADIUS;
        int offY = ty - SMOOTH_RADIUS;
        spaceTable[slot] = exp(-(offX * offX + offY * offY) / max(twoSigmaSpaceSq, 1E-06));
    }

    int originX = x - groupThreadId.x - SMOOTH_RADIUS;
    int originY = y - groupThreadId.y - SMOOTH_RADIUS;
    for (uint tileSlot = groupIndex; tileSlot < SMOOTH_TILE_COUNT; tileSlot += GROUP_THREADS)
    {
        int localY = tileSlot / SMOOTH_TILE_SIZE;
        int localX = tileSlot - localY * SMOOTH_TILE_SIZE;
        int sampleX = originX + localX;
        int sampleY = originY + localY;
        int tileTriple = tileSlot * 3;
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
    int centerLocalX = groupThreadId.x + SMOOTH_RADIUS;
    int centerLocalY = groupThreadId.y + SMOOTH_RADIUS;
    int centerTile = (centerLocalY * SMOOTH_TILE_SIZE + centerLocalX) * 3;
    int triple = (y * width + x) * 3;
    float nl = directionTile[centerTile + 0];
    float na = directionTile[centerTile + 1];
    float nb = directionTile[centerTile + 2];
    if (nl * nl + na * na + nb * nb < VALID_LENGTH_SQUARED_THRESHOLD)
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
    for (int dy = -SMOOTH_RADIUS; dy <= SMOOTH_RADIUS; dy++)
    {
        int sy = y + dy;
        if (sy < 0 || sy >= height)
            continue;
        for (int dx = -SMOOTH_RADIUS; dx <= SMOOTH_RADIUS; dx++)
        {
            int sx = x + dx;
            if (sx < 0 || sx >= width)
                continue;
            int sTile = ((centerLocalY + dy) * SMOOTH_TILE_SIZE + (centerLocalX + dx)) * 3;
            float ml = directionTile[sTile + 0];
            float ma = directionTile[sTile + 1];
            float mb = directionTile[sTile + 2];
            if (ml * ml + ma * ma + mb * mb < VALID_LENGTH_SQUARED_THRESHOLD)
                continue;
            float __reserved__dot = nl * ml + na * ma + nb * mb;
            if (__reserved__dot <= 0.0)
                continue;
            float wSpace = spaceTable[(dy + SMOOTH_RADIUS) * SMOOTH_SPACE_TABLE_STRIDE + (dx + SMOOTH_RADIUS)];
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
