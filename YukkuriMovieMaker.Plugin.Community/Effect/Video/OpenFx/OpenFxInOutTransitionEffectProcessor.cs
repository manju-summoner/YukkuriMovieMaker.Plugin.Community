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
    /// 「場面切り替え（OpenFX）」の描画処理。
    /// 本体の「場面切り替え」（InOutTransitionEffect）と同じ時間制御でrate（表示率0～1）を求め、
    /// アイテム画像と透過画像の2入力でOFXトランジションを駆動する：
    /// 登場時は 透明→アイテム（進行度=rate）、退場時は アイテム→透明（進行度=1-rate）。
    /// 反転はfrom/toの役割と進行度を同時反転する（＝トランジションの逆再生）。
    /// 効果時間外・プラグイン未選択・失敗時は入力を素通しする。
    /// </summary>
    internal sealed unsafe class OpenFxInOutTransitionEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly OpenFxInOutTransitionEffect item;

        AffineTransform2D? transformEffect;
        ID2D1Image? currentInput;
        bool isPassthroughApplied;

        // 透過側の入力。中身が常にゼロで書き換えられない（RenderTransitionの入力はReadOnlySpan）ため、
        // プロセス全体で1本のgrow-onlyゼロ配列を共有してインスタンスごとの確保を避ける
        static byte[] sharedTransparentBuffer = [];

        static byte[] GetSharedTransparentBuffer(int minLength)
        {
            // CASループで常に大きい方の配列を残す（同時要求のサイズ競合で小さい方が後勝ちすると
            // 次のフレームで再確保が走るため）。各スレッドは必ず要求長以上のゼロ配列を受け取る
            while (true)
            {
                var current = sharedTransparentBuffer;
                if (current.Length >= minLength)
                    return current;
                var grown = new byte[minLength];
                if (System.Threading.Interlocked.CompareExchange(ref sharedTransparentBuffer, grown, current) == current)
                    return grown;
            }
        }

        // GPU↔CPU転送用リソース（サイズ変更時に作り直す）
        ID2D1Bitmap1? gpuBitmap;
        ID2D1Bitmap1? cpuBitmap;
        int inputBitmapWidth;
        int inputBitmapHeight;
        byte[] sourceBuffer = [];
        // 出力はRoD（プラグイン宣言の定義域）サイズ
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
        // 効果時間外の素通しが続いたら重いネイティブリソースを解放する
        // （長いアイテムでは効果時間外が大半を占め、OFXのプール画像・変換バッファを保持し続けるのは無駄なため）
        int passthroughFrames;
        const int PassthroughReleaseFrameCount = 120;

        public OpenFxInOutTransitionEffectProcessor(IGraphicsDevicesAndContext devices, OpenFxInOutTransitionEffect item)
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
                // 選択解除されたらネイティブリソースを保持し続けない。
                // 失敗状態も併せてリセットする（残すと同じプラグインの再選択がエッジ検出されず、
                // クールダウンの残りフレームが素通しのまま消化されてしまう）
                ApplyPassthrough();
                ReleaseRenderResources();
                attemptedPluginPath = "";
                attemptedPluginId = "";
                failureCooldownFrames = 0;
                hasLoggedFailure = false;
                return effectDescription.DrawDescription;
            }

            // 本体の「場面切り替え」（InOutTransitionEffect）と同じ時間制御。
            // rate=1（効果時間外・両効果無効）は素通しにする
            var time = effectDescription.ItemPosition.Time;
            var totalTime = effectDescription.ItemDuration.Time;
            var span = item.EffectTimeSeconds;
            var inRate = item.IsInEffect && span > 0 ? Math.Clamp(time.TotalSeconds / span, 0, 1) : 1;
            var outRate = item.IsOutEffect && span > 0 ? Math.Clamp((totalTime - time).TotalSeconds / span, 0, 1) : 1;
            double rate;
            bool isOut = false;
            bool reversed = false;
            if (time.TotalSeconds < span && item.IsInEffect && inRate < outRate)
            {
                rate = Easing.GetValue(item.EasingType, item.EasingMode, time.TotalSeconds / span);
                reversed = item.IsReversedInEffect;
            }
            else if ((totalTime - time).TotalSeconds < span && item.IsOutEffect && outRate < inRate)
            {
                rate = Easing.GetValue(item.EasingType, item.EasingMode, (totalTime - time).TotalSeconds / span);
                isOut = true;
                reversed = item.IsReversedOutEffect;
            }
            else
            {
                rate = 1;
            }
            if (rate >= 1)
            {
                // 効果時間外もクールダウンを減衰させる（失敗スロットリングの意味を毎フレーム減算に揃える）
                if (failureCooldownFrames > 0)
                    failureCooldownFrames--;
                ApplyPassthrough();
                // 素通しが続いたら重いネイティブリソースを解放する（次に効果時間へ入ったとき作り直される。
                // カウンタは閾値で頭打ちにして解放は1回だけ行う）
                if (passthroughFrames < PassthroughReleaseFrameCount)
                {
                    passthroughFrames++;
                    if (passthroughFrames == PassthroughReleaseFrameCount)
                        ReleaseRenderResources();
                }
                return effectDescription.DrawDescription;
            }
            passthroughFrames = 0;

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

            // 登場時は 透明→アイテム（進行度=rate）、退場時は アイテム→透明（進行度=1-rate）。
            // 反転時はfrom/toと進行度を同時に入れ替える（＝トランジションの逆再生）
            var isItemToTransparent = isOut ^ reversed;
            var progress = Math.Clamp(isItemToTransparent ? 1 - rate : rate, 0, 1);

            OfxRectI renderWindow;
            try
            {
                // パラメータ適用の失敗も描画失敗と同じクールダウン経路（パススルー＋ログ1回）に乗せる
                foreach (var parameter in item.Parameters)
                    parameter.ApplyTo(instance, frame, length, fps);
                instance.SetDoubleParam(OfxConstants.ImageEffectTransitionParamName, progress);

                // 入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));
                EnsureInputResources(width, height);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                ReadInputPixels(dc, bounds, width, height);
                var transparentBuffer = GetSharedTransparentBuffer(width * height * 4);
                var fromBuffer = isItemToTransparent ? sourceBuffer : transparentBuffer;
                var toBuffer = isItemToTransparent ? transparentBuffer : sourceBuffer;
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

        /// <summary>
        /// OFXインスタンスと転送用リソースをまとめて解放する（素通し継続・選択解除・破棄時）。
        /// outputBitmapをエフェクト入力に接続したまま破棄しないよう、先頭で素通し状態へ切り替える
        /// </summary>
        void ReleaseRenderResources()
        {
            // 呼び出し元の順序に依存せず接続中破棄を防ぐ（冪等。Dispose経路で一瞬currentInputが
            // 再接続されても、直後にbase.DisposeのClearEffectChainが切り離すため安全）
            ApplyPassthrough();
            instance?.Dispose();
            instance = null;
            instancePluginPath = "";
            instancePluginId = "";
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap?.Dispose();
            cpuBitmap = null;
            outputBitmap?.Dispose();
            outputBitmap = null;
            inputBitmapWidth = 0;
            inputBitmapHeight = 0;
            outputBitmapWidth = 0;
            outputBitmapHeight = 0;
            sourceBuffer = [];
            outputBuffer = [];
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
            if (sourceBuffer.Length < bufferSize)
                sourceBuffer = new byte[bufferSize];
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
                transformEffect?.SetInput(0, null, true);
                ReleaseRenderResources();
            }
            base.Dispose(disposing);
        }
    }
}
