using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;
using YukkuriMovieMaker.Plugin.Community.Shape.OpenFx;

namespace YukkuriMovieMaker.Plugin.Community.Brush.OpenFx
{
    /// <summary>
    /// ブラシ「OpenFX」の描画処理。
    /// ジェネレーターコンテキストのOFXプラグインを入力なしでCPUレンダリングし、
    /// 結果のビットマップをイメージブラシとして出力する。
    /// 割合指定（既定100%×100%）では塗り先の矩形（BrushSourceDescription.Bounds）を基準に配置し、
    /// ピクセル指定では指定サイズの画像を原点中心に配置して繰り返し方法（ExtendMode）で敷き詰める。
    /// プラグイン未選択・読み込み失敗時は透明ブラシを出力する
    /// </summary>
    internal sealed unsafe class OpenFxBrushSource : IBrushSource2
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly OpenFxBrushParameter parameter;
        readonly ID2D1SolidColorBrush emptyBrush;
        bool isEmptyApplied;

        ID2D1ImageBrush? imageBrush;

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
        // 直近の失敗時の試行入力（プラグイン・サイズ・fps/アイテム長・OFXパラメータ評価値。nullなら失敗状態ではない）。
        // 同じ入力での再試行は同じ失敗を繰り返すだけのため、入力が変わるまで透明のまま試行しない。
        // レンダリング失敗はOFX時刻（フレーム）でも結果が変わり得るため、failedAttemptFrameが一致する間だけ抑止する
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

        // 割合指定で受け付ける塗り先Bounds座標の上限（これを超える値は実質無限大の画像とみなして透明にする）
        const double MaxRelativeBoundsValue = 1e9;

        public ID2D1Brush Brush => isEmptyApplied || imageBrush is null ? emptyBrush : imageBrush;

        public OpenFxBrushSource(IGraphicsDevicesAndContext devices, OpenFxBrushParameter parameter)
        {
            this.devices = devices;
            this.parameter = parameter;

            // 未選択・失敗時に出力する透明ブラシ
            emptyBrush = devices.DeviceContext.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0f));
            isEmptyApplied = true;
        }

        /// <summary>
        /// 旧API（IBrushSource.Update）互換。既定実装はBounds=default（空）で呼ぶため、
        /// 割合指定（既定）だと常に透明になってしまう。旧APIで呼ばれた場合は
        /// 画面サイズを原点中心の塗り先とみなして描画する
        /// （新APIの空Boundsは「塗る領域が無い」ため透明のまま＝レンダリングコストも掛けない。挙動を分けるための明示実装）。
        /// なお標準経路（Brush.CreateBrush→BrushWrapperSource）ではラッパーが旧API呼び出しを
        /// Bounds=defaultの新APIへ変換してから内側へ渡すため、この実装には到達しない。
        /// 効くのはIBrushParameter.CreateBrushの戻り値を直接旧APIで叩く非標準の呼び出しのみで、
        /// ラッパー経由の旧API呼び出し（旧API外部プラグイン×割合指定）は空Bounds＝透明になる既知の制限
        /// </summary>
#pragma warning disable CS0618 // 旧APIの互換実装のため
        bool IBrushSource.Update(TimelineItemSourceDescription desc)
