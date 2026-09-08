// pow の底は sRGB が 0.04045 を超える枝でのみ評価され、必ず正になる。
#pragma warning(disable: 3571)
#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    float backgroundR;
    float backgroundG;
    float backgroundB;
    int reach;
    float sigmaLineSq;
    int width;
    int height;
}

RWStructuredBuffer<int> sourceForeground : register(u0);

RWStructuredBuffer<int> sourceValid : register(u1);

StructuredBuffer<int> bgra : register(t0);

RWStructuredBuffer<int> targetForeground : register(u2);

RWStructuredBuffer<int> targetValid : register(u3);

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID)
{
    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
    {
        int x = ThreadIds.x;
        int y = ThreadIds.y;
        if (x >= width || y >= height)
            return;
        int index = y * width + x;
        int packed = bgra[index];
        int a = (packed >> 24) & 0xFF;
        if (a == 0)
        {
            targetForeground[index] = 0;
            targetValid[index] = 0;
            return;
        }

        float bgRl = backgroundR <= 0.04045 ? backgroundR / 12.92 : pow((backgroundR + 0.055) / 1.055, 2.4);
        float bgGl = backgroundG <= 0.04045 ? backgroundG / 12.92 : pow((backgroundG + 0.055) / 1.055, 2.4);
        float bgBl = backgroundB <= 0.04045 ? backgroundB / 12.92 : pow((backgroundB + 0.055) / 1.055, 2.4);
        float bgLenSq = bgRl * bgRl + bgGl * bgGl + bgBl * bgBl;
        float invA = 1.0 / a;
        float observedRs = saturate(((packed >> 16) & 0xFF) * invA);
        float observedGs = saturate(((packed >> 8) & 0xFF) * invA);
        float observedBs = saturate(((packed >> 0) & 0xFF) * invA);
        float observedR = observedRs <= 0.04045 ? observedRs / 12.92 : pow((observedRs + 0.055) / 1.055, 2.4);
        float observedG = observedGs <= 0.04045 ? observedGs / 12.92 : pow((observedGs + 0.055) / 1.055, 2.4);
        float observedB = observedBs <= 0.04045 ? observedBs / 12.92 : pow((observedBs + 0.055) / 1.055, 2.4);
        float obr = observedR - bgRl;
        float obg = observedG - bgGl;
        float obb = observedB - bgBl;
        int bestForeground = 0;
        float bestPurity = -1.0;
        for (int dy = -reach; dy <= reach; dy++)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= height)
                continue;
            for (int dx = -reach; dx <= reach; dx++)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= width)
                    continue;
                int sIndex = sy * width + sx;
                if (sourceValid[sIndex] == 0)
                    continue;
                int f = sourceForeground[sIndex];
                float frs = ((f >> 16) & 0xFF) * (1.0 / 255.0);
                float fgs = ((f >> 8) & 0xFF) * (1.0 / 255.0);
                float fbs = ((f >> 0) & 0xFF) * (1.0 / 255.0);
                float fr = frs <= 0.04045 ? frs / 12.92 : pow((frs + 0.055) / 1.055, 2.4);
                float fg = fgs <= 0.04045 ? fgs / 12.92 : pow((fgs + 0.055) / 1.055, 2.4);
                float fb = fbs <= 0.04045 ? fbs / 12.92 : pow((fbs + 0.055) / 1.055, 2.4);
                float dr = fr - bgRl;
                float dg = fg - bgGl;
                float db = fb - bgBl;
                float dlen2 = dr * dr + dg * dg + db * db;
                if (dlen2 < 1E-08)
                    continue;
                float t = (obr * dr + obg * dg + obb * db) / dlen2;
                float pr = obr - t * dr;
                float pg = obg - t * dg;
                float pb = obb - t * db;
                float distSq = pr * pr + pg * pg + pb * pb;
                if (distSq > sigmaLineSq * dlen2)
                    continue;
                float dotFB = fr * bgRl + fg * bgGl + fb * bgBl;
                float purity = fr * fr + fg * fg + fb * fb - (bgLenSq > 1E-08 ? dotFB * dotFB / bgLenSq : 0.0);
                if (purity > bestPurity)
                {
                    bestPurity = purity;
                    bestForeground = f;
                }
            }
        }

        if (bestPurity >= 0.0)
        {
            targetForeground[index] = bestForeground;
            targetValid[index] = 1;
            return;
        }

        targetForeground[index] = 0;
        targetValid[index] = 0;
    }
}