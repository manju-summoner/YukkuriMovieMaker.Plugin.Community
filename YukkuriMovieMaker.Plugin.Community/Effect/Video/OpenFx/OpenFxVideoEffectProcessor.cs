using System;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OpenFXエフェクトの描画処理。
    /// YMM4のGPUパイプライン（ID2D1Image）とOFXのCPUレンダリングを橋渡しする：
    /// 入力画像をGPUビットマップへ描画 → CPU読み出し → OFXプラグインでレンダリング →
    /// 結果をビットマップへ書き戻して出力ノード（AffineTransform2D）に接続する。
    /// プラグイン未選択・読み込み失敗時は入力を素通しする。
    /// </summary>
    internal sealed unsafe class OpenFxVideoEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly OpenFxVideoEffect item;

        AffineTransform2D? transformEffect;
        ID2D1Image? currentInput;
        bool isPassthroughApplied;

        // GPU↔CPU転送用リソース（サイズ変更時に作り直す）
        ID2D1Bitmap1? gpuBitmap;
        ID2D1Bitmap1? cpuBitmap;
        int inputBitmapWidth;
        int inputBitmapHeight;
        byte[] sourceBuffer = [];
        // 出力はRoD（プラグイン宣言の定義域）サイズ。ぼかし等では入力より大きくなる
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
        // 失敗時は一定フレーム素通しにしてから再試行する（ログはひと続きの失敗で1回だけ）。
        // ユーザーがパラメーター等を編集したら待ちを打ち切って即再試行する（原因を直しても素通しのままにならないように）
        int failureCooldownFrames;
        bool hasLoggedFailure;
        volatile bool retryRequested;
        const int FailureCooldownFrameCount = 120;

        public OpenFxVideoEffectProcessor(IGraphicsDevicesAndContext devices, OpenFxVideoEffect item)
            : base(devices)
        {
            this.devices = devices;
            this.item = item;
            item.PropertyChanged += Item_PropertyChanged;
        }

        void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 値の変化はUIスレッド、消費はレンダリングスレッドのためvolatileフラグで受け渡す
            retryRequested = true;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            transformEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transformEffect);
            var output = transformEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            currentInput = input;
            // 既定は素通し。処理する場合はUpdateで出力ビットマップへ差し替える
            if (transformEffect is not null)
            {
                transformEffect.SetInput(0, input, true);
                transformEffect.TransformMatrix = Matrix3x2.Identity;
            }
            isPassthroughApplied = true;
        }

        protected override void ClearEffectChain()
        {
            currentInput = null;
            transformEffect?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (transformEffect is null || currentInput is null)
                return effectDescription.DrawDescription;

            if (string.IsNullOrEmpty(item.PluginPath) || string.IsNullOrEmpty(item.PluginId))
            {
                // 選択解除されたらネイティブの入出力バッファを保持し続けない。
                // 失敗状態も併せてリセットする（残すと同じプラグインの再選択がエッジ検出されず、
                // クールダウンの残りフレームが素通しのまま消化されてしまう）
                instance?.Dispose();
                instance = null;
                instancePluginPath = "";
                instancePluginId = "";
                attemptedPluginPath = "";
                attemptedPluginId = "";
                failureCooldownFrames = 0;
                hasLoggedFailure = false;
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }

            // プラグインが切り替わったら失敗状態を即座にリセットする（クールダウン中の切替でも待たせない）。
            // 読み込みに失敗し続けているプラグインで毎フレームリセットしないよう、「最後に試行した」識別子で
            // エッジ検出する（instancePluginPath/Idは成功時にしか更新されないため判定に使えない）
            if (!string.Equals(attemptedPluginPath, item.PluginPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(attemptedPluginId, item.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                attemptedPluginPath = item.PluginPath;
                attemptedPluginId = item.PluginId;
                failureCooldownFrames = 0;
                hasLoggedFailure = false;
            }

            // 直近で失敗した場合は一定フレーム素通しにしてから再試行する（毎フレームの失敗連打を避ける）。
            // パラメーター等が編集された場合は待ちを打ち切って即再試行する
            if (failureCooldownFrames > 0)
            {
                if (!retryRequested)
                {
                    failureCooldownFrames--;
                    ApplyPassthrough();
                    return effectDescription.DrawDescription;
                }
                failureCooldownFrames = 0;
            }
            // ここから先は1回の試行として編集済みフラグを消費する（試行中の再編集は次のUpdateで再試行になる）
            retryRequested = false;

            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(currentInput);
            var width = (int)MathF.Ceiling(bounds.Right - bounds.Left);
            var height = (int)MathF.Ceiling(bounds.Bottom - bounds.Top);
            if (width <= 0 || height <= 0)
            {
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            try
            {
                EnsureInstance(width, height, fps, length);
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインの読み込みに失敗しました。id={item.PluginId} path={item.PluginPath}", e);
                hasLoggedFailure = true;
                failureCooldownFrames = FailureCooldownFrameCount;
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }
            if (instance is null)
            {
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }

            OfxRectI renderWindow;
            try
            {
                // パラメータ適用の失敗も描画失敗と同じクールダウン経路（パススルー＋ログ1回）に乗せる
                foreach (var parameter in item.Parameters)
                    parameter.ApplyTo(instance, frame, length, fps);

                // ぼかし・グロー等は入力より大きな出力領域（RoD）を宣言するため、出力はRoDサイズで確保する
                // （入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す）
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));
                EnsureInputResources(width, height);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                ReadInputPixels(dc, bounds, width, height);
                if (isRenderUnsafe)
                {
                    lock (OfxEffectInstance.UnsafeRenderLock)
                        instance.Render(sourceBuffer, outputBuffer, frame, renderWindow);
                }
                else
                {
                    instance.Render(sourceBuffer, outputBuffer, frame, renderWindow);
                }
                fixed (byte* outputPointer = outputBuffer)
                {
                    outputBitmap!.CopyFromMemory((nint)outputPointer, outputBitmapWidth * 4);
                }
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインのレンダリングに失敗しました。id={item.PluginId}", e);
                hasLoggedFailure = true;
                failureCooldownFrames = FailureCooldownFrameCount;
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }

            hasLoggedFailure = false;
            // OFX座標（左下原点）のRoDをD2D座標（上から下）の配置へ変換する
            transformEffect.SetInput(0, outputBitmap, true);
            transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(
                bounds.Left + renderWindow.x1,
                bounds.Top + (height - renderWindow.y2));
            isPassthroughApplied = false;
            return effectDescription.DrawDescription;
        }

        void ApplyPassthrough()
        {
            if (isPassthroughApplied || transformEffect is null)
                return;
            transformEffect.SetInput(0, currentInput, true);
            transformEffect.TransformMatrix = Matrix3x2.Identity;
            isPassthroughApplied = true;
        }

        void EnsureInstance(int width, int height, int fps, int durationFrames)
        {
            // 失敗状態のリセットはUpdate側のエッジ検出で行う（ここで毎回リセットすると
            // クールダウン明けの再試行のたびにログが出てしまう）
            var isSamePlugin =
                string.Equals(instancePluginPath, item.PluginPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(instancePluginId, item.PluginId, StringComparison.OrdinalIgnoreCase);
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

            var plugin = OpenFxPluginScanner.LoadPlugin(item.PluginPath, item.PluginId)
                ?? throw new InvalidOperationException($"OFXプラグインが見つかりません。id={item.PluginId} path={item.PluginPath}");
            var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextFilter);
            isRenderUnsafe = descriptor.Props.GetStringOrDefault(
                OfxConstants.ImageEffectPluginRenderThreadSafety,
                OfxConstants.ImageEffectRenderFullySafe) == OfxConstants.ImageEffectRenderUnsafe;
            var created = new OfxEffectInstance(plugin, OfxConstants.ImageEffectContextFilter, width, height, fps, durationFrames);
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
            instancePluginPath = item.PluginPath;
            instancePluginId = item.PluginId;
        }

        void EnsureInputResources(int width, int height)
        {
            if (inputBitmapWidth == width && inputBitmapHeight == height && gpuBitmap is not null)
                return;
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap?.Dispose();
            cpuBitmap = null;

            var dc = devices.DeviceContext;
            // boundsはDIP・SizeIはピクセルのため、DPIは96固定にして1DIP=1pxで扱う
            // （DirectionalColorKeyEffectProcessorと同じ流儀。dc.Dpiを渡すと高DPI環境で配置がずれる）
            var gpuProperties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.Target);
            var cpuProperties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            gpuBitmap = dc.CreateBitmap(new SizeI(width, height), gpuProperties);
            cpuBitmap = dc.CreateBitmap(new SizeI(width, height), cpuProperties);
            inputBitmapWidth = width;
            inputBitmapHeight = height;

            var bufferSize = width * height * 4;
            if (sourceBuffer.Length < bufferSize)
                sourceBuffer = new byte[bufferSize];
        }

        void EnsureOutputResources(int width, int height)
        {
            if (outputBitmapWidth == width && outputBitmapHeight == height && outputBitmap is not null)
                return;
            // 差し替え前の出力ビットマップがエフェクト入力に残ったまま破棄しない
            transformEffect?.SetInput(0, null, true);
            isPassthroughApplied = false;
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

        void ReadInputPixels(ID2D1DeviceContext dc, Rect bounds, int width, int height)
        {
            // 呼び出し元が設定した描画先を壊さないよう退避して復元する
            var previousTarget = dc.Target;
            try
            {
                dc.Target = gpuBitmap;
                dc.BeginDraw();
                dc.Clear(new Color4(0f, 0f, 0f, 0f));
                dc.DrawImage(
                    currentInput!,
                    new Vector2(-bounds.Left, -bounds.Top),
                    null,
                    InterpolationMode.NearestNeighbor,
                    CompositeMode.SourceCopy);
                dc.EndDraw();
            }
            finally
            {
                dc.Target = previousTarget;
                previousTarget?.Dispose();
            }

            cpuBitmap!.CopyFromBitmap(gpuBitmap!);
            var map = cpuBitmap.Map(MapOptions.Read);
            try
            {
                var source = (byte*)map.Bits;
                fixed (byte* destination = sourceBuffer)
                {
                    var rowBytes = width * 4;
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(
                            source + (long)y * map.Pitch,
                            destination + (long)y * rowBytes,
                            rowBytes,
                            rowBytes);
                    }
                }
            }
            finally
            {
                cpuBitmap.Unmap();
            }
        }

        void DisposeBitmaps()
        {
            gpuBitmap?.Dispose();
            cpuBitmap?.Dispose();
            outputBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap = null;
            outputBitmap = null;
            inputBitmapWidth = 0;
            inputBitmapHeight = 0;
            outputBitmapWidth = 0;
            outputBitmapHeight = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
                transformEffect?.SetInput(0, null, true);
                instance?.Dispose();
                instance = null;
                DisposeBitmaps();
            }
            base.Dispose(disposing);
        }
    }
}
