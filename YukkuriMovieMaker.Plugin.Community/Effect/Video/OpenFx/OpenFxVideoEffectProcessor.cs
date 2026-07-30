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
        // 直近の失敗時の試行入力（プラグイン・サイズ・fps/アイテム長・OFXパラメータ評価値。nullなら失敗状態ではない）。
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
        // （プラグインパス/ID・インスタンスサイズ・fps/アイテム長）
        const int LoadRelevantValueCount = 6;

        public OpenFxVideoEffectProcessor(IGraphicsDevicesAndContext devices, OpenFxVideoEffect item)
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
                // 選択解除されたらネイティブの入出力バッファを保持し続けない。
                // 失敗状態も併せてリセットする（残すと同じプラグインを同じ設定で再選択したときに
                // 前回の失敗入力と一致して再試行されない。再選択は明示的な操作のため試行し直す）
                instance?.Dispose();
                instance = null;
                instancePluginPath = "";
                instancePluginId = "";
                attemptedPluginPath = "";
                attemptedPluginId = "";
                failedAttemptValues = null;
                failedAttemptFrame = null;
                hasLoggedFailure = false;
                ApplyPassthrough();
                return effectDescription.DrawDescription;
            }

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

            // 直近の失敗と同じ入力での再試行は同じ失敗を繰り返すだけのためスキップし、入力が変わったら即再試行する
            // （毎フレームの失敗連打を避けつつ、原因を直したときに素通しのまま固まらないように）。
            // 読み込み失敗（failedAttemptFrame=null）は読み込みの成否に関わる先頭の値だけで比較する
            // （OFXパラメータは読み込みに影響しない。アニメーション中の値の変化で壊れたプラグインの
            //   ロードを毎フレーム再試行しないように）。レンダリング失敗はOFX時刻・入力画像でも結果が変わり得るため
            // 全値＋同一フレームの間だけ抑止する。スナップショットは試行前に採取したものを失敗時にそのまま保存する
            // （レンダリング中のUIスレッド編集を「試行済みで失敗」と誤記録しないため）。
            // パラメータリストはUIスレッドで差し替わり得るため1回だけ読み、スナップショットと適用で共有する
            var ofxParameters = item.Parameters;
            var canCompareAttempt = true;
            try
            {
                CollectAttemptValues(ofxParameters, width, height, fps, length, frame);
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

                // ぼかし・グロー等は入力より大きな出力領域（RoD）を宣言するため、出力はRoDサイズで確保する
                // （入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す）
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));

                // 恒等（効果なし）宣言時はGPU↔CPU転送とrenderを丸ごとスキップして入力を素通しする
                if (instance.GetIdentityClipName(frame, renderWindow) == OfxConstants.ImageEffectSimpleSourceClipName)
                {
                    hasLoggedFailure = false;
                    ApplyPassthrough();
                    return effectDescription.DrawDescription;
                }

                EnsureInputResources(width, height);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                DrawInputToGpuBitmap(dc, bounds);
                var renderedWithInterop = false;
                var canUseD3D11Interop = instance.CanUseD3D11Interop;
                if (canUseD3D11Interop && isRenderUnsafe)
                {
                    lock (OfxEffectInstance.UnsafeRenderLock)
                        renderedWithInterop = OfxD3D11Interop.WithResources(
                            instance,
                            gpuBitmap!,
                            outputBitmap!,
                            (source, output) => instance.TryRenderD3D11(source, output, frame, renderWindow));
                }
                else if (canUseD3D11Interop)
                {
                    renderedWithInterop = OfxD3D11Interop.WithResources(
                        instance,
                        gpuBitmap!,
                        outputBitmap!,
                        (source, output) => instance.TryRenderD3D11(source, output, frame, renderWindow));
                }
                if (!renderedWithInterop)
                {
                    ReadInputPixels(width, height);
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
        /// 今回のOFX試行の成否に影響しうる入力（プラグイン・サイズ・fps/アイテム長・OFXパラメータ評価値）を
        /// attemptValuesBufferへ集める。先頭 <see cref="LoadRelevantValueCount"/> 個は
        /// プラグイン読み込みの成否に関わる値（並び順に意味がある）。
        /// 前回失敗時の値と一致する場合は再試行をスキップする
        /// </summary>
        void CollectAttemptValues(IEnumerable<OfxParameterBase> ofxParameters, int width, int height, int fps, int length, int frame)
        {
            var values = attemptValuesBuffer;
            values.Clear();
            values.Add(item.PluginPath);
            values.Add(item.PluginId);
            values.Add(width);
            values.Add(height);
            values.Add(fps);
            values.Add(length);
            foreach (var parameter in ofxParameters)
                parameter.CollectValues(values, frame, length, fps);
        }

        /// <summary>OFX試行（インスタンス生成〜レンダリング）を開始した回数（テスト用）</summary>
        internal int AttemptCount => attemptCount;

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
            var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextFilter);
            isRenderUnsafe = descriptor.Props.GetStringOrDefault(
                OfxConstants.ImageEffectPluginRenderThreadSafety,
                OfxConstants.ImageEffectRenderFullySafe) == OfxConstants.ImageEffectRenderUnsafe;
            var created = OfxEffectInstance.CreateWithGpuBackend(
                plugin,
                OfxConstants.ImageEffectContextFilter,
                width,
                height,
                fps,
                durationFrames,
                devices);
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
            OfxD3D11Interop.ReleaseResource(instance, gpuBitmap);
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
            OfxD3D11Interop.ReleaseResource(instance, outputBitmap);
            outputBitmap?.Dispose();
            outputBitmap = null;

            var dc = devices.DeviceContext;
            var outputProperties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.Target);
            outputBitmap = dc.CreateBitmap(new SizeI(width, height), outputProperties);
            outputBitmapWidth = width;
            outputBitmapHeight = height;

            var bufferSize = width * height * 4;
            if (outputBuffer.Length < bufferSize)
                outputBuffer = new byte[bufferSize];
        }

        void DrawInputToGpuBitmap(ID2D1DeviceContext dc, Rect bounds)
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
        }

        void ReadInputPixels(int width, int height)
        {
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
