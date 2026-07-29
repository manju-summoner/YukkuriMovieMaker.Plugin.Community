using System;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;

namespace YukkuriMovieMaker.Plugin.Community.Transition.OpenFx
{
    /// <summary>
    /// OpenFX場面切り替えの描画処理。
    /// YMM4のGPUパイプライン（ID2D1Image）とOFXのCPUレンダリングを橋渡しする：
    /// before/afterの2入力をGPUビットマップへ描画 → CPU読み出し → OFXプラグイン（トランジションコンテキスト）で
    /// レンダリング → 結果をビットマップへ書き戻して出力ノード（AffineTransform2D）に接続する。
    /// 進行度（緩急適用後）は毎フレーム Transition パラメータへ設定する。
    /// プラグイン未選択・読み込み失敗時は進行50%を境に before / after を素通しする。
    /// </summary>
    internal sealed unsafe class OpenFxTransitionSource : ITransitionSource
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly ID2D1Image before;
        readonly ID2D1Image after;
        readonly OpenFxTransitionParameter item;

        readonly AffineTransform2D transformEffect;
        readonly ID2D1Image transformOutput;
        // 素通し表示の状態（passthroughInputはtransformEffectへ接続中の素通し入力。nullなら出力ビットマップ接続中）
        ID2D1Image? passthroughInput;

        // GPU↔CPU転送用リソース（サイズ変更時に作り直す）
        ID2D1Bitmap1? gpuBitmap;
        ID2D1Bitmap1? cpuBitmap;
        int inputBitmapWidth;
        int inputBitmapHeight;
        byte[] fromBuffer = [];
        byte[] toBuffer = [];
        // 出力はRoD（プラグイン宣言の定義域）サイズ。入力より大きくなるプラグインもある
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

        public ID2D1Image Output => transformOutput;

