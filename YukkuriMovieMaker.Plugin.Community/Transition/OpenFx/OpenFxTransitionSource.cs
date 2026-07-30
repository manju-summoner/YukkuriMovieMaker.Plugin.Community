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
        ID2D1Bitmap1? secondGpuBitmap;
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
                ApplyPassthrough(passthrough);
                return;
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

            // 直近の失敗と同じ入力での再試行は同じ失敗を繰り返すだけのためスキップし、入力が変わったら即再試行する
            // （毎フレームの失敗連打を避けつつ、原因を直したときに素通しのまま固まらないように）。
            // 読み込み失敗（failedAttemptFrame=null）は読み込みの成否に関わる先頭の値だけで比較する
            // （進行度・OFXパラメータは読み込みに影響しない。毎フレーム変わる値で壊れたプラグインの
            //   ロードを毎フレーム再試行しないように）。レンダリング失敗はOFX時刻・入力画像でも結果が変わり得るため
            // 全値＋同一フレームの間だけ抑止する。スナップショットは試行前に採取したものを失敗時にそのまま保存する
            // （レンダリング中のUIスレッド編集を「試行済みで失敗」と誤記録しないため）。
            // パラメータリストはUIスレッドで差し替わり得るため1回だけ読み、スナップショットと適用で共有する
            var clampedProgress = Math.Clamp(easedProgress, 0, 1);
            var ofxParameters = item.Parameters;
            var canCompareAttempt = true;
            try
            {
                CollectAttemptValues(ofxParameters, width, height, fps, length, frame, clampedProgress);
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
                    ApplyPassthrough(passthrough);
                    return;
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
                // パラメータ適用の失敗も描画失敗と同じ失敗経路（パススルー＋ログ1回）に乗せる
                foreach (var parameter in ofxParameters)
                    parameter.ApplyTo(instance, frame, length, fps);
                // 進行度はトランジションコンテキストの必須パラメータとしてホストが駆動する
                instance.SetDoubleParam(OfxConstants.ImageEffectTransitionParamName, clampedProgress);

                // 入力サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す
                renderWindow = instance.GetRegionOfDefinition(frame, Math.Max(1, (int)dc.MaximumBitmapSize));

                // 恒等（効果なし）宣言時はGPU↔CPU転送とrenderを丸ごとスキップして該当入力を素通しする
                // （クロスフェード等のトランジションは進行度0/1で恒等になる）
                var identityClip = instance.GetIdentityClipName(frame, renderWindow);
                if (identityClip is OfxConstants.ImageEffectTransitionSourceFromClipName
                    or OfxConstants.ImageEffectTransitionSourceToClipName)
                {
                    hasLoggedFailure = false;
                    ApplyPassthrough(identityClip == OfxConstants.ImageEffectTransitionSourceFromClipName ? before : after);
                    return;
                }

                EnsureInputResources(width, height);
                EnsureOutputResources(renderWindow.x2 - renderWindow.x1, renderWindow.y2 - renderWindow.y1);
                DrawInputToGpuBitmap(dc, before, gpuBitmap!, bounds);
                var renderedWithInterop = false;
                if (instance.CanUseD3D11Interop)
                {
                    EnsureSecondGpuBitmap(width, height);
                    DrawInputToGpuBitmap(dc, after, secondGpuBitmap!, bounds);
                    if (isRenderUnsafe)
                    {
                        lock (OfxEffectInstance.UnsafeRenderLock)
                            renderedWithInterop = OfxD3D11Interop.WithResources(
                                instance,
                                gpuBitmap!,
                                secondGpuBitmap!,
                                outputBitmap!,
                                (from, to, output) => instance.TryRenderTransitionD3D11(from, to, output, frame, renderWindow));
                    }
                    else
                    {
                        renderedWithInterop = OfxD3D11Interop.WithResources(
                            instance,
                            gpuBitmap!,
                            secondGpuBitmap!,
                            outputBitmap!,
                            (from, to, output) => instance.TryRenderTransitionD3D11(from, to, output, frame, renderWindow));
                    }
                }
                else
                {
                    OfxD3D11Interop.ReleaseResource(instance, secondGpuBitmap);
                    secondGpuBitmap?.Dispose();
                    secondGpuBitmap = null;
                }
                if (!renderedWithInterop)
                {
                    ReadInputPixels(gpuBitmap!, width, height, fromBuffer);
                    if (secondGpuBitmap is not null)
                    {
                        ReadInputPixels(secondGpuBitmap, width, height, toBuffer);
                    }
                    else
                    {
                        DrawInputToGpuBitmap(dc, after, gpuBitmap!, bounds);
                        ReadInputPixels(gpuBitmap!, width, height, toBuffer);
                    }
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
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインのレンダリングに失敗しました。id={item.PluginId}", e);
                hasLoggedFailure = true;
                failedAttemptValues = canCompareAttempt ? [.. attemptValuesBuffer] : null;
                failedAttemptFrame = frame;
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

        /// <summary>
        /// 今回のOFX試行の成否に影響しうる入力（プラグイン・サイズ・fps/アイテム長・進行度・OFXパラメータ評価値）を
        /// attemptValuesBufferへ集める。先頭 <see cref="LoadRelevantValueCount"/> 個は
        /// プラグイン読み込みの成否に関わる値（並び順に意味がある）。
        /// 前回失敗時の値と一致する場合は再試行をスキップする
        /// </summary>
        void CollectAttemptValues(IEnumerable<OfxParameterBase> ofxParameters, int width, int height, int fps, int length, int frame, double progress)
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
            var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextTransition);
            isRenderUnsafe = descriptor.Props.GetStringOrDefault(
                OfxConstants.ImageEffectPluginRenderThreadSafety,
                OfxConstants.ImageEffectRenderFullySafe) == OfxConstants.ImageEffectRenderUnsafe;
            var created = OfxEffectInstance.CreateWithGpuBackend(
                plugin,
                OfxConstants.ImageEffectContextTransition,
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
            var bufferSize = width * height * 4;
            if (fromBuffer.Length < bufferSize)
                fromBuffer = new byte[bufferSize];
            if (toBuffer.Length < bufferSize)
                toBuffer = new byte[bufferSize];
            if (inputBitmapWidth == width && inputBitmapHeight == height && gpuBitmap is not null)
                return;
            OfxD3D11Interop.ReleaseResource(instance, gpuBitmap);
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            OfxD3D11Interop.ReleaseResource(instance, secondGpuBitmap);
            secondGpuBitmap?.Dispose();
            secondGpuBitmap = null;
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

        void EnsureSecondGpuBitmap(int width, int height)
        {
            if (secondGpuBitmap is not null)
                return;
            var properties = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.Target);
            secondGpuBitmap = devices.DeviceContext.CreateBitmap(new SizeI(width, height), properties);
        }

        void EnsureOutputResources(int width, int height)
        {
            if (outputBitmapWidth == width && outputBitmapHeight == height && outputBitmap is not null)
                return;
            // 差し替え前の出力ビットマップがエフェクト入力に残ったまま破棄しない
            transformEffect.SetInput(0, null, true);
            passthroughInput = null;
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

        static void DrawInputToGpuBitmap(ID2D1DeviceContext dc, ID2D1Image input, ID2D1Bitmap1 target, Rect bounds)
        {
            // 呼び出し元が設定した描画先を壊さないよう退避して復元する
            var previousTarget = dc.Target;
            try
            {
                dc.Target = target;
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
        }

        void ReadInputPixels(ID2D1Bitmap1 sourceBitmap, int width, int height, byte[] destinationBuffer)
        {
            cpuBitmap!.CopyFromBitmap(sourceBitmap);
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
            // エフェクト入力に接続したまま出力ビットマップを破棄しないよう、先に切り離す
            transformEffect.SetInput(0, null, true);
            instance?.Dispose();
            instance = null;
            gpuBitmap?.Dispose();
            gpuBitmap = null;
            secondGpuBitmap?.Dispose();
            secondGpuBitmap = null;
            cpuBitmap?.Dispose();
            cpuBitmap = null;
            outputBitmap?.Dispose();
            outputBitmap = null;
            transformOutput.Dispose();
            transformEffect.Dispose();
        }
    }
}
