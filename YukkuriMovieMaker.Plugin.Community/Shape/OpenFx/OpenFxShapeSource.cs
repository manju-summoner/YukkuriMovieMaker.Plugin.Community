using System;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;

namespace YukkuriMovieMaker.Plugin.Community.Shape.OpenFx
{
    /// <summary>
    /// 図形「OpenFX」の描画処理。
    /// ジェネレーターコンテキストのOFXプラグインを入力なしでCPUレンダリングし、
    /// 結果をビットマップへ書き込んで出力ノード（AffineTransform2D）に接続する。
    /// 出力はユーザー指定サイズ（＝OFXのプロジェクトサイズ）を原点中心に配置する。
    /// プラグイン未選択・読み込み失敗時は透明画像を出力する
    /// </summary>
    internal sealed unsafe class OpenFxShapeSource : IShapeSource
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly OpenFxShapeParameter parameter;

        readonly AffineTransform2D transformEffect;
        readonly ID2D1Image output;
        readonly ID2D1Bitmap1 emptyBitmap;
        bool isEmptyApplied;

        // 出力はRoD（プラグイン宣言の定義域）サイズ。プロジェクトサイズより大きくなり得る
        ID2D1Bitmap1? outputBitmap;
        int outputBitmapWidth;
        int outputBitmapHeight;
        byte[] outputBuffer = [];

        OfxEffectInstance? instance;
        string instancePluginPath = "";
        string instancePluginId = "";
        // 失敗状態のリセット判定用。「最後に試行した」プラグイン（instance側は成功時にしか更新されない）
        string attemptedPluginPath = "";
        string attemptedPluginId = "";
        bool isRenderUnsafe;
        // 失敗時は一定フレーム透明にしてから再試行する（ログはひと続きの失敗で1回だけ）。
        // ユーザーがパラメーター等を編集したら待ちを打ち切って即再試行する（原因を直しても透明のままにならないように）
        int failureCooldownFrames;
        bool hasLoggedFailure;
        volatile bool retryRequested;
        const int FailureCooldownFrameCount = 120;

        // 出力の総ピクセル数の上限（8K・7680x4320までは等倍で許容）。
        // 1辺のクランプだけでは16384x16384のような指定でRGBA floatの中間バッファが数GiBに達するため
        internal const long MaxOutputPixels = 8192L * 4096L;

        public ID2D1Image Output => output;

        public OpenFxShapeSource(IGraphicsDevicesAndContext devices, OpenFxShapeParameter parameter)
        {
            this.devices = devices;
            this.parameter = parameter;
            parameter.PropertyChanged += Parameter_PropertyChanged;

            var dc = devices.DeviceContext;
            transformEffect = new AffineTransform2D(dc);
            output = transformEffect.Output;

            // 未選択・失敗時に接続する透明画像（1x1）
            var properties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.None);
            emptyBitmap = dc.CreateBitmap(new SizeI(1, 1), properties);
            // SkipLocalsInit導入時もゼロ初期化が保たれるよう明示する
            var transparent = stackalloc byte[] { 0, 0, 0, 0 };
            emptyBitmap.CopyFromMemory((nint)transparent, 4);

            ApplyEmpty();
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            if (string.IsNullOrEmpty(parameter.PluginPath) || string.IsNullOrEmpty(parameter.PluginId))
            {
                // 選択解除されたらネイティブの出力バッファを保持し続けない。
                // 失敗状態も併せてリセットする（残すと同じプラグインの再選択がエッジ検出されず、
                // クールダウンの残りフレームが透明のまま消化されてしまう）
                instance?.Dispose();
                instance = null;
                instancePluginPath = "";
                instancePluginId = "";
                attemptedPluginPath = "";
                attemptedPluginId = "";
                failureCooldownFrames = 0;
                hasLoggedFailure = false;
                ApplyEmpty();
                // 出力ビットマップ（最大8K相当）とバッファも解放する。
                // ApplyEmptyで入力はemptyBitmapへ切り替わっているため、エフェクト入力に接続したままの破棄にはならない
                outputBitmap?.Dispose();
                outputBitmap = null;
                outputBitmapWidth = 0;
                outputBitmapHeight = 0;
                outputBuffer = [];
                return;
            }