        public OpenFxTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, OpenFxTransitionParameter item)
        {
            this.devices = devices;
            this.before = before;
            this.after = after;
            this.item = item;

            transformEffect = new AffineTransform2D(devices.DeviceContext);
            transformOutput = transformEffect.Output;
            ApplyPassthrough(before);
            item.PropertyChanged += Item_PropertyChanged;
        }

        void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 値の変化はUIスレッド、消費はレンダリングスレッドのためvolatileフラグで受け渡す
            retryRequested = true;
        }

        void ITransitionSource.Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var rawProgress = (double)frame / length;
            var easedProgress = Easing.GetValue(item.EasingType, item.EasingMode, rawProgress);
            // 素通し時は進行50%を境に切り替える（トランジションの体感に最も近い代替表示）
            var passthrough = easedProgress < 0.5 ? before : after;

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
                ApplyPassthrough(passthrough);
                return;
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
                    ApplyPassthrough(passthrough);
                    return;
                }
                failureCooldownFrames = 0;
            }
            // ここから先は1回の試行として編集済みフラグを消費する（試行中の再編集は次のUpdateで再試行になる）
            retryRequested = false;

            // 2入力は同じ矩形へ描画してプラグインへ渡すため、両者を包含する矩形を使う
            var dc = devices.DeviceContext;
            var beforeBounds = dc.GetImageLocalBounds(before);
            var afterBounds = dc.GetImageLocalBounds(after);
            var left = Math.Min(beforeBounds.Left, afterBounds.Left);
            var top = Math.Min(beforeBounds.Top, afterBounds.Top);
            var right = Math.Max(beforeBounds.Right, afterBounds.Right);
            var bottom = Math.Max(beforeBounds.Bottom, afterBounds.Bottom);
            var bounds = new Rect(left, top, right - left, bottom - top);
            var width = (int)MathF.Ceiling(bounds.Right - bounds.Left);
            var height = (int)MathF.Ceiling(bounds.Bottom - bounds.Top);
            if (width <= 0 || height <= 0)
            {
                ApplyPassthrough(passthrough);
                return;
            }

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
                ApplyPassthrough(passthrough);
                return;
            }
            if (instance is null)
            {
                ApplyPassthrough(passthrough);
                return;
            }

            OfxRectI renderWindow;
            try
            {
                // パラメータ適用の失敗も描画失敗と同じクールダウン経路（パススルー＋ログ1回）に乗せる
                foreach (var parameter in item.Parameters)
                    parameter.ApplyTo(instance, frame, length, fps);
                // 進行度はトランジションコンテキストの必須パラメータとしてホストが駆動する
                instance.SetDoubleParam(OfxConstants.ImageEffectTransitionParamName, Math.Clamp(easedProgress, 0, 1));

                // 入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));
                EnsureInputResources(width, height);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                ReadInputPixels(dc, before, bounds, width, height, fromBuffer);
                ReadInputPixels(dc, after, bounds, width, height, toBuffer);
                if (isRenderUnsafe)
                {
                    lock (OfxEffectInstance.UnsafeRenderLock)
                        instance.RenderTransition(fromBuffer, toBuffer, outputBuffer, frame, renderWindow);
                }
                else
                {
                    instance.RenderTransition(fromBuffer, toBuffer, outputBuffer, frame, renderWindow);
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
                ApplyPassthrough(passthrough);
                return;
            }

            hasLoggedFailure = false;
            // OFX座標（左下原点）のRoDをD2D座標（上から下）の配置へ変換する
            transformEffect.SetInput(0, outputBitmap, true);
            transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(
                bounds.Left + renderWindow.x1,
                bounds.Top + (height - renderWindow.y2));
            passthroughInput = null;
        }

        void ApplyPassthrough(ID2D1Image input)
        {
            if (ReferenceEquals(passthroughInput, input))
                return;
            transformEffect.SetInput(0, input, true);
            transformEffect.TransformMatrix = Matrix3x2.Identity;
            passthroughInput = input;
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
            var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextTransition);
            isRenderUnsafe = descriptor.Props.GetStringOrDefault(
                OfxConstants.ImageEffectPluginRenderThreadSafety,
                OfxConstants.ImageEffectRenderFullySafe) == OfxConstants.ImageEffectRenderUnsafe;
            var created = new OfxEffectInstance(plugin, OfxConstants.ImageEffectContextTransition, width, height, fps, durationFrames);
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
            var bufferSize = width * height * 4;
            if (fromBuffer.Length < bufferSize)
                fromBuffer = new byte[bufferSize];
            if (toBuffer.Length < bufferSize)
                toBuffer = new byte[bufferSize];
            if (inputBitmapWidth == width && inputBitmapHeight == height && gpuBitmap is not null)
                return;
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap?.Dispose();
            cpuBitmap = null;

            var dc = devices.DeviceContext;
            // boundsはDIP・SizeIはピクセルのため、DPIは96固定にして1DIP=1pxで扱う
            // （OpenFxVideoEffectProcessorと同じ流儀。dc.Dpiを渡すと高DPI環境で配置がずれる）
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
        }

        void EnsureOutputResources(int width, int height)
        {
            if (outputBitmapWidth == width && outputBitmapHeight == height && outputBitmap is not null)
                return;
            // 差し替え前の出力ビットマップがエフェクト入力に残ったまま破棄しない
            transformEffect.SetInput(0, null, true);
            passthroughInput = null;
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

        void ReadInputPixels(ID2D1DeviceContext dc, ID2D1Image input, Rect bounds, int width, int height, byte[] destinationBuffer)
        {
            // 呼び出し元が設定した描画先を壊さないよう退避して復元する
            var previousTarget = dc.Target;
            try
            {
                dc.Target = gpuBitmap;
                dc.BeginDraw();
                dc.Clear(new Color4(0f, 0f, 0f, 0f));
                dc.DrawImage(
                    input,
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
                fixed (byte* destination = destinationBuffer)
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

        void IDisposable.Dispose()
        {
            item.PropertyChanged -= Item_PropertyChanged;
            // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
            transformEffect.SetInput(0, null, true);
            instance?.Dispose();
            instance = null;
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap?.Dispose();
            cpuBitmap = null;
            outputBitmap?.Dispose();
            outputBitmap = null;
            transformOutput.Dispose();
            transformEffect.Dispose();
        }
    }
}
