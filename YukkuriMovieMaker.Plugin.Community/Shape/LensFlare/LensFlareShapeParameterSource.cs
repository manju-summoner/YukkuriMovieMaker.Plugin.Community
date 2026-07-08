using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using D2DEffects = Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    class LensFlareShapeParameterSource : IShapeSource2
    {
        readonly DisposeCollector disposer = new();
        public ID2D1Image Output { get; }
        public IEnumerable<VideoController> Controllers { get; private set; } = [];

        readonly LensFlareShapeParameter parameter;

#pragma warning disable IDE0044 // 読み取り専用修飾子を追加します
        D2DEffects.Flood floodEffect;
        D2DEffects.Crop cropEffect;
        LensFlareCustomEffect? lensFlareEffect;
#pragma warning restore IDE0044 // 読み取り専用修飾子を追加します

        bool isFirst = true;
        Vector4 cropRect;
        double x, y, intensity, scale, bladeCount, rotation, starLength, starBrightness,
            starWidth, streakBrightness, streakWidth, shimmerBrightness,
            ghostCount, ghostBrightness, haloRadius, haloBrightness, dispersion, seed;
        Color lightColor;
        System.Drawing.Size screenSize;

        public LensFlareShapeParameterSource(IGraphicsDevicesAndContext devices, LensFlareShapeParameter parameter)
        {
            this.parameter = parameter;

            floodEffect = new D2DEffects.Flood(devices.DeviceContext);
            disposer.Collect(floodEffect);
            cropEffect = new D2DEffects.Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);
            lensFlareEffect = new LensFlareCustomEffect(devices);
            disposer.Collect(lensFlareEffect);

            floodEffect.Color = new Vector4(0f, 0f, 0f, 0f);

            if (!lensFlareEffect.IsEnabled)
            {
                //ShaderModel非対応環境では透明画像を出力する
                disposer.RemoveAndDispose(ref lensFlareEffect);

                cropEffect.Rectangle = new Vector4(-1, -1, 2, 2);
                using (var output = floodEffect.Output)
                    cropEffect.SetInput(0, output, true);
            }
            else
            {
                using (var output = floodEffect.Output)
                    lensFlareEffect.SetInput(0, output, true);
                using (var output = lensFlareEffect.Output)
                    cropEffect.SetInput(0, output, true);
            }

            var result = cropEffect.Output;
            disposer.Collect(result);
            Output = result;
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var x = parameter.X.GetValue(frame, length, fps);
            var y = parameter.Y.GetValue(frame, length, fps);

            //光源位置はプレビュー上のコントローラーでドラッグ操作できる
            Controllers =
            [
                new VideoController(
                [
                    new ControllerPoint(
                        new Vector3((float)x, (float)y, 0),
                        arg =>
                        {
                            parameter.X.AddToEachValues(arg.Delta.X);
                            parameter.Y.AddToEachValues(arg.Delta.Y);
                        })
                ])
            ];

            if (lensFlareEffect is null)
                return;

            var intensity = parameter.Intensity.GetValue(frame, length, fps);
            var scale = parameter.Scale.GetValue(frame, length, fps);
            var lightColor = parameter.LightColor;
            var bladeCount = Math.Round(parameter.BladeCount.GetValue(frame, length, fps));
            var rotation = parameter.Rotation.GetValue(frame, length, fps);
            var starLength = parameter.StarLength.GetValue(frame, length, fps);
            var starBrightness = parameter.StarBrightness.GetValue(frame, length, fps);
            var starWidth = parameter.StarWidth.GetValue(frame, length, fps);
            var streakBrightness = parameter.StreakBrightness.GetValue(frame, length, fps);
            var streakWidth = parameter.StreakWidth.GetValue(frame, length, fps);
            var shimmerBrightness = parameter.ShimmerBrightness.GetValue(frame, length, fps);
            var ghostCount = Math.Round(parameter.GhostCount.GetValue(frame, length, fps));
            var ghostBrightness = parameter.GhostBrightness.GetValue(frame, length, fps);
            var haloRadius = parameter.HaloRadius.GetValue(frame, length, fps);
            var haloBrightness = parameter.HaloBrightness.GetValue(frame, length, fps);
            var dispersion = parameter.Dispersion.GetValue(frame, length, fps);
            var seed = Math.Round(parameter.Seed.GetValue(frame, length, fps));
            var screenSize = desc.ScreenSize;

            if (isFirst || this.screenSize != screenSize)
            {
                //スクリーンサイズは各成分の大きさの基準としてのみ使い、出力範囲はパラメーターから決める
                lensFlareEffect.CanvasWidth = screenSize.Width;
                lensFlareEffect.CanvasHeight = screenSize.Height;
            }

            //出力範囲はスクリーンサイズ固定ではなく、各成分の減衰から内容が収まる矩形を逆算する
            var bounds = CalculateContentBounds(
                x, y, intensity / 100, scale / 100,
                starLength / 100, starBrightness / 100,
                streakBrightness / 100, streakWidth / 100, shimmerBrightness / 100,
                ghostCount, ghostBrightness / 100,
                haloRadius / 100, haloBrightness / 100, dispersion / 100,
                screenSize);
            if (isFirst || cropRect != bounds)
            {
                cropEffect.Rectangle = bounds;
                cropRect = bounds;
            }
            if (isFirst || this.x != x)
                lensFlareEffect.LightX = (float)x;
            if (isFirst || this.y != y)
                lensFlareEffect.LightY = (float)y;
            if (isFirst || this.intensity != intensity)
                lensFlareEffect.Intensity = (float)(intensity / 100);
            if (isFirst || this.scale != scale)
                lensFlareEffect.Scale = (float)(scale / 100);
            if (isFirst || this.lightColor != lightColor)
            {
                lensFlareEffect.ColorR = lightColor.R / 255f * lightColor.A / 255f;
                lensFlareEffect.ColorG = lightColor.G / 255f * lightColor.A / 255f;
                lensFlareEffect.ColorB = lightColor.B / 255f * lightColor.A / 255f;
            }
            if (isFirst || this.bladeCount != bladeCount)
                lensFlareEffect.Blades = (float)bladeCount;
            if (isFirst || this.rotation != rotation)
                lensFlareEffect.Rotation = (float)(rotation / 180 * Math.PI);
            if (isFirst || this.starLength != starLength)
                lensFlareEffect.StarLength = (float)(starLength / 100);
            if (isFirst || this.starBrightness != starBrightness)
                lensFlareEffect.StarBrightness = (float)(starBrightness / 100);
            if (isFirst || this.starWidth != starWidth)
                lensFlareEffect.StarWidth = (float)(starWidth / 100);
            if (isFirst || this.streakBrightness != streakBrightness)
                lensFlareEffect.StreakBrightness = (float)(streakBrightness / 100);
            if (isFirst || this.streakWidth != streakWidth)
                lensFlareEffect.StreakWidth = (float)(streakWidth / 100);
            if (isFirst || this.shimmerBrightness != shimmerBrightness)
                lensFlareEffect.ShimmerBrightness = (float)(shimmerBrightness / 100);
            if (isFirst || this.ghostCount != ghostCount)
                lensFlareEffect.GhostCount = (float)ghostCount;
            if (isFirst || this.ghostBrightness != ghostBrightness)
                lensFlareEffect.GhostBrightness = (float)(ghostBrightness / 100);
            if (isFirst || this.haloRadius != haloRadius)
                lensFlareEffect.HaloRadius = (float)(haloRadius / 100);
            if (isFirst || this.haloBrightness != haloBrightness)
                lensFlareEffect.HaloBrightness = (float)(haloBrightness / 100);
            if (isFirst || this.dispersion != dispersion)
                lensFlareEffect.Dispersion = (float)(dispersion / 100);
            if (isFirst || this.seed != seed)
                lensFlareEffect.Seed = (float)seed;

            isFirst = false;
            this.x = x;
            this.y = y;
            this.intensity = intensity;
            this.scale = scale;
            this.lightColor = lightColor;
            this.bladeCount = bladeCount;
            this.rotation = rotation;
            this.starLength = starLength;
            this.starBrightness = starBrightness;
            this.starWidth = starWidth;
            this.streakBrightness = streakBrightness;
            this.streakWidth = streakWidth;
            this.shimmerBrightness = shimmerBrightness;
            this.ghostCount = ghostCount;
            this.ghostBrightness = ghostBrightness;
            this.haloRadius = haloRadius;
            this.haloBrightness = haloBrightness;
            this.dispersion = dispersion;
            this.seed = seed;
            this.screenSize = screenSize;
        }

        /// <summary>
        /// シェーダー(LensFlare.hlsl)の各成分の減衰式から、寄与が閾値(約0.5/255)を下回る半径を逆算し、
        /// フレア全体が収まる出力矩形(原点=光学中心)を求める。
        /// 係数はシェーダー側の定数に対応している。シェーダーを変更したらここも合わせること。
        /// 引数はいずれもシェーダーに渡すのと同じ単位(%は1=100%換算済み)。
        /// </summary>
        static Vector4 CalculateContentBounds(
            double x, double y, double intensity, double scale,
            double starLength, double starBrightness,
            double streakBrightness, double streakWidth, double shimmerBrightness,
            double ghostCount, double ghostBrightness,
            double haloRadius, double haloBrightness, double dispersion,
            System.Drawing.Size screenSize)
        {
            const double Epsilon = 0.002;   //8bitでほぼ見えなくなる寄与
            const double MaxExtent = 8192;  //D2Dビットマップ上限(16384px)に収まる片側最大

            var s = Math.Max(0.01, scale);
            double minDim = Math.Max(1, Math.Min(screenSize.Width, screenSize.Height));
            var i = Math.Max(0, intensity);
            var dispMax = 1.0 + 0.148 * Math.Max(0, dispersion); //LAMBDA.r(赤が最も外側に広がる)
            var lightDist = Math.Sqrt(x * x + y * y);

            //ガウス A*exp(-r^2/(2σ^2)) = ε となる r
            static double GaussR(double a, double sigma) =>
                a <= Epsilon ? 0 : sigma * Math.Sqrt(2 * Math.Log(a / Epsilon));

            //1. 光源PSF(コア+多段グロー+スカート)
            var sigma = 10 * s;
            var rLight = GaussR(2.5 * i, sigma);
            rLight = Math.Max(rLight, GaussR(0.45 * i, 3 * sigma) * dispMax);
            rLight = Math.Max(rLight, GaussR(0.18 * i, 8 * sigma) * dispMax);
            var aSkirt = 0.10 * i; //スカート A/(1+t^2)^1.5 = ε, t = r/(3σ・disp)
            if (aSkirt > Epsilon)
                rLight = Math.Max(rLight, 3 * sigma * dispMax * Math.Sqrt(Math.Pow(aSkirt / Epsilon, 2.0 / 3) - 1));

            //1b. 回折コロナ(リング中心半径+リング幅の減衰分)
            var aCorona = 0.30 * i * Math.Clamp(dispersion, 0, 1);
            if (aCorona > Epsilon)
                rLight = Math.Max(rLight, 30 * s * (1 + 0.148 * 1.6 * dispersion) + GaussR(aCorona, 7 * s));

            //2. 光条: A*exp(-r/(2L))/(1+(3r/L)^2) = ε を二分法で解く
            var aStar = 0.6 * i * starBrightness;
            if (aStar > Epsilon)
            {
                var lStar = Math.Max(1, 0.30 * minDim * starLength * s);
                var lo = 0.0;
                var hi = 2 * lStar * Math.Log(aStar / Epsilon); //exp項のみの上界
                for (var k = 0; k < 24; k++)
                {
                    var mid = (lo + hi) / 2;
                    var t = 3 * mid / lStar;
                    var v = aStar * Math.Exp(-mid / (2 * lStar)) / (1 + t * t);
                    if (v > Epsilon) lo = mid; else hi = mid;
                }
                rLight = Math.Max(rLight, hi * dispMax);
            }

            //1d. シマー光条(最長レイ: exp(-r/lFan) = ε/A)
            var aShimmer = 0.55 * i * shimmerBrightness;
            if (aShimmer > Epsilon)
                rLight = Math.Max(rLight, Math.Max(1, 0.10 * minDim * s) * Math.Log(aShimmer / Epsilon) * dispMax);

            //1c. アナモルフィックストリーク(横長矩形)
            double streakX = 0, streakY = 0;
            var aStreak = 0.9 * i * streakBrightness;
            if (aStreak > Epsilon)
            {
                var lStreak = Math.Max(1, 0.40 * screenSize.Width * s);
                streakX = lStreak / 3 * Math.Log(aStreak / Epsilon); //exp項
                var tail = 0.08 * aStreak / Epsilon - 1;             //ローレンツ裾
                if (tail > 0)
                    streakX = Math.Max(streakX, lStreak * 0.5 * Math.Sqrt(tail));
                var w = (1.6 + 0.010 * streakX) * s * Math.Max(0.01, streakWidth);
                streakY = w * Math.Sqrt(Math.Log(aStreak / Epsilon));
            }

            //3. ゴースト(光学中心を挟んで si∈[-1.5, 0.9] に並ぶ+ビネットの届く範囲)
            double rOrigin = 0;
            if (ghostBrightness > 0 && ghostCount > 0)
            {
                var ghostSize = minDim * s * 0.135;
                var offAxis = lightDist / (0.5 * minDim);
                rOrigin = 1.5 * lightDist + ghostSize * (1.8 + 0.9 * offAxis);
            }

            //4. ハローリング(光学中心)
            var aHalo = 0.25 * i * haloBrightness;
            if (aHalo > Epsilon && haloRadius > 0)
            {
                var baseR = 0.5 * minDim * haloRadius * s;
                var wh = Math.Max(4, 0.12 * baseR);
                rOrigin = Math.Max(rOrigin, baseR * (1 + 0.148 * 0.35 * dispersion) + GaussR(aHalo, wh));
            }

            //光源周りの円・ストリーク矩形・光学中心周りの円の和を取る
            var minX = Math.Min(Math.Min(x - rLight, x - streakX), -rOrigin);
            var maxX = Math.Max(Math.Max(x + rLight, x + streakX), rOrigin);
            var minY = Math.Min(Math.Min(y - rLight, y - streakY), -rOrigin);
            var maxY = Math.Max(Math.Max(y + rLight, y + streakY), rOrigin);

            //D2Dの上限に収めつつ、全成分が無効でも最低限の矩形を返す
            minX = Math.Clamp(minX, -MaxExtent, MaxExtent - 2);
            minY = Math.Clamp(minY, -MaxExtent, MaxExtent - 2);
            maxX = Math.Clamp(maxX, minX + 2, MaxExtent);
            maxY = Math.Clamp(maxY, minY + 2, MaxExtent);

            return new Vector4((float)minX, (float)minY, (float)maxX, (float)maxY);
        }

        #region IDisposable
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposer?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