#pragma warning restore CS0618
        {
            var screenSize = desc.ScreenSize;
            return Update(new BrushSourceDescription(
                desc,
                new RawRectF(-screenSize.Width / 2f, -screenSize.Height / 2f, screenSize.Width / 2f, screenSize.Height / 2f)));
        }

        public bool Update(BrushSourceDescription desc)
        {
            var timelineDesc = desc.TimelineItemSourceDescription;
            var frame = timelineDesc.ItemPosition.Frame;
            var length = timelineDesc.ItemDuration.Frame;
            var fps = timelineDesc.FPS;

            if (string.IsNullOrEmpty(parameter.PluginPath) || string.IsNullOrEmpty(parameter.PluginId))
            {
                // 選択解除されたらネイティブの出力バッファを保持し続けない。
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
                var changed = ApplyEmpty();
                // 出力ビットマップ（最大8K相当）とバッファも解放する。
                // 透明ブラシへ切り替え済みのため、使用中ブラシの参照先を破棄することにはならない
                imageBrush?.Dispose();
                imageBrush = null;
                outputBitmap?.Dispose();
                outputBitmap = null;
                outputBitmapWidth = 0;
                outputBitmapHeight = 0;
                outputBuffer = [];
                return changed;
            }

            // プラグインが切り替わったら失敗状態を即座にリセットする（失敗ログを新しいプラグインで出し直すため）
            if (!string.Equals(attemptedPluginPath, parameter.PluginPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(attemptedPluginId, parameter.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                attemptedPluginPath = parameter.PluginPath;
                attemptedPluginId = parameter.PluginId;
                failedAttemptValues = null;
                failedAttemptFrame = null;
                hasLoggedFailure = false;
            }

            var dc = devices.DeviceContext;
            var bounds = desc.Bounds;
            // 単位はUIスレッドから切り替わり得るため1回だけ読み、以降の分岐とGetOutputSizeで共有する
            var coordinateMode = parameter.CoordinateMode;
            var isRelative = coordinateMode is CoordinateMode.Relative;
            double rawWidth, rawHeight;
            float snappedLeft = 0f, snappedTop = 0f;
            if (isRelative)
            {
                // 異常なBounds（NaN・Infinity・実質無限大の巨大値）はフィット倍率やアンカーが
                // floatで±Infinityへ飽和して行列が壊れるため、透明へ倒す
                // （!(<=)の形にすることでNaNも弾く。1e9pxを超える塗り先に意味のある描画はできない）
                if (!(Math.Abs(bounds.Left) <= MaxRelativeBoundsValue) || !(Math.Abs(bounds.Top) <= MaxRelativeBoundsValue)
                    || !(Math.Abs(bounds.Right) <= MaxRelativeBoundsValue) || !(Math.Abs(bounds.Bottom) <= MaxRelativeBoundsValue))
                {
                    return ApplyEmpty();
                }
                // 割合指定は塗り先の矩形サイズに対する%（100%×100%＝塗り先にぴったり）。
                // 100%以外は塗り先の中心を基準に配置する
                var (targetWidth, targetHeight) = parameter.GetOutputSize(coordinateMode, frame, length, fps, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
                if (!double.IsFinite(targetWidth) || !double.IsFinite(targetHeight) || targetWidth <= 0 || targetHeight <= 0)
                {
                    return ApplyEmpty();
                }
                // 塗り先のBoundsは非整数になりうる（ぼかし・回転後の境界等）。アンカーを整数に保って
                // 既定の線形補間のサブピクセル再サンプルでにじまないよう、床/天井で整数グリッドへ広げて覆う
                var centerX = (bounds.Left + bounds.Right) / 2.0;
                var centerY = (bounds.Top + bounds.Bottom) / 2.0;
                var left = Math.Floor(centerX - targetWidth / 2);
                var top = Math.Floor(centerY - targetHeight / 2);
                rawWidth = Math.Ceiling(centerX + targetWidth / 2) - left;
                rawHeight = Math.Ceiling(centerY + targetHeight / 2) - top;
                snappedLeft = (float)left;
                snappedTop = (float)top;
            }
            else
            {
                // ピクセル指定は図形「OpenFX」と同じ丸め（Math.Round。Bounds非依存のためサイズは0でよい）
                (rawWidth, rawHeight) = parameter.GetOutputSize(coordinateMode, frame, length, fps, 0, 0);
            }
            // 異常な指定値（NaN・Infinity）はintへの変換結果が未定義のため明示的に透明へ倒す
            if (!double.IsFinite(rawWidth) || !double.IsFinite(rawHeight))
            {
                return ApplyEmpty();
            }
            // D2Dビットマップの上限を超えるサイズは生成できないためクランプする
            var maxSize = Math.Max(1, (int)dc.MaximumBitmapSize);
            var width = (int)Math.Clamp(isRelative ? rawWidth : Math.Round(rawWidth), 0, maxSize);
            var height = (int)Math.Clamp(isRelative ? rawHeight : Math.Round(rawHeight), 0, maxSize);
            if (width <= 0 || height <= 0)
            {
                return ApplyEmpty();
            }
            // 総ピクセル数も制限する（超えたら縦横比を保って縮小。上限は図形「OpenFX」と共通）
            if ((long)width * height > OpenFxShapeSource.MaxOutputPixels)
            {
                var scale = Math.Sqrt((double)OpenFxShapeSource.MaxOutputPixels / ((long)width * height));
                width = Math.Max(1, (int)(width * scale));
                height = Math.Max(1, (int)(height * scale));
            }

            // 直近の失敗と同じ入力での再試行は同じ失敗を繰り返すだけのためスキップし、入力が変わったら即再試行する
            // （毎フレームの失敗連打を避けつつ、原因を直したときに透明のまま固まらないように）。
            // 読み込み失敗（failedAttemptFrame=null）は読み込みの成否に関わる先頭の値だけで比較する
            // （OFXパラメータは読み込みに影響しない。アニメーション中の値の変化で壊れたプラグインの
            //   ロードを毎フレーム再試行しないように）。レンダリング失敗はOFX時刻でも結果が変わり得るため
            // 全値＋同一フレームの間だけ抑止する。スナップショットは試行前に採取したものを失敗時にそのまま保存する
            // （レンダリング中のUIスレッド編集を「試行済みで失敗」と誤記録しないため）。
            // パラメータリストはUIスレッドで差し替わり得るため1回だけ読み、スナップショットと適用で共有する
            var ofxParameters = parameter.Parameters;
            var canCompareAttempt = true;
            try
            {
                CollectAttemptValues(ofxParameters, width, height, fps, length, frame);
            }
            catch
            {
                // 試行値の評価自体が失敗する場合（Min>Max等の壊れたメタデータ）は比較不能＝毎回試行に倒す
                // （同じ計算を行うApplyToも失敗するため、レンダリング失敗経路の透明出力＋ログ1回に乗る）
                canCompareAttempt = false;
                attemptValuesBuffer.Clear();
            }
            if (canCompareAttempt && failedAttemptValues is not null)
            {
                var isSameFailedAttempt = failedAttemptFrame is null
                    ? attemptValuesBuffer.Take(failedAttemptValues.Length).SequenceEqual(failedAttemptValues)
                    : failedAttemptFrame == frame && attemptValuesBuffer.SequenceEqual(failedAttemptValues);
                if (isSameFailedAttempt)
                    return ApplyEmpty();
                failedAttemptValues = null;
                failedAttemptFrame = null;
            }
            attemptCount++;

            try
            {
                // 割合指定では塗り先サイズの変化のたびにインスタンスが作り直される
                // （サイズはインスタンス生成時にプラグインへ伝わるため。フィルター・図形と同じv1制限）
                EnsureInstance(width, height, fps, length);
            }
            catch (Exception e)
            {
                if (!hasLoggedFailure)
                    Log.Default.Write($"OFXプラグインの読み込みに失敗しました。id={parameter.PluginId} path={parameter.PluginPath}", e);
                hasLoggedFailure = true;
                failedAttemptValues = canCompareAttempt ? [.. attemptValuesBuffer.Take(LoadRelevantValueCount)] : null;
                failedAttemptFrame = null;
                return ApplyEmpty();
            }
            if (instance is null)
            {
                return ApplyEmpty();
            }

            OfxRectI renderWindow;
            try
            {
                // パラメータ適用の失敗も描画失敗と同じ失敗経路（透明出力＋ログ1回）に乗せる
                foreach (var ofxParameter in ofxParameters)
                    ofxParameter.ApplyTo(instance, frame, length, fps);

                // ジェネレーターもRoD（定義域）を宣言できるため、出力はRoDサイズで確保する
                // （指定サイズが上限付近のとき、RoD拡張でビットマップ上限を超えないよう上限も渡す）
                renderWindow = instance.GetRegionOfDefinition(frame, maxSize);
                // RoD拡張（±1024px）が乗ると総ピクセル上限を超え得るため、renderWindowにも上限を適用する
                renderWindow = OpenFxShapeSource.ClampRenderWindowArea(renderWindow, width, height, OpenFxShapeSource.MaxOutputPixels);
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
                failedAttemptValues = canCompareAttempt ? [.. attemptValuesBuffer] : null;
                failedAttemptFrame = frame;
                return ApplyEmpty();
            }

            hasLoggedFailure = false;

            // 指定サイズ（塗り先基準または指定ピクセル）がレンダリング上限（総ピクセル数・D2Dビットマップ上限）で
            // クランプされた場合も見かけのサイズ・タイル周期を保つよう、縮小された分をブラシ変換の拡大で補う
            // （クランプ無しでは等倍＝1。ピクセル指定は四捨五入後の指定値を基準にし、通常時のずれを持ち込まない）
            var (fitScaleX, fitScaleY) = GetFitScale(
                isRelative ? rawWidth : Math.Round(rawWidth),
                isRelative ? rawHeight : Math.Round(rawHeight),
                width,
                height);

            // OFX座標（左下原点）のRoDを、プロジェクトサイズの矩形が基準位置に合うD2D座標へ変換する。
            // 割合指定では整数化した配置先矩形（塗り先中心基準）の左上へぴったり合わせる
            // （クランプ無しでは整数アンカー＝等倍コピーになる）。
            // RoDオフセットは画像と一緒に拡大されるため、フィット倍率を掛けてから加える。
            // ピクセル指定では図形「OpenFX」と同じく原点中心（クランプ時は拡大後の見かけサイズ基準）に配置し、
            // 半ピクセルの平行移動で既定の線形補間がにじまないよう整数へ丸める
            // （Roundは銀行家丸めで方向が変わるためFloorで統一）
            var anchorX = isRelative
                ? snappedLeft + renderWindow.x1 * fitScaleX
                : MathF.Floor((-width * fitScaleX) / 2f + renderWindow.x1 * fitScaleX);
            var anchorY = isRelative
                ? snappedTop + (height - renderWindow.y2) * fitScaleY
                : MathF.Floor((-height * fitScaleY) / 2f + (height - renderWindow.y2) * fitScaleY);

            // 共通変換（ズーム・回転・縦横比・反転）の基準点は「描画内容の中心」に揃える。
            // ピクセル指定では画像が原点中心配置のため原点基準＝内容中心。割合指定では塗り先が原点から
            // 離れていても内容がその場でズーム・回転するよう、配置先（整数化後）の中心を挟んで合成する
            // （基準点は実Boundsの中心ではなくスナップ後矩形の中心＝最大0.5pxずれるが、
            //   描画内容＝スナップ後矩形の中心で回す方が内容とぶれない。意図的な選択）
            Matrix3x2 brushMatrix;
            if (isRelative)
            {
                var centerX = snappedLeft + (float)(rawWidth / 2);
                var centerY = snappedTop + (float)(rawHeight / 2);
                brushMatrix =
                    Matrix3x2.CreateScale(fitScaleX, fitScaleY)
                    * Matrix3x2.CreateTranslation(anchorX - centerX, anchorY - centerY)
                    * parameter.CreateBrushMatrix(timelineDesc)
                    * Matrix3x2.CreateTranslation(centerX, centerY);
            }
            else
            {
                brushMatrix =
                    Matrix3x2.CreateScale(fitScaleX, fitScaleY)
                    * Matrix3x2.CreateTranslation(anchorX, anchorY)
                    * parameter.CreateBrushMatrix(timelineDesc);
            }

            // ブラシは毎回作り直す（時間変化するジェネレーターに追従するため画像も毎フレーム更新される）。
            // 繰り返し（Wrap/Mirror）の1タイルはRoDサイズの画像全体になる（RoD拡張時は指定サイズより大きくなる）
            var newBrush = dc.CreateImageBrush(
                outputBitmap,
                new ImageBrushProperties(
                    new RawRectF(0, 0, outputBitmapWidth, outputBitmapHeight),
                    parameter.ExtendModeX.ToD2DExtendMode(),
                    parameter.ExtendModeY.ToD2DExtendMode(),
                    InterpolationMode.MultiSampleLinear),
                new BrushProperties(1f, brushMatrix));
            imageBrush?.Dispose();
            imageBrush = newBrush;
            isEmptyApplied = false;
            return true;
        }

        /// <summary>
        /// 指定サイズ（割合指定の配置先矩形またはピクセル指定の四捨五入後の値）がレンダリング上限に
        /// クランプされた場合の、指定サイズへ引き伸ばすブラシ倍率を求める。
        /// クランプされていない場合（切り上げで配置先よりわずかに大きい場合を含む）は等倍＝1を返す
        /// （縮小方向のフィットまで行うと、整数サイズの配置先でもサブピクセル縮小で常にぼけるため）
        /// </summary>
        internal static (float ScaleX, float ScaleY) GetFitScale(double boundsWidth, double boundsHeight, int width, int height)
        {
            var scaleX = boundsWidth > width ? (float)(boundsWidth / width) : 1f;
            var scaleY = boundsHeight > height ? (float)(boundsHeight / height) : 1f;
            return (scaleX, scaleY);
        }

        /// <summary>
        /// 透明ブラシへ切り替える。出力が変化した場合はtrueを返す
        /// </summary>
        bool ApplyEmpty()
        {
            if (isEmptyApplied)
                return false;
            isEmptyApplied = true;
            return true;
        }

        void EnsureInstance(int width, int height, int fps, int durationFrames)
        {
            // 失敗状態のリセットはUpdate側のエッジ検出で行う（ここで毎回リセットすると
            // 入力変更による再試行のたびにログが出てしまう）
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
            // 旧ビットマップを参照するブラシは次のブラシ作成時に破棄される（D2Dの参照カウントで生存が管理される）
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
            values.Add(parameter.PluginPath);
            values.Add(parameter.PluginId);
            values.Add(width);
            values.Add(height);
            values.Add(fps);
            values.Add(length);
            foreach (var ofxParameter in ofxParameters)
                ofxParameter.CollectValues(values, frame, length, fps);
        }

        /// <summary>OFX試行（インスタンス生成〜レンダリング）を開始した回数（テスト用）</summary>
        internal int AttemptCount => attemptCount;

        public void Dispose()
        {
            instance?.Dispose();
            instance = null;
            imageBrush?.Dispose();
            imageBrush = null;
            outputBitmap?.Dispose();
            outputBitmap = null;
            emptyBrush.Dispose();
        }
    }
}
