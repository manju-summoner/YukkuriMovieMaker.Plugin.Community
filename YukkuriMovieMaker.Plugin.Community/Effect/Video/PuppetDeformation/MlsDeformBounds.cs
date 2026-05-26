using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal static class MlsDeformBounds
    {
        const float Epsilon = 1e-6f;
        const int GridResolution = 48;

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

            if (n == 0)
                return (-halfW, -halfH, halfW, halfH);

            if (n == 1)
            {
                var delta = currentLocal[0] - restLocal[0];
                return (-halfW + delta.X, -halfH + delta.Y, halfW + delta.X, halfH + delta.Y);
            }

            float alpha = Math.Clamp(stiffness, 0.1f, 8.0f);
            float scale = Math.Max(imageWidth, imageHeight);
            float scaleInvSq = 1f / (scale * scale);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                var c = currentLocal[i];
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.X > maxX) maxX = c.X;
                if (c.Y > maxY) maxY = c.Y;
            }

            for (int row = 0; row <= GridResolution; row++)
            {
                float ty = (float)row / GridResolution;
                float py = -halfH + imageHeight * ty;

                for (int col = 0; col <= GridResolution; col++)
                {
                    float tx = (float)col / GridResolution;
                    float px = -halfW + imageWidth * tx;

                    var q = MapPointForward(new Vector2(px, py), restLocal, currentLocal, n, alpha, scaleInvSq);

                    if (q.X < minX) minX = q.X;
                    if (q.Y < minY) minY = q.Y;
                    if (q.X > maxX) maxX = q.X;
                    if (q.Y > maxY) maxY = q.Y;
                }
            }

            float margin = scale * (0.5f / GridResolution);
            return (minX - margin, minY - margin, maxX + margin, maxY + margin);
        }

        static Vector2 MapPointForward(
            Vector2 p,
            IReadOnlyList<Vector2> restLocal,
            IReadOnlyList<Vector2> currentLocal,
            int n,
            float alpha,
            float scaleInvSq)
        {
            float totalW = 0f;
            float rStarX = 0f, rStarY = 0f;
            float cStarX = 0f, cStarY = 0f;
            float minDistSq = float.MaxValue;
            int nearest = 0;

            for (int i = 0; i < n; i++)
            {
                float dx = restLocal[i].X - p.X;
                float dy = restLocal[i].Y - p.Y;
                float distSq = (dx * dx + dy * dy) * scaleInvSq + Epsilon;
                if (distSq < minDistSq) { minDistSq = distSq; nearest = i; }
                float wi = MathF.Pow(distSq, -alpha);
                totalW += wi;
                rStarX += wi * restLocal[i].X;
                rStarY += wi * restLocal[i].Y;
                cStarX += wi * currentLocal[i].X;
                cStarY += wi * currentLocal[i].Y;
            }

            if (minDistSq < Epsilon * 4f)
                return currentLocal[nearest];

            float invW = 1f / totalW;
            rStarX *= invW; rStarY *= invW;
            cStarX *= invW; cStarY *= invW;

            float vHatX = p.X - rStarX;
            float vHatY = p.Y - rStarY;
            float vHatLen = MathF.Sqrt(vHatX * vHatX + vHatY * vHatY);

            float frX = 0f;
            float frY = 0f;

            for (int i = 0; i < n; i++)
            {
                float dx = restLocal[i].X - p.X;
                float dy = restLocal[i].Y - p.Y;
                float distSq = (dx * dx + dy * dy) * scaleInvSq + Epsilon;
                float wi = MathF.Pow(distSq, -alpha);

                float rHatX = restLocal[i].X - rStarX;
                float rHatY = restLocal[i].Y - rStarY;
                float cHatX = currentLocal[i].X - cStarX;
                float cHatY = currentLocal[i].Y - cStarY;

                float rHatPerpX = -rHatY;
                float rHatPerpY = rHatX;

                float dotRV = rHatX * vHatX + rHatY * vHatY;
                float dotPV = rHatPerpX * vHatX + rHatPerpY * vHatY;

                frX += wi * (dotRV * cHatX - dotPV * cHatY);
                frY += wi * (dotRV * cHatY + dotPV * cHatX);
            }

            float frLen = MathF.Sqrt(frX * frX + frY * frY);

            if (frLen < Epsilon)
                return new Vector2(p.X - rStarX + cStarX, p.Y - rStarY + cStarY);

            float normScale = vHatLen / frLen;
            return new Vector2(normScale * frX + cStarX, normScale * frY + cStarY);
        }
    }
}
