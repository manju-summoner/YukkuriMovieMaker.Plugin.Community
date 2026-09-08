using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons.Compute;
using YukkuriMovieMaker.Player.Video.Effects;
using Color = System.Windows.Media.Color;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    internal sealed class DirectionalColorKeyEffectProcessor : VideoEffectProcessorBase
    {
        private static readonly Vector3 WhiteLab = new(1f, 0f, 0f);

        private readonly IGraphicsDevicesAndContext devices;
        private readonly DirectionalColorKeyEffect item;
        private DirectionalColorKeyAnalyzer? analyzer;

        private DirectionalColorKeyCustomEffect? effect;

        private ComputeSurface? sourceSurface;
        private ComputeSurface? foregroundSurface;
        private ID2D1Bitmap1? foregroundBitmap;
        private int sourceWidth, sourceHeight;
        private int foregroundWidth, foregroundHeight;
        private bool hasSourceContent;

        private bool isFirst = true;
        private bool hasAnalysisCache;
        private int lastFrame;
        private RawRectF lastBounds;
        private Color backgroundColor;
        private Color foregroundColor;
        private DirectionalColorKeyScaleMode scaleMode;
        private int clusterCount;
        private double noiseThreshold;
        private double sigmaColor;
        private double edgeSoftness;
        private double spillStrength;
        private double despillBias;
        private double opaquePercentile;
        private bool outputForeground;

        public DirectionalColorKeyEffectProcessor(IGraphicsDevicesAndContext devices, DirectionalColorKeyEffect item)
            : base(devices)
        {
            this.devices = devices;
            this.item = item;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            // 基底の構築子から呼ばれるため、構築子の本体より先に解析器を用意する。
            analyzer = DirectionalColorKeyAnalyzer.TryCreate(devices);

            // cs_5_0 に対応せず解析器を生成できなかった場合はパススルーする。
            if (analyzer is null)
                return null;
            disposer.Collect(analyzer);

            effect = new DirectionalColorKeyCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            effect?.SetInput(1, null, true);
            hasAnalysisCache = false;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null || analyzer is null || input is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(input);
            int width = (int)Math.Ceiling(bounds.Right - bounds.Left);
            int height = (int)Math.Ceiling(bounds.Bottom - bounds.Top);

            if (width <= 0 || height <= 0)
                return effectDescription.DrawDescription;

            var currentBackground = item.BackgroundColor;
            var currentForeground = item.ForegroundColor;
            var currentScaleMode = item.ScaleMode;
            var currentClusterCount = (int)Math.Round(Math.Clamp(item.ClusterCount.GetValue(frame, length, fps), 1, 4));
            var currentNoiseThreshold = item.NoiseThreshold.GetValue(frame, length, fps);
            var currentSigmaColor = item.SigmaColor.GetValue(frame, length, fps);
            var currentEdgeSoftness = item.EdgeSoftness.GetValue(frame, length, fps) / 100.0;
            var currentSpillStrength = item.SpillStrength.GetValue(frame, length, fps) / 100.0;
            var currentDespillBias = item.DespillBias.GetValue(frame, length, fps);
            var currentOpaquePercentile = item.OpaquePercentile.GetValue(frame, length, fps) / 100.0;
            var currentOutputForeground = item.OutputForeground;

            var backgroundLab = ToOklab(ToLinear(currentBackground));
            var backgroundChromaDir = ComputeChromaDirection(backgroundLab);

            int pixelCount = width * height;

            bool sourcePossiblyChanged = isFirst
                || !hasAnalysisCache
                || lastFrame != frame
                || !lastBounds.Equals(bounds);

            bool contentChanged = sourcePossiblyChanged && RenderSource(dc, bounds, width, height);

            bool analysisDirty = isFirst
                || !hasAnalysisCache
                || !lastBounds.Equals(bounds)
                || contentChanged
                || backgroundColor != currentBackground
                || foregroundColor != currentForeground
                || scaleMode != currentScaleMode
                || clusterCount != currentClusterCount
                || noiseThreshold != currentNoiseThreshold
                || sigmaColor != currentSigmaColor
                || opaquePercentile != currentOpaquePercentile;

            // コンテンツ変化（再生）以外の理由で解析が走るとき＝スケール決定に関わる
            // 設定が変わったときは、lambda の時間方向ブレンドをリセットして即座に反映する。
            bool lambdaInputsChanged = isFirst
                || !hasAnalysisCache
                || !lastBounds.Equals(bounds)
                || backgroundColor != currentBackground
                || foregroundColor != currentForeground
                || scaleMode != currentScaleMode
                || clusterCount != currentClusterCount
                || noiseThreshold != currentNoiseThreshold
                || sigmaColor != currentSigmaColor
                || opaquePercentile != currentOpaquePercentile;

            if (analysisDirty)
            {
                var whiteDirection = ComputeWhiteDirection(backgroundLab);
                float foregroundLambda = ComputeForegroundLambda(backgroundLab, currentForeground);

                analyzer.Analyze(
                    width,
                    height,
                    backgroundLab,
                    whiteDirection,
                    currentClusterCount,
                    (float)currentNoiseThreshold,
                    (float)Math.Max(currentSigmaColor, 1e-3),
                    currentScaleMode,
                    (float)currentOpaquePercentile,
                    foregroundLambda,
                    (keyDirection, floorValue) => ComputePhysicalLambda(backgroundLab, keyDirection, floorValue),
                    lambdaInputsChanged);

                ApplyClusters(analyzer, backgroundLab);

                var backgroundSrgb = new Vector3(
                    currentBackground.R / 255f,
                    currentBackground.G / 255f,
                    currentBackground.B / 255f);
                EnsureForegroundTarget(dc, width, height);
                if (foregroundSurface is not null)
                {
                    analyzer.BuildForegroundField(foregroundSurface, width, height, backgroundLab, backgroundSrgb);
                    effect.SetInput(1, foregroundSurface.Bitmap, true);
                }
                else
                {
                    var foregroundField = analyzer.BuildForegroundField(width, height, backgroundLab, backgroundSrgb);
                    UploadForegroundField(foregroundField, width);
                    effect.SetInput(1, foregroundBitmap, true);
                }

                hasAnalysisCache = true;
            }

            if (isFirst || backgroundColor != currentBackground)
            {
                effect.BackgroundLab = backgroundLab;
                effect.BackgroundChromaDir = backgroundChromaDir;
            }
            if (isFirst || noiseThreshold != currentNoiseThreshold)
                effect.NoiseThreshold = (float)currentNoiseThreshold;
            if (isFirst || edgeSoftness != currentEdgeSoftness)
                effect.EdgeSoftness = (float)currentEdgeSoftness;
            if (isFirst || spillStrength != currentSpillStrength)
                effect.SpillStrength = (float)currentSpillStrength;
            if (isFirst || despillBias != currentDespillBias)
                effect.DespillBias = (float)currentDespillBias;
            if (isFirst || outputForeground != currentOutputForeground)
                effect.OutputForeground = currentOutputForeground ? 1f : 0f;

            isFirst = false;
            lastFrame = frame;
            lastBounds = bounds;
            backgroundColor = currentBackground;
            foregroundColor = currentForeground;
            scaleMode = currentScaleMode;
            clusterCount = currentClusterCount;
            noiseThreshold = currentNoiseThreshold;
            sigmaColor = currentSigmaColor;
            edgeSoftness = currentEdgeSoftness;
            spillStrength = currentSpillStrength;
            despillBias = currentDespillBias;
            opaquePercentile = currentOpaquePercentile;
            outputForeground = currentOutputForeground;

            return effectDescription.DrawDescription;
        }

        private void ApplyClusters(DirectionalColorKeyAnalyzer analyzer, Vector3 backgroundLab)
        {
            if (effect is null)
                return;

            int count = analyzer.ClusterCount;
            effect.ClusterCount = count;

            effect.Cluster0 = PackCluster(analyzer, 0, count);
            effect.Cluster1 = PackCluster(analyzer, 1, count);
            effect.Cluster2 = PackCluster(analyzer, 2, count);
            effect.Cluster3 = PackCluster(analyzer, 3, count);
        }

        private static Vector4 PackCluster(DirectionalColorKeyAnalyzer analyzer, int index, int count)
        {
            if (index >= count)
                return new Vector4(0f, 0f, 0f, 1f);

            var center = analyzer.GetCenter(index);
            float lambda = analyzer.GetLambda(index);
            return new Vector4(center.X, center.Y, center.Z, lambda);
        }

        private bool RenderSource(ID2D1DeviceContext dc, RawRectF bounds, int width, int height)
        {
            bool reused = EnsureSourceSurface(dc, width, height) && hasSourceContent;

            var previousTarget = dc.Target;
            dc.Target = sourceSurface!.Bitmap;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(
                input!,
                new Vector2(-bounds.Left, -bounds.Top),
                null,
                InterpolationMode.NearestNeighbor,
                CompositeMode.SourceCopy);
            dc.EndDraw();
            dc.Target = previousTarget;

            int changed = analyzer!.UpdateSource(sourceSurface, width, height, reused);
            hasSourceContent = true;
            return !reused || changed != 0;
        }


        private unsafe void UploadForegroundField(ReadOnlySpan<int> field, int width)
        {
            fixed (int* src = field)
            {
                foregroundBitmap!.CopyFromMemory((nint)src, width * sizeof(int));
            }
        }

        // 面へ直接書けない環境では従来どおり CPU 転送へ落とす。
        private void EnsureForegroundTarget(ID2D1DeviceContext dc, int width, int height)
        {
            if ((foregroundSurface is not null || foregroundBitmap is not null)
                && foregroundWidth == width
                && foregroundHeight == height)
                return;

            disposer.RemoveAndDispose(ref foregroundSurface);
            disposer.RemoveAndDispose(ref foregroundBitmap);

            if (analyzer!.SupportsWritableSurface)
            {
                foregroundSurface = analyzer.CreateSurface(dc, width, height, true);
                disposer.Collect(foregroundSurface);
            }
            else
            {
                var pixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
                foregroundBitmap = dc.CreateBitmap(
                    new SizeI(width, height),
                    new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.None));
                disposer.Collect(foregroundBitmap);
            }

            foregroundWidth = width;
            foregroundHeight = height;
        }

        private bool EnsureSourceSurface(ID2D1DeviceContext dc, int width, int height)
        {
            if (sourceSurface is not null && sourceWidth == width && sourceHeight == height)
                return true;

            disposer.RemoveAndDispose(ref sourceSurface);
            sourceSurface = analyzer!.CreateSurface(dc, width, height, false);
            disposer.Collect(sourceSurface);

            sourceWidth = width;
            sourceHeight = height;
            hasSourceContent = false;
            return false;
        }



        private float ComputeForegroundLambda(Vector3 backgroundLab, Color foreground)
        {
            var foregroundLab = ToOklab(ToLinear(foreground));
            var direction = ComputeWhiteDirection(backgroundLab);
            return MathF.Max(Vector3.Dot(foregroundLab - backgroundLab, direction), 1e-5f);
        }

        private static Vector3 ComputeWhiteDirection(Vector3 backgroundLab)
        {
            var direction = WhiteLab - backgroundLab;
            float length = direction.Length();
            return length > 1e-6f ? direction / length : WhiteLab;
        }

        private static Vector3 ComputeChromaDirection(Vector3 backgroundLab)
        {
            float chromaLenSq = backgroundLab.Y * backgroundLab.Y + backgroundLab.Z * backgroundLab.Z;
            if (chromaLenSq <= 1e-12f)
                return new Vector3(0f, 0f, 0f);
            return new Vector3(0f, backgroundLab.Y, backgroundLab.Z);
        }

        private static float ComputePhysicalLambda(Vector3 backgroundLab, Vector3 keyDirection, float floorValue)
        {
            const float searchCeiling = 2f;

            if (!IsInGamut(OklabToLinear(backgroundLab + keyDirection * 1e-4f)))
                return MathF.Max(floorValue, 1e-5f);

            float low = 1e-4f;
            float high = searchCeiling;

            if (IsInGamut(OklabToLinear(backgroundLab + keyDirection * high)))
                return high;

            for (int iteration = 0; iteration < 40; iteration++)
            {
                float mid = 0.5f * (low + high);
                if (IsInGamut(OklabToLinear(backgroundLab + keyDirection * mid)))
                    low = mid;
                else
                    high = mid;
            }

            return MathF.Max(low, MathF.Max(floorValue, 1e-5f));
        }

        private static bool IsInGamut(Vector3 linear)
        {
            const float epsilon = 1e-4f;
            return linear.X >= -epsilon && linear.X <= 1f + epsilon
                && linear.Y >= -epsilon && linear.Y <= 1f + epsilon
                && linear.Z >= -epsilon && linear.Z <= 1f + epsilon;
        }

        private static Vector3 ToLinear(Color color)
        {
            return new Vector3(
                SrgbToLinear(color.R / 255f),
                SrgbToLinear(color.G / 255f),
                SrgbToLinear(color.B / 255f));
        }

        private static float SrgbToLinear(float c)
            => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static Vector3 ToOklab(Vector3 c)
        {
            float l = 0.4122214708f * c.X + 0.5363325363f * c.Y + 0.0514459929f * c.Z;
            float m = 0.2119034982f * c.X + 0.6806995451f * c.Y + 0.1073969566f * c.Z;
            float s = 0.0883024619f * c.X + 0.2817188376f * c.Y + 0.6299787005f * c.Z;

            float l_ = MathF.Cbrt(l);
            float m_ = MathF.Cbrt(m);
            float s_ = MathF.Cbrt(s);

            return new Vector3(
                0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
                1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
                0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_);
        }

        private static Vector3 OklabToLinear(Vector3 lab)
        {
            float l_ = lab.X + 0.3963377774f * lab.Y + 0.2158037573f * lab.Z;
            float m_ = lab.X - 0.1055613458f * lab.Y - 0.0638541728f * lab.Z;
            float s_ = lab.X - 0.0894841775f * lab.Y - 1.2914855480f * lab.Z;

            float l = l_ * l_ * l_;
            float m = m_ * m_ * m_;
            float s = s_ * s_ * s_;

            return new Vector3(
                 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
                -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
                -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
        }
    }
}
