using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// エフェクトのディスクリプタ・インスタンスがハンドル経由で共有するビュー
    /// （OfxImageEffectSuite の getPropertySet / getParamSet が両者に応えるため）
    /// </summary>
    internal interface IOfxImageEffectObject
    {
        OfxPropertySet Props { get; }
        OfxParamSet ParamSet { get; }
    }

    /// <summary>
    /// 画像エフェクトのインスタンス（kOfxActionCreateInstance 以降の OfxImageEffectHandle の実体）。
    /// パラメータ値の保持と、フレーム単位のレンダリング駆動を担う。
    /// </summary>
    internal sealed unsafe class OfxEffectInstance : OfxObject, IOfxImageEffectObject
    {
        /// <summary>
        /// kOfxImageEffectRenderUnsafe（同時レンダリング不可）を宣言するプラグイン用の全体ロック。
        /// 映像エフェクト・場面切替えの双方から同じロックで直列化する
        /// </summary>
        internal static readonly object UnsafeRenderLock = new();

        readonly OfxImageEffectPlugin plugin;
        readonly List<OfxClipInstance> clips = [];
        readonly HashSet<string> changedParams = [];
        bool isCreated;

        // フレーム毎の大きなネイティブ確保を避けるため、クリップ画像はサイズが変わるまで使い回す
        readonly Dictionary<string, OfxImage> pooledInputImages = [];
        OfxImage? pooledOutputImage;
        long renderSerial;

        public OfxPropertySet Props { get; }
        public OfxParamSet ParamSet { get; } = new();
        public IReadOnlyList<OfxClipInstance> Clips => clips;
        public int Width { get; }
        public int Height { get; }
        public double FrameRate { get; }
        public double DurationFrames { get; }

        public OfxEffectInstance(OfxImageEffectPlugin plugin, string context, int width, int height, double frameRate, double durationFrames)
        {
            this.plugin = plugin;
            Width = width;
            Height = height;
            FrameRate = frameRate;
            DurationFrames = durationFrames;

            var descriptor = plugin.DescribeInContext(context);
            // スキャンを経ずに到達した場合（保存済みプロジェクト等）に備えて対応外の宣言を再検査する
            if (descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPluginPropSingleInstance, 0) != 0)
                throw new InvalidOperationException($"単一インスタンス制約のプラグインは未対応です。plugin={plugin.Identifier}");
            if (descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPropTemporalClipAccess, 0) != 0)
                throw new InvalidOperationException($"テンポラルアクセスを要求するプラグインは未対応です。plugin={plugin.Identifier}");
            if (!descriptor.SupportedPixelDepths.Contains(OfxConstants.BitDepthFloat))
                throw new InvalidOperationException($"floatピクセル深度非対応のプラグインは未対応です。plugin={plugin.Identifier}");
            foreach (var clipName in new[]
            {
                OfxConstants.ImageEffectSimpleSourceClipName,
                OfxConstants.ImageEffectTransitionSourceFromClipName,
                OfxConstants.ImageEffectTransitionSourceToClipName,
                OfxConstants.ImageEffectOutputClipName,
            })
            {
                var clipDescriptor = descriptor.FindClip(clipName);
                if (clipDescriptor is not null
                    && !clipDescriptor.Props.GetStrings(OfxConstants.ImageEffectPropSupportedComponents).Contains(OfxConstants.ImageComponentRGBA))
                    throw new InvalidOperationException($"RGBA非対応のクリップを持つプラグインは未対応です。plugin={plugin.Identifier} clip={clipName}");
            }
            Props = new OfxPropertySet { DebugName = $"effectInstance({plugin.Identifier})" };
            Props.CopyFrom(descriptor.Props);
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeImageEffectInstance);
            Props.SetString(OfxConstants.ImageEffectPropContext, context);
            Props.SetInt(OfxConstants.PropIsInteractive, 0);
            Props.SetDoubleN(OfxConstants.ImageEffectPropProjectSize, width, height);
            Props.SetDoubleN(OfxConstants.ImageEffectPropProjectOffset, 0, 0);
            Props.SetDoubleN(OfxConstants.ImageEffectPropProjectExtent, width, height);
            Props.SetDouble(OfxConstants.ImageEffectPropProjectPixelAspectRatio, 1);
            Props.SetDouble(OfxConstants.ImageEffectInstancePropEffectDuration, durationFrames);
            Props.SetInt(OfxConstants.ImageEffectInstancePropSequentialRender, 0);
            Props.SetDouble(OfxConstants.ImageEffectPropFrameRate, frameRate);
            Props.SetPointer(OfxConstants.PropInstanceData, 0);
            // インスタンス生成完了時点の値を propReset の復元先にする（CopyFrom後の再スナップショット）
            Props.SealDefaults();

            // ディスクリプタのパラメータ定義からインスタンスパラメータを複製し、既定値で初期化する
            foreach (var definition in descriptor.ParamSet.Parameters)
            {
                var param = ParamSet.Define(definition.ParamType, definition.Name);
                param.Props.CopyFrom(definition.Props);
                // 規格上、インスタンスのパラメータの kOfxPropType はディスクリプタと異なる
                param.Props.SetString(OfxConstants.PropType, OfxConstants.TypeParameterInstance);
                // describe時にプラグインがアニメーション対応へ上書きしていても、
                // 本ホストは時刻指定取得（paramGetValueAtTime）へ応えられないため非対応で確定する
                param.Props.SetInt(OfxConstants.ParamPropAnimates, 0);
                param.Props.SealDefaults();
                param.EnsureInstanceValues();
            }

            foreach (var clipDescriptor in descriptor.Clips)
                clips.Add(new OfxClipInstance(clipDescriptor, context, width, height, frameRate, durationFrames));
        }

        public OfxClipInstance? FindClip(string name) => clips.FirstOrDefault(c => c.Name == name);
        public OfxParam? FindParam(string name) => ParamSet.Find(name);

        /// <summary>
        /// kOfxActionCreateInstance を実行する（コンストラクタでパラメータ・クリップを構築済みであること）
        /// </summary>
        public void Create()
        {
            if (isCreated)
                return;
            var status = plugin.CallAction(OfxConstants.ActionCreateInstance, Handle, 0, 0);
            if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                throw new InvalidOperationException($"kOfxActionCreateInstance が失敗しました。plugin={plugin.Identifier} status={status}");
            isCreated = true;
        }

        //====================================================================
        // パラメータ値の設定（YMM4側からの反映用）
        //====================================================================

        public void SetDoubleParam(string name, params double[] values)
        {
            if (FindParam(name) is not { DoubleValues: { } doubles })
                return;
            for (var i = 0; i < Math.Min(values.Length, doubles.Length); i++)
            {
                if (doubles[i] != values[i])
                    changedParams.Add(name);
                doubles[i] = values[i];
            }
        }

        public void SetIntParam(string name, params int[] values)
        {
            if (FindParam(name) is not { IntValues: { } ints })
                return;
            for (var i = 0; i < Math.Min(values.Length, ints.Length); i++)
            {
                if (ints[i] != values[i])
                    changedParams.Add(name);
                ints[i] = values[i];
            }
        }

        public void SetBoolParam(string name, bool value) => SetIntParam(name, value ? 1 : 0);

        public void SetStringParam(string name, string value)
        {
            if (FindParam(name) is not { IsStringType: true } param)
                return;
            if (!string.Equals(param.StringValue, value, StringComparison.Ordinal))
                changedParams.Add(name);
            param.StringValue = value;
        }

        /// <summary>
        /// 前回の通知以降に値が変わったパラメータを kOfxActionInstanceChanged でプラグインへ通知する
        /// （プラグインがパラメータ変更を契機に内部状態を更新する契約への対応）
        /// </summary>
        void NotifyChangedParams(double time)
        {
            if (changedParams.Count == 0)
                return;
            using var bracketArgs = new OfxPropertySet { DebugName = "instanceChanged.bracketArgs" };
            bracketArgs.SetString(OfxConstants.PropChangeReason, OfxConstants.ChangeUserEdited);
            plugin.CallAction(OfxConstants.ActionBeginInstanceChanged, Handle, bracketArgs.Handle, 0);
            foreach (var name in changedParams)
            {
                using var args = new OfxPropertySet { DebugName = "instanceChanged.inArgs" };
                args.SetString(OfxConstants.PropType, OfxConstants.TypeParameter);
                args.SetString(OfxConstants.PropName, name);
                args.SetString(OfxConstants.PropChangeReason, OfxConstants.ChangeUserEdited);
                args.SetDouble(OfxConstants.PropTime, time);
                args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
                plugin.CallAction(OfxConstants.ActionInstanceChanged, Handle, args.Handle, 0);
            }
            plugin.CallAction(OfxConstants.ActionEndInstanceChanged, Handle, bracketArgs.Handle, 0);
            changedParams.Clear();
        }

        //====================================================================
        // レンダリング
        //====================================================================

        /// <summary>
        /// プラグインが宣言する出力の定義域（RoD）を取得する。
        /// ぼかし・グロー等は入力より大きな領域を返すため、出力バッファはこの矩形で確保する。
        /// アクション未対応・異常値の場合は入力と同じ矩形へフォールバックする
        /// </summary>
        public OfxRectI GetRegionOfDefinition(double time)
        {
            var fallback = new OfxRectI { x1 = 0, y1 = 0, x2 = Width, y2 = Height };
            Create();
            // パラメータ変更から内部状態を更新するプラグインがあるため、RoDの問い合わせより先に通知する
            NotifyChangedParams(time);
            try
            {
                using var inArgs = new OfxPropertySet { DebugName = "getRoD.inArgs" };
                inArgs.SetDouble(OfxConstants.PropTime, time);
                inArgs.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
                using var outArgs = new OfxPropertySet { DebugName = "getRoD.outArgs" };
                outArgs.SetDoubleN(OfxConstants.ImageEffectPropRegionOfDefinition, 0, 0, Width, Height);
                var status = plugin.CallAction(OfxConstants.ImageEffectActionGetRegionOfDefinition, Handle, inArgs.Handle, outArgs.Handle);
                if (status is not OfxStatus.OK)
                    return fallback;
                var rod = outArgs.GetDoubles(OfxConstants.ImageEffectPropRegionOfDefinition);
                if (rod.Length < 4 || !double.IsFinite(rod[0]) || !double.IsFinite(rod[1]) || !double.IsFinite(rod[2]) || !double.IsFinite(rod[3]))
                    return fallback;
                // 無限RoD（kOfxFlagInfinite）や極端な拡張は入力周辺へクランプする
                // （拡張分は出力バッファの確保量に直結するため、辺ごとの上限で総量を抑える）
                const int maxExpansion = 1024;
                var result = new OfxRectI
                {
                    x1 = (int)Math.Floor(Math.Clamp(rod[0], -maxExpansion, Width + maxExpansion)),
                    y1 = (int)Math.Floor(Math.Clamp(rod[1], -maxExpansion, Height + maxExpansion)),
                    x2 = (int)Math.Ceiling(Math.Clamp(rod[2], -maxExpansion, Width + maxExpansion)),
                    y2 = (int)Math.Ceiling(Math.Clamp(rod[3], -maxExpansion, Height + maxExpansion)),
                };
                if (result.x2 <= result.x1 || result.y2 <= result.y1)
                    return fallback;
                return result;
            }
            catch (Exception e)
            {
                OfxHostLog.Info($"GetRegionOfDefinitionに失敗しました。plugin={plugin.Identifier}: {e.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// premultiplied BGRA（上から下への行順）の入力を処理して同形式の出力を得る（出力は入力と同じ矩形）。
        /// </summary>
        public void Render(ReadOnlySpan<byte> sourceBgraTopDown, Span<byte> outputBgraTopDown, double time)
            => Render(sourceBgraTopDown, outputBgraTopDown, time, new OfxRectI { x1 = 0, y1 = 0, x2 = Width, y2 = Height });

        /// <summary>
        /// premultiplied BGRA（上から下への行順）の入力を処理して同形式の出力を得る。
        /// 内部でOFX標準の RGBA float（下から上への行順）へ変換してrenderアクションを駆動する。
        /// 出力バッファは renderWindow（OFX座標。通常は <see cref="GetRegionOfDefinition"/> の結果）のサイズ
        /// </summary>
        public void Render(ReadOnlySpan<byte> sourceBgraTopDown, Span<byte> outputBgraTopDown, double time, OfxRectI renderWindow)
        {
            ValidateRenderWindow(renderWindow, outputBgraTopDown.Length);
            ValidateInputBuffer(sourceBgraTopDown.Length);
            Create();
            NotifyChangedParams(time);

            // プール画像は内容がレンダリング毎に変わるため、画像の同一性を表す識別子も毎回更新する
            // （固定のままだと、識別子で画像をキャッシュするプラグインが前フレームの結果を返しうる）
            renderSerial++;
            var sourceImage = PrepareInputImage(OfxConstants.ImageEffectSimpleSourceClipName, sourceBgraTopDown);
            var outputImage = PrepareOutputImage(renderWindow);
            RunRenderSequence(
                time,
                renderWindow,
                [(FindRequiredClip(OfxConstants.ImageEffectSimpleSourceClipName), sourceImage)],
                outputImage);
            OfxFrameConverter.RgbaBottomUpToBgraTopDown(outputImage.Data, outputBgraTopDown, outputImage.Width, outputImage.Height);
        }

        /// <summary>
        /// トランジションコンテキストのレンダリング。SourceFrom / SourceTo の2入力（premultiplied BGRA・上から下への行順）を
        /// 処理して同形式の出力を得る。進行度は事前に Transition パラメータ
        /// （<see cref="OfxConstants.ImageEffectTransitionParamName"/>）へ設定しておくこと
        /// </summary>
        public void RenderTransition(ReadOnlySpan<byte> fromBgraTopDown, ReadOnlySpan<byte> toBgraTopDown, Span<byte> outputBgraTopDown, double time, OfxRectI renderWindow)
        {
            ValidateRenderWindow(renderWindow, outputBgraTopDown.Length);
            ValidateInputBuffer(fromBgraTopDown.Length);
            ValidateInputBuffer(toBgraTopDown.Length);
            Create();
            NotifyChangedParams(time);

            renderSerial++;
            var fromImage = PrepareInputImage(OfxConstants.ImageEffectTransitionSourceFromClipName, fromBgraTopDown);
            var toImage = PrepareInputImage(OfxConstants.ImageEffectTransitionSourceToClipName, toBgraTopDown);
            var outputImage = PrepareOutputImage(renderWindow);
            RunRenderSequence(
                time,
                renderWindow,
                [
                    (FindRequiredClip(OfxConstants.ImageEffectTransitionSourceFromClipName), fromImage),
                    (FindRequiredClip(OfxConstants.ImageEffectTransitionSourceToClipName), toImage),
                ],
                outputImage);
            OfxFrameConverter.RgbaBottomUpToBgraTopDown(outputImage.Data, outputBgraTopDown, outputImage.Width, outputImage.Height);
        }

        void ValidateRenderWindow(OfxRectI renderWindow, int outputBufferLength)
        {
            var outputWidth = renderWindow.x2 - renderWindow.x1;
            var outputHeight = renderWindow.y2 - renderWindow.y1;
            if (outputWidth <= 0 || outputHeight <= 0)
                throw new ArgumentException("renderWindowが空です。");
            if (outputBufferLength < (long)outputWidth * outputHeight * 4)
                throw new ArgumentException("画像バッファのサイズが不足しています。");
        }

        void ValidateInputBuffer(int inputBufferLength)
        {
            if (inputBufferLength < (long)Width * Height * 4)
                throw new ArgumentException("画像バッファのサイズが不足しています。");
        }

        /// <summary>
        /// クリップ名ごとのプール入力画像へBGRA入力を変換して詰める。
        /// プール画像はフレーム間でゼロ初期化しない（入力は毎回変換で全書き込みされる）
        /// </summary>
        OfxImage PrepareInputImage(string clipName, ReadOnlySpan<byte> sourceBgraTopDown)
        {
            if (!pooledInputImages.TryGetValue(clipName, out var image))
            {
                image = new OfxImage(Width, Height, 0, 0, $"{plugin.Identifier}/{clipName}");
                pooledInputImages.Add(clipName, image);
            }
            image.Props.SetString(OfxConstants.ImagePropUniqueIdentifier, $"{plugin.Identifier}/{clipName}#{renderSerial}");
            OfxFrameConverter.BgraTopDownToRgbaBottomUp(sourceBgraTopDown, image.Data, Width, Height);
            return image;
        }

        /// <summary>
        /// renderWindowサイズのプール出力画像を用意する（renderWindow全域を埋めるのはプラグイン側の契約）
        /// </summary>
        OfxImage PrepareOutputImage(OfxRectI renderWindow)
        {
            var outputWidth = renderWindow.x2 - renderWindow.x1;
            var outputHeight = renderWindow.y2 - renderWindow.y1;
            if (pooledOutputImage is null
                || pooledOutputImage.Width != outputWidth
                || pooledOutputImage.Height != outputHeight
                || pooledOutputImage.OffsetX != renderWindow.x1
                || pooledOutputImage.OffsetY != renderWindow.y1)
            {
                pooledOutputImage?.Dispose();
                pooledOutputImage = null;
                pooledOutputImage = new OfxImage(outputWidth, outputHeight, renderWindow.x1, renderWindow.y1, $"{plugin.Identifier}/Output");
            }
            pooledOutputImage.Props.SetString(OfxConstants.ImagePropUniqueIdentifier, $"{plugin.Identifier}/Output#{renderSerial}");
            return pooledOutputImage;
        }

        OfxClipInstance FindRequiredClip(string name)
            => FindClip(name)
                ?? throw new InvalidOperationException($"コンテキストに必要なクリップが定義されていません。plugin={plugin.Identifier} clip={name}");

        /// <summary>
        /// 入力・出力クリップへ画像を差し込み、Begin/EndSequenceRenderで括ってrenderアクションを駆動する
        /// </summary>
        void RunRenderSequence(double time, OfxRectI renderWindow, (OfxClipInstance Clip, OfxImage Image)[] inputs, OfxImage outputImage)
        {
            var outputClip = FindRequiredClip(OfxConstants.ImageEffectOutputClipName);

            foreach (var (clip, image) in inputs)
            {
                clip.CurrentImage = image;
                clip.CurrentTime = time;
            }
            outputClip.CurrentImage = outputImage;
            outputClip.CurrentTime = time;
            try
            {
                using var sequenceArgs = CreateSequenceRenderArgs(time);
                var beginStatus = plugin.CallAction(OfxConstants.ImageEffectActionBeginSequenceRender, Handle, sequenceArgs.Handle, 0);
                if (beginStatus is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                    throw new InvalidOperationException($"kOfxImageEffectActionBeginSequenceRender が失敗しました。plugin={plugin.Identifier} status={beginStatus}");

                try
                {
                    using var renderArgs = CreateRenderArgs(time, renderWindow);
                    var status = plugin.CallAction(OfxConstants.ImageEffectActionRender, Handle, renderArgs.Handle, 0);
                    // renderは必須実装のため kOfxStatReplyDefault（未処理）も失敗として扱う
                    // （成功扱いすると未描画のプール出力バッファがそのまま表示される）
                    if (status is not OfxStatus.OK)
                        throw new InvalidOperationException($"kOfxImageEffectActionRender が失敗しました。plugin={plugin.Identifier} status={status}");
                }
                finally
                {
                    // renderが失敗してもBegin/Endの対応を崩さない（シーケンス状態を持つプラグインが復帰不能になるため）
                    var endStatus = plugin.CallAction(OfxConstants.ImageEffectActionEndSequenceRender, Handle, sequenceArgs.Handle, 0);
                    if (endStatus is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                        OfxHostLog.Info($"kOfxImageEffectActionEndSequenceRender が失敗しました。plugin={plugin.Identifier} status={endStatus}");
                }
            }
            finally
            {
                foreach (var (clip, _) in inputs)
                    clip.CurrentImage = null;
                outputClip.CurrentImage = null;
            }
        }

        OfxPropertySet CreateRenderArgs(double time, OfxRectI renderWindow)
        {
            var args = new OfxPropertySet { DebugName = "render.inArgs" };
            args.SetDouble(OfxConstants.PropTime, time);
            args.SetString(OfxConstants.ImageEffectPropFieldToRender, OfxConstants.ImageFieldNone);
            args.SetIntN(OfxConstants.ImageEffectPropRenderWindow, renderWindow.x1, renderWindow.y1, renderWindow.x2, renderWindow.y2);
            args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            args.SetInt(OfxConstants.ImageEffectPropSequentialRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropInteractiveRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropRenderQualityDraft, 0);
            return args;
        }

        OfxPropertySet CreateSequenceRenderArgs(double time)
        {
            var args = new OfxPropertySet { DebugName = "sequenceRender.inArgs" };
            args.SetDoubleN(OfxConstants.ImageEffectPropFrameRange, time, time);
            args.SetDouble(OfxConstants.ImageEffectPropFrameStep, 1);
            args.SetInt(OfxConstants.PropIsInteractive, 0);
            args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            args.SetInt(OfxConstants.ImageEffectPropSequentialRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropInteractiveRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropRenderQualityDraft, 0);
            return args;
        }

        public override void Dispose()
        {
            // destroyInstanceで画像ポインタへ触るプラグインに備え、画像の解放はアクションの後に行う
            if (isCreated)
            {
                isCreated = false;
                var status = plugin.CallAction(OfxConstants.ActionDestroyInstance, Handle, 0, 0);
                if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                    OfxHostLog.Info($"kOfxActionDestroyInstance が失敗しました。plugin={plugin.Identifier} status={status}");
            }
            foreach (var image in pooledInputImages.Values)
                image.Dispose();
            pooledInputImages.Clear();
            pooledOutputImage?.Dispose();
            pooledOutputImage = null;
            foreach (var clip in clips)
                clip.Dispose();
            clips.Clear();
            ParamSet.Dispose();
            Props.Dispose();
            base.Dispose();
        }
    }
}