            // プラグインが切り替わったら失敗状態を即座にリセットする（クールダウン中の切替でも待たせない）
            if (!string.Equals(attemptedPluginPath, parameter.PluginPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(attemptedPluginId, parameter.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                attemptedPluginPath = parameter.PluginPath;
                attemptedPluginId = parameter.PluginId;
                failureCooldownFrames = 0;
                hasLoggedFailure = false;
            }

            // 直近で失敗した場合は一定フレーム透明にしてから再試行する（毎フレームの失敗連打を避ける）。
            // パラメーター等が編集された場合は待ちを打ち切って即再試行する
            if (failureCooldownFrames > 0)
            {
                if (!retryRequested)
                {
                    failureCooldownFrames--;
                    ApplyEmpty();
                    return;
                }
                failureCooldownFrames = 0;
            }
            // ここから先は1回の試行として編集済みフラグを消費する（試行中の再編集は次のUpdateで再試行になる）
            retryRequested = false;

            var dc = devices.DeviceContext;
            var (rawWidth, rawHeight) = parameter.GetOutputSize(frame, length, fps);
            // D2Dビットマップの上限を超えるサイズは生成できないためクランプする
            var maxSize = Math.Max(1, (int)dc.MaximumBitmapSize);
            var width = (int)Math.Clamp(Math.Round(rawWidth), 0, maxSize);
            var height = (int)Math.Clamp(Math.Round(rawHeight), 0, maxSize);
            if (width <= 0 || height <= 0)
            {
                ApplyEmpty();
                return;
            }
            // 総ピクセル数も制限する（超えたら縦横比を保って縮小）
            if ((long)width * height > MaxOutputPixels)
            {
                var scale = Math.Sqrt((double)MaxOutputPixels / ((long)width * height));
                width = Math.Max(1, (int)(width * scale));
                height = Math.Max(1, (int)(height * scale));
            }

            try
            {
                EnsureInstance(width, height, fps, length);
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインの読み込みに失敗しました。id={parameter.PluginId} path={parameter.PluginPath}", e);
                hasLoggedFailure = true;
                failureCooldownFrames = FailureCooldownFrameCount;
                ApplyEmpty();
                return;
            }
            if (instance is null)
            {
                ApplyEmpty();
                return;
            }

            OfxRectI renderWindow;
            try
            {
                // パラメータ適用の失敗も描画失敗と同じクールダウン経路（透明出力＋ログ1回）に乗せる
                foreach (var ofxParameter in parameter.Parameters)
                    ofxParameter.ApplyTo(instance, frame, length, fps);

                // ジェネレーターもRoD（定義域）を宣言できるため、出力はRoDサイズで確保する
                // （指定サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す）
                renderWindow = instance.GetRegionOfDefinition(frame, maxSize);
                // RoD拡張（±1024px）が乗ると総ピクセル上限を超え得るため、renderWindowにも上限を適用する
                renderWindow = ClampRenderWindowArea(renderWindow, width, height, MaxOutputPixels);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                if (isRenderUnsafe)
                {
                    lock (OfxEffectInstance.UnsafeRenderLock)
                        instance.RenderGenerator(outputBuffer, frame, renderWindow);
                }
                else
                {
                    instance.RenderGenerator(outputBuffer, frame, renderWindow);
                }
                fixed (byte* outputPointer = outputBuffer)
                {
                    outputBitmap!.CopyFromMemory((nint)outputPointer, outputBitmapWidth * 4);
                }
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインのレンダリングに失敗しました。id={parameter.PluginId}", e);
                hasLoggedFailure = true;
                failureCooldownFrames = FailureCooldownFrameCount;
                ApplyEmpty();
                return;
            }

            hasLoggedFailure = false;
            // OFX座標（左下原点）のRoDを、プロジェクトサイズの矩形が原点中心になるD2D座標へ変換する。
            // 奇数サイズで平行移動が半ピクセルになると既定の線形補間でにじむため、整数へ丸めて等倍コピーにする
            // （Roundは銀行家丸めで値により方向が変わるためFloorで統一する）
            transformEffect.SetInput(0, outputBitmap, true);
            transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(
                MathF.Floor(-width / 2f + renderWindow.x1),
                MathF.Floor(-height / 2f + (height - renderWindow.y2)));
            isEmptyApplied = false;
        }

        /// <summary>
        /// renderWindow（RoD）の総ピクセル数を上限に収める。
        /// プロジェクト矩形（width x height）は上限内に収まっている前提で、
        /// 拡張分（余白）を両軸同率で縮めてから各軸をプロジェクト矩形優先で切り詰める
        /// </summary>
        internal static OfxRectI ClampRenderWindowArea(OfxRectI renderWindow, int width, int height, long maxPixels)
        {
            var spanX = (long)renderWindow.x2 - renderWindow.x1;
            var spanY = (long)renderWindow.y2 - renderWindow.y1;
            if (spanX * spanY <= maxPixels)
                return renderWindow;

            // (width + t*ex) * (height + t*ey) = maxPixels を満たす縮小率 t∈[0,1] を解く（exとeyは拡張分）
            var ex = (double)Math.Max(0, spanX - width);
            var ey = (double)Math.Max(0, spanY - height);
            var a = ex * ey;
            var b = width * ey + height * ex;
            var c = (double)width * height - maxPixels;
            // 前提（プロジェクト矩形が上限内＝c<0）が破れた場合も、平方根のNaNを伝播させず拡張分ゼロへ倒す
            var t = c >= 0 ? 0
                : a > 0 ? (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a)
                : b > 0 ? -c / b : 0;
            t = Math.Clamp(t, 0, 1);
            var limitX = width + (int)Math.Floor(t * ex);
            var limitY = height + (int)Math.Floor(t * ey);
            OfxEffectInstance.ClampSpan(ref renderWindow.x1, ref renderWindow.x2, width, limitX);
            OfxEffectInstance.ClampSpan(ref renderWindow.y1, ref renderWindow.y2, height, limitY);
            return renderWindow;
        }

        void ApplyEmpty()
        {
            if (isEmptyApplied)
                return;
            transformEffect.SetInput(0, emptyBitmap, true);
            // 透明画像も原点中心に配置する（左上1pxに寄るとアイテム枠の位置がずれるため）
            transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(-0.5f, -0.5f);
            isEmptyApplied = true;
        }

        void EnsureInstance(int width, int height, int fps, int durationFrames)
        {
            // 失敗状態のリセットはUpdate側のエッジ検出で行う（ここで毎回リセットすると
            // クールダウン明けの再試行のたびにログが出てしまう）
            var isSamePlugin =
                string.Equals(instancePluginPath, parameter.PluginPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(instancePluginId, parameter.PluginId, StringComparison.OrdinalIgnoreCase);
            // fps・アイテム長もインスタンス生成時にプラグインへ伝わるため、変わったら作り直す
            if (instance is not null
                && (!isSamePlugin
                    || instance.Width != width
                    || instance.Height != height
                    || instance.FrameRate != fps
                    || instance.DurationFrames != durationFrames))
            {
                instance.Dispose();
                instance = null;
            }
            if (instance is not null)
                return;

            var plugin = OpenFxPluginScanner.LoadPlugin(parameter.PluginPath, parameter.PluginId)
                ?? throw new InvalidOperationException($"OFXプラグインが見つかりません。id={parameter.PluginId} path={parameter.PluginPath}");
            var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextGenerator);
            isRenderUnsafe = descriptor.Props.GetStringOrDefault(
                OfxConstants.ImageEffectPluginRenderThreadSafety,
                OfxConstants.ImageEffectRenderFullySafe) == OfxConstants.ImageEffectRenderUnsafe;
            var created = new OfxEffectInstance(plugin, OfxConstants.ImageEffectContextGenerator, width, height, fps, durationFrames);
            try
            {
                created.Create();
            }
            catch
            {
                created.Dispose();
                throw;
            }
            instance = created;
            instancePluginPath = parameter.PluginPath;
            instancePluginId = parameter.PluginId;
        }

        void EnsureOutputResources(int width, int height)
        {
            if (outputBitmapWidth == width && outputBitmapHeight == height && outputBitmap is not null)
                return;
            // 差し替え前の出力ビットマップがエフェクト入力に残ったまま破棄しない
            transformEffect.SetInput(0, null, true);
            isEmptyApplied = false;
            outputBitmap?.Dispose();
            outputBitmap = null;

            var dc = devices.DeviceContext;
            var outputProperties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.None);
            outputBitmap = dc.CreateBitmap(new SizeI(width, height), outputProperties);
            outputBitmapWidth = width;
            outputBitmapHeight = height;

            var bufferSize = width * height * 4;
            if (outputBuffer.Length < bufferSize)
                outputBuffer = new byte[bufferSize];
        }

        void Parameter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 値の変化はUIスレッド、消費はレンダリングスレッドのためvolatileフラグで受け渡す
            retryRequested = true;
        }

        public void Dispose()
        {
            parameter.PropertyChanged -= Parameter_PropertyChanged;
            // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
            transformEffect.SetInput(0, null, true);
            instance?.Dispose();
            instance = null;
            outputBitmap?.Dispose();
            outputBitmap = null;
            emptyBitmap.Dispose();
            output.Dispose();
            transformEffect.Dispose();
        }
    }
}
