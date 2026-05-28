using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal static class MlsDeformBounds
    {
        const float Epsilon = 1e-6f;
        const float HalfTexel = 0.5f;
        const int GridResolution = 96;
        const float SearchExpansionFactor = 1.5f;

        public static (float left, float top, float right, float bottom) Compute(
            float imageWidth,
            float imageHeight,
            IReadOnlyList<Vector2> restLocal,
            IReadOnlyList<Vector2> currentLocal,
            float stiffness)
        {
            int n = Math.Min(restLocal.Count, currentLocal.Count);

            float halfW = imageWidth * 0.5f;
            float halfH = imageHeight * 0.5f;

            if (n == 0 || imageWidth <= 0f || imageHeight <= 0f)
                return (-halfW, -halfH, halfW, halfH);

            if (n == 1)
            {
                var delta = currentLocal[0] - restLocal[0];
                return (-halfW + delta.X, -halfH + delta.Y, halfW + delta.X, halfH + delta.Y);
            }

            float alpha = Math.Clamp(stiffness, 0.1f, 8.0f);
            float scale = Math.Max(imageWidth, imageHeight);
            float scaleInvSq = 1f / (scale * scale);

            float marginedMinX = -halfW + HalfTexel;
            float marginedMinY = -halfH + HalfTexel;
            float marginedMaxX = halfW - HalfTexel;
            float marginedMaxY = halfH - HalfTexel;

            float regionMinX = -halfW, regionMinY = -halfH;
            float regionMaxX = halfW, regionMaxY = halfH;
            float maxDisplacement = 0f;

            for (int i = 0; i < n; i++)
            {
                var r = restLocal[i];
                var c = currentLocal[i];

                if (r.X < regionMinX) regionMinX = r.X;
                if (r.Y < regionMinY) regionMinY = r.Y;
                if (r.X > regionMaxX) regionMaxX = r.X;
                if (r.Y > regionMaxY) regionMaxY = r.Y;
                if (c.X < regionMinX) regionMinX = c.X;
                if (c.Y < regionMinY) regionMinY = c.Y;
                if (c.X > regionMaxX) regionMaxX = c.X;
                if (c.Y > regionMaxY) regionMaxY = c.Y;

                float ddx = MathF.Abs(c.X - r.X);
                float ddy = MathF.Abs(c.Y - r.Y);
                if (ddx > maxDisplacement) maxDisplacement = ddx;
                if (ddy > maxDisplacement) maxDisplacement = ddy;
            }

            float expansion = maxDisplacement * SearchExpansionFactor + scale;
            float searchMinX = regionMinX - expansion;
            float searchMinY = regionMinY - expansion;
            float searchMaxX = regionMaxX + expansion;
            float searchMaxY = regionMaxY + expansion;
            float searchWidth = searchMaxX - searchMinX;
            float searchHeight = searchMaxY - searchMinY;

            float outMinX = float.PositiveInfinity;
            float outMinY = float.PositiveInfinity;
            float outMaxX = float.NegativeInfinity;
            float outMaxY = float.NegativeInfinity;
            bool found = false;

            for (int row = 0; row <= GridResolution; row++)
            {
                float vy = searchMinY + searchHeight * ((float)row / GridResolution);

                for (int col = 0; col <= GridResolution; col++)
                {
                    float vx = searchMinX + searchWidth * ((float)col / GridResolution);

                    var source = InverseMlsMap(new Vector2(vx, vy), restLocal, currentLocal, n, alpha, scaleInvSq);

                    if (source.X >= marginedMinX && source.X <= marginedMaxX
                        && source.Y >= marginedMinY && source.Y <= marginedMaxY)
                    {
                        if (vx < outMinX) outMinX = vx;
                        if (vy < outMinY) outMinY = vy;
                        if (vx > outMaxX) outMaxX = vx;
                        if (vy > outMaxY) outMaxY = vy;
                        found = true;
                    }
                }
            }

            if (!found)
                return (-halfW, -halfH, halfW, halfH);

            float cellW = searchWidth / GridResolution;
            float cellH = searchHeight / GridResolution;
            return (outMinX - cellW, outMinY - cellH, outMaxX + cellW, outMaxY + cellH);
        }

        static Vector2 InverseMlsMap(
            Vector2 v,
            IReadOnlyList<Vector2> restLocal,
            IReadOnlyList<Vector2> currentLocal,
            int n,
            float alpha,
            float scaleInvSq)
        {
            Span<float> weights = stackalloc float[n];
            float totalW = 0f;
            float pStarX = 0f, pStarY = 0f;
            float qStarX = 0f, qStarY = 0f;
            float minDistSq = float.MaxValue;
            int nearest = 0;

            for (int i = 0; i < n; i++)
            {
                float dx = currentLocal[i].X - v.X;
                float dy = currentLocal[i].Y - v.Y;
                float distSq = (dx * dx + dy * dy) * scaleInvSq + Epsilon;
                if (distSq < minDistSq) { minDistSq = distSq; nearest = i; }
                float wi = MathF.Pow(distSq, -alpha);
                weights[i] = wi;
                totalW += wi;
                pStarX += wi * currentLocal[i].X;
                pStarY += wi * currentLocal[i].Y;
                qStarX += wi * restLocal[i].X;
                qStarY += wi * restLocal[i].Y;
            }

            if (minDistSq < Epsilon * 4f || float.IsInfinity(totalW))
                return restLocal[nearest];

            float invW = 1f / totalW;
            pStarX *= invW; pStarY *= invW;
            qStarX *= invW; qStarY *= invW;

            float vHatX = v.X - pStarX;
            float vHatY = v.Y - pStarY;
            float vHatLen = MathF.Sqrt(vHatX * vHatX + vHatY * vHatY);

            float frX = 0f;
            float frY = 0f;

            for (int i = 0; i < n; i++)
            {
                float pHatX = currentLocal[i].X - pStarX;
                float pHatY = currentLocal[i].Y - pStarY;
                float qHatX = restLocal[i].X - qStarX;
                float qHatY = restLocal[i].Y - qStarY;

                float pHatPerpX = -pHatY;
                float pHatPerpY = pHatX;

                float dotPV = pHatX * vHatX + pHatY * vHatY;
                float dotPperpV = pHatPerpX * vHatX + pHatPerpY * vHatY;

                float wi = weights[i];
                frX += wi * (dotPV * qHatX - dotPperpV * qHatY);
                frY += wi * (dotPV * qHatY + dotPperpV * qHatX);
            }

            float frLen = MathF.Sqrt(frX * frX + frY * frY);

            if (frLen < Epsilon)
                return new Vector2(v.X - pStarX + qStarX, v.Y - pStarY + qStarY);

            float normScale = vHatLen / frLen;
            return new Vector2(normScale * frX + qStarX, normScale * frY + qStarY);
        }
    }
}
