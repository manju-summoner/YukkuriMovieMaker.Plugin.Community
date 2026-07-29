using System;
using System.Collections.Generic;
using System.Linq;
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
        // 直近の失敗時の試行入力（プラグイン・サイズ・fps/アイテム長・進行度・OFXパラメータ評価値。nullなら失敗状態ではない）。
        // 同じ入力での再試行は同じ失敗を繰り返すだけのため、入力が変わるまで素通しのまま試行しない。
        // レンダリング失敗はOFX時刻（フレーム）・入力画像でも結果が変わり得るため、failedAttemptFrameが一致する間だけ抑止する
        // （読み込み失敗はフレーム非依存＝null。ログはひと続きの失敗で1回だけ）
        object?[]? failedAttemptValues;
        int? failedAttemptFrame;
        bool hasLoggedFailure;
        int attemptCount;
        // 試行入力の収集バッファ（毎フレームのList/配列割り当てを避けるため再利用する。Updateスレッドでのみ使用）
        readonly List<object?> attemptValuesBuffer = [];
        // CollectAttemptValuesの先頭に並ぶ、プラグイン読み込みの成否に関わる値の個数
        // （プラグインパス/ID・インスタンスサイズ・fps/アイテム長。進行度は読み込みに影響しないため含めない）
        const int LoadRelevantValueCount = 6;
        // 効果時間外の素通しが続いたら重いネイティブリソースを解放する
        // （長いアイテムでは効果時間外が大半を占め、OFXのプール画像・変換バッファを保持し続けるのは無駄なため）
        int passthroughFrames;
        const int PassthroughReleaseFrameCount = 120;

        public OpenFxInOutTransitionEffectProcessor(IGraphicsDevicesAndContext devices, OpenFxInOutTransitionEffect item)
            : base(devices)
        {
            this.devices = devices;
            this.item = item;
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
                // 失敗状態も併せてリセットする（残すと同じプラグインを同じ設定で再選択したときに
                // 前回の失敗入力と一致して再試行されない。再選択は明示的な操作のため試行し直す）
                ApplyPassthrough();
                ReleaseRenderResources();
                attemptedPluginPath = "";
                attemptedPluginId = "";
                failedAttemptValues = null;
                failedAttemptFrame = null;
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

            // プラグインが切り替わったら失敗状態を即座にリセットする（失敗ログを新しいプラグインで出し直すため）。
            // 読み込みに失敗し続けているプラグインで毎フレームリセットしないよう、「最後に試行した」識別子で
            // エッジ検出する（instancePluginPath/Idは成功時にしか更新されないため判定に使えない）
            if (!string.Equals(attemptedPluginPath, item.PluginPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(attemptedPluginId, item.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                attemptedPluginPath = item.PluginPath;
                attemptedPluginId = item.PluginId;
                failedAttemptValues = null;
                failedAttemptFrame = null;
                hasLoggedFailure = false;
            }

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

            // 登場時は 透明→アイテム（進行度=rate）、退場時は アイテム→透明（進行度=1-rate）。
            // 反転時はfrom/toと進行度を同時に入れ替える（＝トランジションの逆再生）
            var isItemToTransparent = isOut ^ reversed;
            var progress = Math.Clamp(isItemToTransparent ? 1 - rate : rate, 0, 1);

            // 直近の失敗と同じ入力での再試行は同じ失敗を繰り返すだけのためスキップし、入力が変わったら即再試行する
            // （毎フレームの失敗連打を避けつつ、原因を直したときに素通しのまま固まらないように）。
            // 読み込み失敗（failedAttemptFrame=null）は読み込みの成否に関わる先頭の値だけで比較する
            // （進行度・OFXパラメータは読み込みに影響しない。毎フレーム変わる値で壊れたプラグインの
            //   ロードを毎フレーム再試行しないように）。レンダリング失敗はOFX時刻・入力画像でも結果が変わり得るため
            // 全値＋同一フレームの間だけ抑止する。スナップショットは試行前に採取したものを失敗時にそのまま保存する
            // （レンダリング中のUIスレッド編集を「試行済みで失敗」と誤記録しないため）。
            // パラメータリストはUIスレッドで差し替わり得るため1回だけ読み、スナップショットと適用で共有する
            var ofxParameters = item.Parameters;
            var canCompareAttempt = true;
            try
            {
                CollectAttemptValues(ofxParameters, width, height, fps, length, frame, progress, isItemToTransparent);
            }
            catch
            {
                // 試行値の評価自体が失敗する場合（Min>Max等の壊れたメタデータ）は比較不能＝毎回試行に倒す
                // （同じ計算を行うApplyToも失敗するため、レンダリング失敗経路のパススルー＋ログ1回に乗る）
                canCompareAttempt = false;
                attemptValuesBuffer.Clear();
            }
            if (canCompareAttempt && failedAttemptValues is not null)
            {
                var isSameFailedAttempt = failedAttemptFrame is null
                    ? attemptValuesBuffer.Take(failedAttemptValues.Length).SequenceEqual(failedAttemptValues)
                    : failedAttemptFrame == frame && attemptValuesBuffer.SequenceEqual(failedAttemptValues);
                if (isSameFailedAttempt)
                {
                    ApplyPassthrough();
                    return effectDescription.DrawDescription;
                }
                failedAttemptValues = null;
                failedAttemptFrame = null;
            }
            attemptCount++;

            try
            {
                EnsureInstance(width, height, fps, length);
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインの読み込みに失敗しました。id={item.PluginId} path={item.PluginPath}", e);
                hasLoggedFailure = true;
                failedAttemptValues = canCompareAttempt ? [.. attemptValuesBuffer.Take(LoadRelevantValueCount)] : null;
                failedAttemptFrame = null;
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
                // パラメータ適用の失敗も描画失敗と同じ失敗経路（パススルー＋ログ1回）に乗せる
                foreach (var parameter in ofxParameters)
                    parameter.ApplyTo(instance, frame, length, fps);
                instance.SetDoubleParam(OfxConstants.ImageEffectTransitionParamName, progress);

                // 入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));

                // 恒等（効果なし）宣言時はrenderを呼ばずに宣言されたクリップの内容を出力する（規格の契約。
                // 恒等宣言後にrenderを呼ぶと、恒等フレームでは描画しない前提のプラグインで
                // クリアされないプール出力バッファの前フレーム残像が表示され得る）。
                // アイテム画像側のクリップなら入力を素通しし（GPU↔CPU転送も丸ごとスキップ）、
                // 透明側のクリップ（登場の先頭・退場の末尾＝進行度0/1で発生）なら全面透明を直接出力する。
                // それ以外のクリップ（プラグインが追加定義したMask等）への恒等は内容を供給していないため
                // 恒等扱いにせず通常レンダリングへ倒す（姉妹ホストと同じ明示照合）
                var identityClip = instance.GetIdentityClipName(frame, renderWindow);
                var itemSideClipName = isItemToTransparent
                    ? OfxConstants.ImageEffectTransitionSourceFromClipName
                    : OfxConstants.ImageEffectTransitionSourceToClipName;
                var transparentSideClipName = isItemToTransparent
                    ? OfxConstants.ImageEffectTransitionSourceToClipName
                    : OfxConstants.ImageEffectTransitionSourceFromClipName;
                if (identityClip == itemSideClipName)
                {
                    hasLoggedFailure = false;
                    ApplyPassthrough();
                    return effectDescription.DrawDescription;
                }
                if (identityClip == transparentSideClipName)
                {
                    EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                    // 出力バッファは毎フレームクリアされないプールのため、透明出力はここでゼロ埋めして転送する
                    Array.Clear(outputBuffer, 0, outputBitmapWidth * outputBitmapHeight * 4);
                    fixed (byte* outputPointer = outputBuffer)
                    {
                        outputBitmap!.CopyFromMemory((nint)outputPointer, outputBitmapWidth * 4);
                    }
                    hasLoggedFailure = false;
                    transformEffect.SetInput(0, outputBitmap, true);
                    transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(
                        bounds.Left + renderWindow.x1,
                        bounds.Top + (height - renderWindow.y2));
                    isPassthroughApplied = false;
                    return effectDescription.DrawDescription;
                }

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
                failedAttemptValues = canCompareAttempt ? [.. attemptValuesBuffer] : null;
                failedAttemptFrame = frame;
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
        /// 今回のOFX試行の成否に影響しうる入力（プラグイン・サイズ・fps/アイテム長・進行度・OFXパラメータ評価値）を
        /// attemptValuesBufferへ集める。先頭 <see cref="LoadRelevantValueCount"/> 個は
        /// プラグイン読み込みの成否に関わる値（並び順に意味がある）。
        /// 前回失敗時の値と一致する場合は再試行をスキップする
        /// </summary>
        void CollectAttemptValues(IEnumerable<OfxParameterBase> ofxParameters, int width, int height, int fps, int length, int frame, double progress, bool isItemToTransparent)
        {
            var values = attemptValuesBuffer;
            values.Clear();
            values.Add(item.PluginPath);
            values.Add(item.PluginId);
            values.Add(width);
            values.Add(height);
            values.Add(fps);
            values.Add(length);
            values.Add(progress);
            values.Add(isItemToTransparent);
            foreach (var parameter in ofxParameters)
                parameter.CollectValues(values, frame, length, fps);
        }

        /// <summary>OFX試行（インスタンス生成〜レンダリング）を開始した回数（テスト用）</summary>
        internal int AttemptCount => attemptCount;

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
            // 入力変更による再試行のたびにログが出てしまう）
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
                // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
                transformEffect?.SetInput(0, null, true);
                ReleaseRenderResources();
            }
            base.Dispose(disposing);
        }
    }
}
