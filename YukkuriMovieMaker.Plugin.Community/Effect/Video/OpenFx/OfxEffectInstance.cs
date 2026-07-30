using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

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
        /// 映像エフェクト・場面切り替えの双方から同じロックで直列化する
        /// </summary>
        internal static readonly object UnsafeRenderLock = new();

        readonly OfxImageEffectPlugin plugin;
        readonly List<OfxClipInstance> clips = [];
        readonly HashSet<string> changedParams = [];
        readonly HashSet<string> loggedClipPreferencesWarnings = [];
        // 前回の GetClipPreferences 以降に値が変わったパラメータ名（ホスト起点＝changedParams からの引き継ぎと、
        // プラグイン起点＝paramSetValue 系のフックの両方）。プラグインはマルチスレッドスイートの
        // ワーカースレッドからも paramSetValue を呼びうるため、このセット自身をロックして読み書きする
        readonly HashSet<string> paramsChangedForClipPreferences = [];
        string outputPreMultiplication = OfxConstants.ImagePreMultiplied;
        bool isCreated;

        // フレーム毎の大きなネイティブ確保を避けるため、クリップ画像はサイズが変わるまで使い回す
        readonly Dictionary<string, OfxImage> pooledInputImages = [];
        OfxImage? pooledOutputImage;
        readonly Dictionary<string, OfxImage> pooledGpuInputImages = [];
        OfxImage? pooledGpuOutputImage;
        readonly object gpuBackendLock = new();
        readonly Func<IOfxGpuRenderBackend?>? gpuBackendFactory;
        IOfxGpuRenderBackend? gpuBackend;
        bool hasAttemptedGpuBackendCreation;
        bool hasLoggedGpuFailure;
        bool hasLoggedD3D11SurfaceFailure;
        bool isD3D11SurfaceUnavailable;
        bool createInstanceUsedGpuContext;
        bool isDisposed;
        long renderSerial;
        long parameterVersion;
        long currentTimeBits;
        GpuAttemptSnapshot? failedGpuSnapshot;
        long gpuFailureParameterVersion = -1;
        int consecutivePluginGpuFailures;
        int consecutivePluginGpuFailuresAcrossParameters;
        bool hasAbandonedGpuRendering;
        bool lastUseGpuRendering;
        bool hasGpuBackendFailed;
        int gpuSettingChangePending;
        GpuAttemptSnapshot? preparedDirectRenderSnapshot;
        const int MaxConsecutivePluginGpuFailures = 3;
        const int MaxConsecutivePluginGpuFailuresAcrossParameters = 10;

        public OfxPropertySet Props { get; }
        public OfxParamSet ParamSet { get; } = new();
        public IReadOnlyList<OfxClipInstance> Clips => clips;
        public int Width { get; }
        public int Height { get; }
        public double FrameRate { get; }
        public double DurationFrames { get; }
        /// <summary>TimeLine Suiteが返す、現在駆動中のOFX時刻</summary>
        public double CurrentTime
        {
            get => BitConverter.Int64BitsToDouble(Volatile.Read(ref currentTimeBits));
            private set => Volatile.Write(ref currentTimeBits, BitConverter.DoubleToInt64Bits(value));
        }

        internal bool CanUseD3D11Interop
        {
            get
            {
                UpdateGpuSettingState();
                if (!OpenFxSettings.Default.UseGpuRendering || isD3D11SurfaceUnavailable)
                {
                    return false;
                }
                lock (gpuBackendLock)
                {
                    if (failedGpuSnapshot is not null || hasAbandonedGpuRendering)
                        return false;
                }
                EnsureGpuBackend();
                return gpuBackend is IOfxD3D11InteropBackend { IsD3D11InteropAvailable: true }
                    && IsGpuBackendSupported(gpuBackend, Props);
            }
        }

#if DEBUG
        internal bool HasPreparedDirectRenderSnapshotForTest => preparedDirectRenderSnapshot is not null;
        internal long RenderSerialForTest => renderSerial;
        internal static int RenderIterationsForTest { get; set; } = 1;
#endif

        internal void OnD3D11SurfaceUnavailable(SharpGen.Runtime.SharpGenException exception)
            => OnD3D11SurfaceUnavailable(exception.Message);

        internal void OnD3D11SurfaceUnavailable(string error)
        {
            isD3D11SurfaceUnavailable = true;
            if (gpuBackend is IOfxD3D11InteropBackend interop)
                interop.ReleaseD3D11Resources();
            if (hasLoggedD3D11SurfaceFailure)
                return;
            hasLoggedD3D11SurfaceFailure = true;
            OfxHostLog.Info($"OpenFX用D3D11リソースを取得できないため、このエフェクトインスタンスではCPU経路を使用します。error={error}");
        }

        internal void ReleaseD3D11Resource(nint d3d11Resource)
        {
            if (gpuBackend is IOfxD3D11InteropBackend interop)
                interop.ReleaseD3D11Resource(d3d11Resource);
        }

        /// <summary>
        /// GetClipPreferences でプラグインが宣言した出力のpremultiplication状態
        /// （<see cref="OfxConstants.ImagePreMultiplied"/> / <see cref="OfxConstants.ImageUnPreMultiplied"/> /
        /// <see cref="OfxConstants.ImageOpaque"/>。未宣言時は premultiplied）
        /// </summary>
        public string OutputPreMultiplication => outputPreMultiplication;

        /// <summary>GetClipPreferences を問い合わせた回数（スレーブパラメータ契約のテスト用）</summary>
        internal int ClipPreferencesQueryCount { get; private set; }

        public OfxEffectInstance(
            OfxImageEffectPlugin plugin,
            string context,
            int width,
            int height,
            double frameRate,
            double durationFrames,
            IOfxGpuRenderBackend? gpuBackend = null,
            Func<IOfxGpuRenderBackend?>? gpuBackendFactory = null)
        {
            this.plugin = plugin;
            this.gpuBackend = gpuBackend;
            this.gpuBackendFactory = gpuBackendFactory;
            lastUseGpuRendering = OpenFxSettings.Default.UseGpuRendering;
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
            var isCpuRenderSupported = !descriptor.Props.GetStringOrDefault(OfxConstants.ImageEffectPropCPURenderSupported, "true")
                .Equals("false", StringComparison.OrdinalIgnoreCase);
            if (!isCpuRenderSupported
                && (!OpenFxSettings.Default.UseGpuRendering
                    || (!IsGpuBackendSupported(gpuBackend, descriptor.Props)
                        && (gpuBackendFactory is null || !IsSupportedGpuRenderingDeclared(descriptor.Props)))))
            {
                throw new InvalidOperationException($"CPUレンダリング非対応かつ利用可能なGPUバックエンドがないプラグインは未対応です。plugin={plugin.Identifier}");
            }
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
            if (!isCpuRenderSupported && !IsGpuBackendSupported(gpuBackend, descriptor.Props))
            {
                hasAttemptedGpuBackendCreation = true;
                var created = gpuBackendFactory!();
                if (!IsGpuBackendSupported(created, descriptor.Props))
                {
                    created?.ReleaseDeviceResources();
                    created?.Dispose();
                    throw new InvalidOperationException($"CPUレンダリング非対応かつGPUバックエンドの生成に失敗したプラグインは未対応です。plugin={plugin.Identifier}");
                }
                Volatile.Write(ref this.gpuBackend, created);
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
                // プラグインが paramSetValue 系で書き換えたスレーブパラメータも
                // GetClipPreferences の再問い合わせ判定に含める（規格はホスト起点の変更に限定していない）
                param.PluginValueSet = p =>
                {
                    lock (paramsChangedForClipPreferences)
                        paramsChangedForClipPreferences.Add(p.Name);
                };
            }

            foreach (var clipDescriptor in descriptor.Clips)
                clips.Add(new OfxClipInstance(clipDescriptor, context, width, height, frameRate, durationFrames));
            OpenFxSettings.Default.PropertyChanged += OnOpenFxSettingsPropertyChanged;
        }

        /// <summary>
        /// 5ホスト共通の生成入口。バックエンド初期化後にインスタンス構築が失敗してもGPU資源を漏らさない。
        /// </summary>
        public static OfxEffectInstance CreateWithGpuBackend(
            OfxImageEffectPlugin plugin,
            string context,
            int width,
            int height,
            double frameRate,
            double durationFrames,
            IGraphicsDevicesAndContext devices)
        {
            return new OfxEffectInstance(
                plugin,
                context,
                width,
                height,
                frameRate,
                durationFrames,
                gpuBackendFactory: () => OfxGpuRenderBackendFactory.Create(devices, plugin.DescribeInContext(context).Props));
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
            UpdateGpuSettingState();
            EnsureGpuBackend();
            createInstanceUsedGpuContext = IsGpuBackendSupported(gpuBackend, Props);
            var status = CallInstanceAction(OfxConstants.ActionCreateInstance, 0, 0, createInstanceUsedGpuContext);
            if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                throw new InvalidOperationException($"kOfxActionCreateInstance が失敗しました。plugin={plugin.Identifier} status={status}");
            isCreated = true;
            QueryClipPreferences();
        }

        //====================================================================
        // クリップ形式の希望（kOfxImageEffectActionGetClipPreferences）
        //====================================================================

        /// <summary>
        /// kOfxImageEffectActionGetClipPreferences でクリップ形式の希望をプラグインへ問い合わせる。
        /// 本ホストは RGBA float・PAR=1 固定のため、尊重するのは出力のpremultiplicationのみ
        /// （unpremultiplied宣言はBGRA変換時にアルファを乗算し、opaque宣言はアルファを1へ確定する）。
        /// それ以外の宣言外の要求（コンポーネント・深度・PAR・フレームレート・フィールド順の変更）は
        /// ログへ記録して無視する。呼び出しはインスタンス生成後と、
        /// kOfxImageEffectPropClipPreferencesSlaveParam に列挙されたパラメータの変更後（規格の契約）。
        /// アクションが失敗（kOfxStatFailed / kOfxStatErrMemory 等）した場合は直前の宣言を維持する
        /// （kOfxStatReplyDefault の「既定premultipliedへの復帰」とは非対称。再試行は次の問い合わせ契機まで行わない）
        /// </summary>
        void QueryClipPreferences()
        {
            if (!isCreated)
                return;
            // これから問い合わせる結果は現時点までの全変更を反映するため、保留中の変更記録は消化済みにする
            // （createInstance中にプラグインがスレーブパラメータを設定した場合の重複問い合わせ防止。
            // アクション実行中の paramSetValue はこのクリアの後に記録され、次回の判定へ回る。
            // getClipPreferences内でスレーブ値を書き換え続けるプラグインでも、再問い合わせは
            // NotifyChangedParams の呼び出し（RoD・IsIdentity・renderの駆動時）ごとに高々1回＝
            // 毎フレーム数回で有界）
            lock (paramsChangedForClipPreferences)
                paramsChangedForClipPreferences.Clear();
            ClipPreferencesQueryCount++;
            try
            {
                // outArgs はホストの既定値（現在クリップへ供給している形式）で埋めてから渡す
                using var outArgs = new OfxPropertySet { DebugName = "getClipPreferences.outArgs" };
                foreach (var clip in clips)
                {
                    outArgs.SetString(OfxConstants.ImageClipPropComponentsPrefix + clip.Name, OfxConstants.ImageComponentRGBA);
                    outArgs.SetString(OfxConstants.ImageClipPropDepthPrefix + clip.Name, OfxConstants.BitDepthFloat);
                    outArgs.SetDouble(OfxConstants.ImageClipPropPARPrefix + clip.Name, 1);
                }
                outArgs.SetDouble(OfxConstants.ImageEffectPropFrameRate, FrameRate);
                outArgs.SetString(OfxConstants.ImageClipPropFieldOrder, OfxConstants.ImageFieldNone);
                outArgs.SetString(OfxConstants.ImageEffectPropPreMultiplication, OfxConstants.ImagePreMultiplied);
                outArgs.SetInt(OfxConstants.ImageClipPropContinuousSamples, 0);
                outArgs.SetInt(OfxConstants.ImageEffectFrameVarying, 0);

                var status = plugin.CallAction(OfxConstants.ImageEffectActionGetClipPreferences, Handle, 0, outArgs.Handle);
                if (status is OfxStatus.ReplyDefault)
                {
                    // 未処理＝既定値の使用（規格）。以前の宣言が残っていれば既定へ戻す
                    ApplyOutputPreMultiplication(OfxConstants.ImagePreMultiplied);
                    return;
                }
                if (status is not OfxStatus.OK)
                {
                    LogClipPreferencesWarningOnce("status", $"kOfxImageEffectActionGetClipPreferences が失敗しました。現在の形式を継続します。plugin={plugin.Identifier} status={status}");
                    return;
                }

                // 供給できない形式の要求は無視する（本ホストはRGBA float・PAR=1のみ宣言しており、
                // 規格上プラグインはホスト宣言の範囲から選ぶ契約。宣言外の要求＝規格違反）
                foreach (var clip in clips)
                {
                    var components = outArgs.GetStringOrDefault(OfxConstants.ImageClipPropComponentsPrefix + clip.Name, OfxConstants.ImageComponentRGBA);
                    if (components != OfxConstants.ImageComponentRGBA)
                        LogClipPreferencesWarningOnce($"components:{clip.Name}", $"GetClipPreferencesのRGBA以外のコンポーネント要求は未対応のため無視します。plugin={plugin.Identifier} clip={clip.Name} components={components}");
                    var depth = outArgs.GetStringOrDefault(OfxConstants.ImageClipPropDepthPrefix + clip.Name, OfxConstants.BitDepthFloat);
                    if (depth != OfxConstants.BitDepthFloat)
                        LogClipPreferencesWarningOnce($"depth:{clip.Name}", $"GetClipPreferencesのfloat以外のピクセル深度要求は未対応のため無視します。plugin={plugin.Identifier} clip={clip.Name} depth={depth}");
                    var pixelAspectRatio = outArgs.GetDoubleOrDefault(OfxConstants.ImageClipPropPARPrefix + clip.Name, 1);
                    if (pixelAspectRatio != 1)
                        LogClipPreferencesWarningOnce($"par:{clip.Name}", $"GetClipPreferencesのピクセルアスペクト比の変更要求は未対応のため無視します。plugin={plugin.Identifier} clip={clip.Name} par={pixelAspectRatio.ToString(CultureInfo.InvariantCulture)}");
                }
                var preferredFrameRate = outArgs.GetDoubleOrDefault(OfxConstants.ImageEffectPropFrameRate, FrameRate);
                if (preferredFrameRate != FrameRate)
                    LogClipPreferencesWarningOnce("frameRate", $"GetClipPreferencesのフレームレート変更要求は未対応のため無視します（kOfxImageEffectPropSetableFrameRate=0）。plugin={plugin.Identifier}");
                var fieldOrder = outArgs.GetStringOrDefault(OfxConstants.ImageClipPropFieldOrder, OfxConstants.ImageFieldNone);
                if (fieldOrder != OfxConstants.ImageFieldNone)
                    LogClipPreferencesWarningOnce("fieldOrder", $"GetClipPreferencesのフィールド順変更要求は未対応のため無視します（kOfxImageEffectPropSetableFielding=0）。plugin={plugin.Identifier}");
                // ContinuousSamples / FrameVarying はキャッシュ制御のヒント。本ホストは毎フレーム再レンダリングするため読み捨てる

                var preMultiplication = outArgs.GetStringOrDefault(OfxConstants.ImageEffectPropPreMultiplication, OfxConstants.ImagePreMultiplied);
                if (preMultiplication is OfxConstants.ImagePreMultiplied or OfxConstants.ImageUnPreMultiplied or OfxConstants.ImageOpaque)
                    ApplyOutputPreMultiplication(preMultiplication);
                else
                    LogClipPreferencesWarningOnce("premultiplication", $"GetClipPreferencesのpremultiplication宣言が不正なため無視します。plugin={plugin.Identifier} value={preMultiplication}");
            }
            catch (Exception e)
            {
                LogClipPreferencesWarningOnce("exception", $"GetClipPreferencesに失敗しました。現在の形式を継続します。plugin={plugin.Identifier}: {e.Message}");
            }
        }

        /// <summary>
        /// 出力のpremultiplication宣言を反映する（出力クリップのプロパティと、BGRA変換時の扱いの両方）。
        /// クリップ側は保持フィールドではなく実際の格納値と比較する（プラグインが propReset 等で
        /// クリップ側だけ既定値へ戻していても、問い合わせのたびに宣言と同期させるため。
        /// 値が同じ間は書き込まず、propGetString 用のネイティブ文字列キャッシュの無駄な再確保を避ける）
        /// </summary>
        void ApplyOutputPreMultiplication(string preMultiplication)
        {
            outputPreMultiplication = preMultiplication;
            var outputClip = FindClip(OfxConstants.ImageEffectOutputClipName);
            if (outputClip is not null
                && outputClip.Props.GetStringOrDefault(OfxConstants.ImageEffectPropPreMultiplication, "") != preMultiplication)
            {
                outputClip.Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, preMultiplication);
            }
        }

        /// <summary>
        /// GetClipPreferences関連の警告を同一の要因（key）につき1回だけログへ記録する
        /// （スレーブパラメータのアニメーション中は毎フレーム再問い合わせになるため、ログの氾濫を防ぐ。
        /// keyにはPAR値のような可変値を含めないこと。値をキーにすると再問い合わせのたびに
        /// 別内容と判定され、抑制が効かないままHashSetも際限なく増える）
        /// </summary>
        void LogClipPreferencesWarningOnce(string key, string message)
        {
            if (loggedClipPreferencesWarnings.Add(key))
                OfxHostLog.Info(message);
        }

        //====================================================================
        // パラメータ値の設定（YMM4側からの反映用）
        //====================================================================

        public void SetDoubleParam(string name, params double[] values)
        {
            if (FindParam(name) is not { DoubleValues: { } doubles })
                return;
            var changed = false;
            for (var i = 0; i < Math.Min(values.Length, doubles.Length); i++)
            {
                if (doubles[i] != values[i])
                {
                    changedParams.Add(name);
                    changed = true;
                }
                doubles[i] = values[i];
            }
            if (changed)
                parameterVersion++;
        }

        public void SetIntParam(string name, params int[] values)
        {
            if (FindParam(name) is not { IntValues: { } ints })
                return;
            var changed = false;
            for (var i = 0; i < Math.Min(values.Length, ints.Length); i++)
            {
                if (ints[i] != values[i])
                {
                    changedParams.Add(name);
                    changed = true;
                }
                ints[i] = values[i];
            }
            if (changed)
                parameterVersion++;
        }

        public void SetBoolParam(string name, bool value) => SetIntParam(name, value ? 1 : 0);

        public void SetStringParam(string name, string value)
        {
            if (FindParam(name) is not { IsStringType: true } param)
                return;
            if (!string.Equals(param.StringValue, value, StringComparison.Ordinal))
            {
                changedParams.Add(name);
                parameterVersion++;
            }
            param.StringValue = value;
        }

        /// <summary>
        /// 前回の通知以降に値が変わったパラメータを kOfxActionInstanceChanged でプラグインへ通知する
        /// （プラグインがパラメータ変更を契機に内部状態を更新する契約への対応）
        /// </summary>
        void NotifyChangedParams(double time)
        {
            CurrentTime = time;
            if (changedParams.Count != 0)
            {
                using var bracketArgs = new OfxPropertySet { DebugName = "instanceChanged.bracketArgs" };
                bracketArgs.SetString(OfxConstants.PropChangeReason, OfxConstants.ChangeUserEdited);
                CallInstanceAction(OfxConstants.ActionBeginInstanceChanged, bracketArgs.Handle, 0);
                foreach (var name in changedParams)
                {
                    using var args = new OfxPropertySet { DebugName = "instanceChanged.inArgs" };
                    args.SetString(OfxConstants.PropType, OfxConstants.TypeParameter);
                    args.SetString(OfxConstants.PropName, name);
                    args.SetString(OfxConstants.PropChangeReason, OfxConstants.ChangeUserEdited);
                    args.SetDouble(OfxConstants.PropTime, time);
                    args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
                    CallInstanceAction(OfxConstants.ActionInstanceChanged, args.Handle, 0);
                }
                CallInstanceAction(OfxConstants.ActionEndInstanceChanged, bracketArgs.Handle, 0);
                // ホスト起点の変更を GetClipPreferences の再問い合わせ判定へ引き継いでからクリアする
                lock (paramsChangedForClipPreferences)
                {
                    foreach (var name in changedParams)
                        paramsChangedForClipPreferences.Add(name);
                }
                changedParams.Clear();
            }
            // プラグイン起点の変更（paramSetValue 系フック）だけのこともあるため、通知の有無に関わらず判定する
            RequeryClipPreferencesIfSlaveParamChanged();
        }

        /// <summary>
        /// 前回の GetClipPreferences 以降に変更されたパラメータ（ホスト起点・プラグイン起点の両方）に
        /// スレーブ宣言（kOfxImageEffectPropClipPreferencesSlaveParam）のパラメータが含まれる場合のみ
        /// 再問い合わせする（規格の契約。スレーブ外の変更はクリップ形式に影響しないため破棄する）
        /// </summary>
        void RequeryClipPreferencesIfSlaveParamChanged()
        {
            // Props（別ロックを持つ）へはロックの外から触れるよう、変更名はスナップショットで取り出す。
            // スナップショット後〜問い合わせまでの間に入った paramSetValue の取りこぼしは、
            // プラグインのワーカーがアクション実行中しか動かない前提で許容する
            // （この区間はアクションが走っていない。常駐スレッドを持つ規格違反プラグインのみ該当）
            string[] changedNames;
            lock (paramsChangedForClipPreferences)
            {
                if (paramsChangedForClipPreferences.Count == 0)
                    return;
                changedNames = [.. paramsChangedForClipPreferences];
                paramsChangedForClipPreferences.Clear();
            }
            foreach (var slaveParam in Props.GetStrings(OfxConstants.ImageEffectPropClipPreferencesSlaveParam))
            {
                if (Array.IndexOf(changedNames, slaveParam) >= 0)
                {
                    QueryClipPreferences();
                    return;
                }
            }
        }

        //====================================================================
        // レンダリング
        //====================================================================

        /// <summary>
        /// プラグインが宣言する出力の定義域（RoD）を取得する。
        /// ぼかし・グロー等は入力より大きな領域を返すため、出力バッファはこの矩形で確保する。
        /// アクション未対応・異常値の場合は入力と同じ矩形へフォールバックする。
        /// maxOutputSize には出力バッファの1辺の上限（D2Dの最大ビットマップサイズ等）を渡す。
        /// プロジェクトサイズが上限に近い場合、RoD拡張が上限を超えるとビットマップを確保できず
        /// レンダリング自体が失敗するため、プロジェクト矩形を優先して余白側から切り詰める
        /// </summary>
        public OfxRectI GetRegionOfDefinition(double time, int maxOutputSize = int.MaxValue)
        {
            CurrentTime = time;
            // フォールバック（プロジェクト矩形）も上限契約を守る
            var fallback = new OfxRectI { x1 = 0, y1 = 0, x2 = Width, y2 = Height };
            ClampSpan(ref fallback.x1, ref fallback.x2, Width, maxOutputSize);
            ClampSpan(ref fallback.y1, ref fallback.y2, Height, maxOutputSize);
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
                ClampSpan(ref result.x1, ref result.x2, Width, maxOutputSize);
                ClampSpan(ref result.y1, ref result.y2, Height, maxOutputSize);
                return result;
            }
            catch (Exception e)
            {
                OfxHostLog.Info($"GetRegionOfDefinitionに失敗しました。plugin={plugin.Identifier}: {e.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// RoDの1軸の範囲を上限サイズに収める。プロジェクト矩形（0..projectSize）を優先して残し、
        /// 拡張分（余白）を切り詰める
        /// </summary>
        internal static void ClampSpan(ref int min, ref int max, int projectSize, int limit)
        {
            if (max - min <= limit)
                return;
            var margin = Math.Max(0, limit - projectSize);
            var lo = Math.Max(min, -margin / 2);
            var hi = lo + limit;
            if (hi > max)
            {
                hi = max;
                lo = hi - limit;
            }
            min = lo;
            max = hi;
        }

        /// <summary>
        /// kOfxImageEffectActionIsIdentity で「恒等（効果なし）」かをプラグインへ問い合わせる。
        /// 恒等の場合は出力の代わりに使う入力クリップ名を返す（呼び出し元はrenderを省略して素通しする）。
        /// 非恒等（kOfxStatReplyDefault）・アクション失敗・不明なクリップ名の場合は null（通常レンダリング）。
        /// 別時刻の指定（time slip）は本ホストが現在フレームの画像しか供給できない
        /// （テンポラルアクセス非対応）ため、恒等扱いにせず null へ倒す。
        /// renderWindow には実際にレンダリングする矩形（通常は <see cref="GetRegionOfDefinition"/> の結果）を渡す
        /// </summary>
        public string? GetIdentityClipName(double time, OfxRectI renderWindow)
        {
            CurrentTime = time;
            // 恒等宣言はrenderWindowに対するもので、素通しは入力画像全体を表示する。
            // renderWindow（通常はRoD）が入力矩形より狭い場合、通常レンダリングなら出力されない
            // RoD外の画素まで素通しで表示されてしまうため、入力矩形全体を覆うときだけ恒等扱いにする
            // （RoD拡張側は透明余白が増えるだけで視覚的に等価なので許容する）
            if (renderWindow.x1 > 0 || renderWindow.y1 > 0 || renderWindow.x2 < Width || renderWindow.y2 < Height)
                return null;
            Create();
            // パラメータ変更から内部状態を更新するプラグインがあるため、問い合わせより先に通知する
            NotifyChangedParams(time);
            try
            {
                using var inArgs = new OfxPropertySet { DebugName = "isIdentity.inArgs" };
                inArgs.SetDouble(OfxConstants.PropTime, time);
                inArgs.SetString(OfxConstants.ImageEffectPropFieldToRender, OfxConstants.ImageFieldNone);
                inArgs.SetIntN(OfxConstants.ImageEffectPropRenderWindow, renderWindow.x1, renderWindow.y1, renderWindow.x2, renderWindow.y2);
                inArgs.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
                // outArgs は規格の既定値（クリップ名は空文字列・時刻は inArgs と同じ）で埋めてから渡す
                using var outArgs = new OfxPropertySet { DebugName = "isIdentity.outArgs" };
                outArgs.SetString(OfxConstants.PropName, "");
                outArgs.SetDouble(OfxConstants.PropTime, time);
                var status = plugin.CallAction(OfxConstants.ImageEffectActionIsIdentity, Handle, inArgs.Handle, outArgs.Handle);
                if (status is not OfxStatus.OK)
                    return null;
                var clipName = outArgs.GetStringOrDefault(OfxConstants.PropName, "");
                if (clipName.Length == 0
                    || clipName == OfxConstants.ImageEffectOutputClipName
                    || FindClip(clipName) is null)
                {
                    return null;
                }
                // 厳密比較を意図している（丸め等の微差もtime slip扱い＝安全側の通常レンダリングに倒す）
                if (outArgs.GetDoubleOrDefault(OfxConstants.PropTime, time) != time)
                    return null;
                return clipName;
            }
            catch (Exception e)
            {
                OfxHostLog.Info($"IsIdentityの問い合わせに失敗しました。通常レンダリングを継続します。plugin={plugin.Identifier}: {e.Message}");
                return null;
            }
        }

#if DEBUG
        /// <summary>
        /// レンダリング失敗経路のテスト用フォールトインジェクション。
        /// trueの間、Render / RenderTransition / RenderGenerator が例外を投げる
        /// </summary>
        internal static bool ThrowOnRenderForTest;
        /// <summary>
        /// GPU失敗後のCPU再試行も失敗する経路を検証するためのステータス注入。
        /// null以外の間、CPUのrenderアクション結果を指定値へ置き換える。
        /// </summary>
        static readonly System.Threading.AsyncLocal<int?> cpuRenderStatusForTest = new();
        internal static int? CpuRenderStatusForTest
        {
            get => cpuRenderStatusForTest.Value;
            set => cpuRenderStatusForTest.Value = value;
        }
#endif

        [System.Diagnostics.Conditional("DEBUG")]
        static void ThrowIfRenderFaultInjected()
        {
#if DEBUG
            if (ThrowOnRenderForTest)
                throw new InvalidOperationException("テスト用のレンダリング失敗（ThrowOnRenderForTest）");
#endif
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
            ThrowIfRenderFaultInjected();
            ValidateRenderWindow(renderWindow, outputBgraTopDown.Length);
            ValidateInputBuffer(sourceBgraTopDown.Length);
            PrepareCpuRender(time, renderWindow);
            var sourceImage = PrepareInputImage(OfxConstants.ImageEffectSimpleSourceClipName, sourceBgraTopDown);
            var outputImage = PrepareOutputImage(renderWindow);
            RunRenderSequence(
                time,
                renderWindow,
                [(FindRequiredClip(OfxConstants.ImageEffectSimpleSourceClipName), sourceImage)],
                outputImage);
            OfxFrameConverter.RgbaBottomUpToBgraTopDown(outputImage.Data, outputBgraTopDown, outputImage.Width, outputImage.Height, outputPreMultiplication);
        }

        /// <summary>
        /// D3D11のBGRA8入力・出力テクスチャをCPUへ戻さずCUDAでレンダリングする。
        /// interopを利用できない場合はfalseを返し、呼び出し元が既存CPU転送経路へ切り替える。
        /// </summary>
        public bool TryRenderD3D11(nint sourceResource, nint outputResource, double time, OfxRectI renderWindow)
            => TryRenderD3D11Core(
                [(FindRequiredClip(OfxConstants.ImageEffectSimpleSourceClipName), sourceResource)],
                outputResource,
                time,
                renderWindow);

        /// <summary>
        /// トランジションコンテキストのレンダリング。SourceFrom / SourceTo の2入力（premultiplied BGRA・上から下への行順）を
        /// 処理して同形式の出力を得る。進行度は事前に Transition パラメータ
        /// （<see cref="OfxConstants.ImageEffectTransitionParamName"/>）へ設定しておくこと
        /// </summary>
        public void RenderTransition(ReadOnlySpan<byte> fromBgraTopDown, ReadOnlySpan<byte> toBgraTopDown, Span<byte> outputBgraTopDown, double time, OfxRectI renderWindow)
        {
            ThrowIfRenderFaultInjected();
            ValidateRenderWindow(renderWindow, outputBgraTopDown.Length);
            ValidateInputBuffer(fromBgraTopDown.Length);
            ValidateInputBuffer(toBgraTopDown.Length);
            PrepareCpuRender(time, renderWindow);
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
            OfxFrameConverter.RgbaBottomUpToBgraTopDown(outputImage.Data, outputBgraTopDown, outputImage.Width, outputImage.Height, outputPreMultiplication);
        }

        public bool TryRenderTransitionD3D11(nint fromResource, nint toResource, nint outputResource, double time, OfxRectI renderWindow)
            => TryRenderD3D11Core(
                [
                    (FindRequiredClip(OfxConstants.ImageEffectTransitionSourceFromClipName), fromResource),
                    (FindRequiredClip(OfxConstants.ImageEffectTransitionSourceToClipName), toResource),
                ],
                outputResource,
                time,
                renderWindow);

        /// <summary>
        /// ジェネレーターコンテキストのレンダリング。入力なしで premultiplied BGRA（上から下への行順）の出力を得る。
        /// 出力バッファは renderWindow（OFX座標。通常は <see cref="GetRegionOfDefinition"/> の結果）のサイズ
        /// </summary>
        public void RenderGenerator(Span<byte> outputBgraTopDown, double time, OfxRectI renderWindow)
        {
            ThrowIfRenderFaultInjected();
            ValidateRenderWindow(renderWindow, outputBgraTopDown.Length);
            PrepareCpuRender(time, renderWindow);
            var outputImage = PrepareOutputImage(renderWindow);
            RunRenderSequence(time, renderWindow, [], outputImage);
            OfxFrameConverter.RgbaBottomUpToBgraTopDown(outputImage.Data, outputBgraTopDown, outputImage.Width, outputImage.Height, outputPreMultiplication);
        }

        public bool TryRenderGeneratorD3D11(nint outputResource, double time, OfxRectI renderWindow)
            => TryRenderD3D11Core([], outputResource, time, renderWindow);

        bool TryRenderD3D11Core(
            (OfxClipInstance Clip, nint Resource)[] inputs,
            nint outputResource,
            double time,
            OfxRectI renderWindow)
        {
            var snapshot = CaptureGpuAttemptSnapshot(time, renderWindow);
            if (!CanUseGpuBackend(snapshot)
                || gpuBackend is not IOfxD3D11InteropBackend { IsD3D11InteropAvailable: true } interop)
                return false;

            var preservePreparedSnapshotForCpuFallback = false;
            try
            {
                ThrowIfRenderFaultInjected();
                ValidateD3D11Resources(inputs, outputResource, renderWindow);
                Create();
                NotifyChangedParams(time);
                renderSerial++;
                preparedDirectRenderSnapshot = snapshot;

                var gpuInputs = new (OfxClipInstance Clip, OfxImage Image)[inputs.Length];
                for (var i = 0; i < inputs.Length; i++)
                {
                    var (clip, resource) = inputs[i];
                    var gpuImage = PrepareGpuInputImage(clip.Name, Width, Height, 0, 0);
                    interop.UploadFromD3D11(resource, gpuImage);
                    gpuInputs[i] = (clip, gpuImage);
                }

                var gpuOutput = PrepareGpuOutputImage(renderWindow);
                var status = RunRenderSequenceIterations(time, renderWindow, gpuInputs, gpuOutput, gpuBackend);
                if (status == OfxStatus.OK)
                {
                    interop.DownloadToD3D11(gpuOutput, outputResource, outputPreMultiplication);
                    ResetPluginGpuFailures();
                    return true;
                }
                if (!IsGpuFailureStatus(status))
                {
                    throw new InvalidOperationException($"kOfxImageEffectActionRender が失敗しました。plugin={plugin.Identifier} status={status}");
                }

                HandlePluginGpuFailure(status, snapshot);
                preservePreparedSnapshotForCpuFallback = true;
                return false;
            }
            catch (CudaInteropUnavailableException)
            {
                // D3D11共有だけの失敗。CUDAプラグインは既存のCPU転送経路で再試行できる。
                preservePreparedSnapshotForCpuFallback = preparedDirectRenderSnapshot == snapshot;
                return false;
            }
            catch (OpenClInteropUnavailableException)
            {
                // D3D11共有だけの失敗。OpenCLプラグインは既存のCPU転送経路で再試行できる。
                preservePreparedSnapshotForCpuFallback = preparedDirectRenderSnapshot == snapshot;
                return false;
            }
            catch (Exception e) when (IsBackendException(e))
            {
                HandleBackendGpuFailure(GetFallbackStatus(e), e);
                preservePreparedSnapshotForCpuFallback = true;
                return false;
            }
            catch (D3D11TextureValidationException e)
            {
                OnD3D11SurfaceUnavailable(e.Message);
                return false;
            }
            finally
            {
                if (!preservePreparedSnapshotForCpuFallback)
                    preparedDirectRenderSnapshot = null;
            }
        }

        void HandlePluginGpuFailure(int status, GpuAttemptSnapshot snapshot)
        {
            gpuBackend!.OnRenderFailed(status);
            ReleaseGpuImages();
            RecordPluginGpuFailure(snapshot);
            LogGpuFailureOnce(status);
            if (!IsCpuRenderSupported())
                throw new InvalidOperationException($"GPUレンダリングが失敗し、プラグインはCPUレンダリング非対応です。plugin={plugin.Identifier} status={status}");
        }

        void HandleBackendGpuFailure(int status, Exception exception)
        {
            SynchronizeGpuBackendBestEffort();
            ReleaseGpuImages();
            gpuBackend!.OnBackendFailed();
            lock (gpuBackendLock)
                hasGpuBackendFailed = true;
            LogGpuFailureOnce(status, exception);
            if (!IsCpuRenderSupported())
                throw new InvalidOperationException($"GPUレンダリングが失敗し、プラグインはCPUレンダリング非対応です。plugin={plugin.Identifier} status={status}", exception);
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
            // 出力画像のpremultiplicationはGetClipPreferencesの宣言に追従させる（クリップと画像で矛盾させない）。
            // 値が同じ間は書き込まず、propGetString 用のネイティブ文字列キャッシュの毎フレーム再確保を避ける
            if (pooledOutputImage.Props.GetStringOrDefault(OfxConstants.ImageEffectPropPreMultiplication, "") != outputPreMultiplication)
                pooledOutputImage.Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, outputPreMultiplication);
            return pooledOutputImage;
        }

        OfxClipInstance FindRequiredClip(string name)
            => FindClip(name)
                ?? throw new InvalidOperationException($"コンテキストに必要なクリップが定義されていません。plugin={plugin.Identifier} clip={name}");

        /// <summary>
        /// GPUが利用可能ならGPU画像でレンダリングし、GPU固有失敗時だけ同一呼び出し内でCPUへ再試行する。
        /// GPU成功時はCPU出力画像へダウンロードし、後段の既存BGRA変換を共通利用する。
        /// </summary>
        void RunRenderSequence(double time, OfxRectI renderWindow, (OfxClipInstance Clip, OfxImage Image)[] inputs, OfxImage outputImage)
        {
            var snapshot = CaptureGpuAttemptSnapshot(time, renderWindow);
            if (CanUseGpuBackend(snapshot))
            {
                var backend = gpuBackend!;
                try
                {
                    var gpuInputs = new (OfxClipInstance Clip, OfxImage Image)[inputs.Length];
                    for (var i = 0; i < inputs.Length; i++)
                    {
                        var (clip, cpuImage) = inputs[i];
                        var gpuImage = PrepareGpuInputImage(
                            clip.Name,
                            cpuImage.Width,
                            cpuImage.Height,
                            cpuImage.OffsetX,
                            cpuImage.OffsetY);
                        backend.Upload(cpuImage, gpuImage);
                        gpuInputs[i] = (clip, gpuImage);
                    }
                    var gpuOutput = PrepareGpuOutputImage(renderWindow);
                    var gpuStatus = RunRenderSequenceIterations(time, renderWindow, gpuInputs, gpuOutput, backend);
                    if (gpuStatus == OfxStatus.OK)
                    {
                        backend.Download(gpuOutput, outputImage);
                        ResetPluginGpuFailures();
                        return;
                    }

                    if (IsGpuFailureStatus(gpuStatus))
                    {
                        FallBackToCpu(gpuStatus, snapshot, time, renderWindow, inputs, outputImage);
                        return;
                    }

                    throw new InvalidOperationException($"kOfxImageEffectActionRender が失敗しました。plugin={plugin.Identifier} status={gpuStatus}");
                }
                catch (Exception e) when (IsBackendException(e))
                {
                    FallBackToCpu(GetFallbackStatus(e), snapshot, time, renderWindow, inputs, outputImage, e);
                    return;
                }
            }

            if (!IsCpuRenderSupported())
                throw new InvalidOperationException($"GPUレンダリングを利用できず、プラグインはCPUレンダリング非対応です。plugin={plugin.Identifier}");
            var status = RunRenderSequenceIterations(time, renderWindow, inputs, outputImage, null);
            if (status != OfxStatus.OK)
                throw new InvalidOperationException($"kOfxImageEffectActionRender が失敗しました。plugin={plugin.Identifier} status={status}");
        }

        bool CanUseGpuBackend(GpuAttemptSnapshot snapshot)
        {
            UpdateGpuSettingState();
            if (!OpenFxSettings.Default.UseGpuRendering)
                return false;
            EnsureGpuBackend();
            lock (gpuBackendLock)
            {
                return !hasAbandonedGpuRendering
                    && IsGpuBackendSupported(gpuBackend, Props)
                    && (gpuFailureParameterVersion != snapshot.ParameterVersion
                        || consecutivePluginGpuFailures < MaxConsecutivePluginGpuFailures)
                    && failedGpuSnapshot != snapshot;
            }
        }

        void RecordPluginGpuFailure(GpuAttemptSnapshot snapshot)
        {
            lock (gpuBackendLock)
            {
                failedGpuSnapshot = snapshot;
                consecutivePluginGpuFailuresAcrossParameters++;
                if (consecutivePluginGpuFailuresAcrossParameters >= MaxConsecutivePluginGpuFailuresAcrossParameters)
                    hasAbandonedGpuRendering = true;
                if (gpuFailureParameterVersion == snapshot.ParameterVersion)
                {
                    consecutivePluginGpuFailures++;
                }
                else
                {
                    gpuFailureParameterVersion = snapshot.ParameterVersion;
                    consecutivePluginGpuFailures = 1;
                }
            }
        }

        void ResetPluginGpuFailures()
        {
            lock (gpuBackendLock)
            {
                failedGpuSnapshot = null;
                gpuFailureParameterVersion = -1;
                consecutivePluginGpuFailures = 0;
                consecutivePluginGpuFailuresAcrossParameters = 0;
                hasAbandonedGpuRendering = false;
            }
        }

        void UpdateGpuSettingState()
        {
            var hasPendingChange = Volatile.Read(ref gpuSettingChangePending) != 0;
            var useGpuRendering = OpenFxSettings.Default.UseGpuRendering;
            if (!hasPendingChange && Volatile.Read(ref lastUseGpuRendering) == useGpuRendering)
                return;
            lock (gpuBackendLock)
            {
                useGpuRendering = OpenFxSettings.Default.UseGpuRendering;
                Volatile.Write(ref gpuSettingChangePending, 0);
                lastUseGpuRendering = useGpuRendering;
                ResetPluginGpuFailures();
                if (!useGpuRendering)
                {
                    if (gpuBackend is IOfxD3D11InteropBackend interop)
                        interop.ReleaseD3D11Resources();
                    return;
                }
                if (hasGpuBackendFailed && gpuBackend is not null)
                {
                    gpuBackend.ReleaseDeviceResources();
                    gpuBackend.Dispose();
                    Volatile.Write(ref gpuBackend, null);
                    hasGpuBackendFailed = false;
                }
                if (gpuBackend is null)
                    hasAttemptedGpuBackendCreation = false;
            }
        }

        void OnOpenFxSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(OpenFxSettings.UseGpuRendering))
                return;
            Volatile.Write(ref gpuSettingChangePending, 1);
        }

        void EnsureGpuBackend()
        {
            if (Volatile.Read(ref gpuBackend) is not null
                || gpuBackendFactory is null
                || Volatile.Read(ref hasAttemptedGpuBackendCreation)
                || !OpenFxSettings.Default.UseGpuRendering
                || !IsSupportedGpuRenderingDeclared(Props))
            {
                return;
            }
            lock (gpuBackendLock)
            {
                if (gpuBackend is not null || hasAttemptedGpuBackendCreation)
                    return;
                hasAttemptedGpuBackendCreation = true;
                var created = gpuBackendFactory();
                if (created is null)
                    return;
                if (IsGpuBackendSupported(created, Props))
                {
                    Volatile.Write(ref gpuBackend, created);
                    hasGpuBackendFailed = false;
                    return;
                }
                created.ReleaseDeviceResources();
                created.Dispose();
            }
        }

        void FallBackToCpu(
            int gpuStatus,
            GpuAttemptSnapshot snapshot,
            double time,
            OfxRectI renderWindow,
            (OfxClipInstance Clip, OfxImage Image)[] inputs,
            OfxImage outputImage,
            Exception? backendException = null)
        {
            if (backendException is null)
            {
                gpuBackend!.OnRenderFailed(gpuStatus);
                ReleaseGpuImages();
                RecordPluginGpuFailure(snapshot);
            }
            else
            {
                SynchronizeGpuBackendBestEffort();
                ReleaseGpuImages();
                gpuBackend!.OnBackendFailed();
                lock (gpuBackendLock)
                    hasGpuBackendFailed = true;
            }
            LogGpuFailureOnce(gpuStatus, backendException);
            if (!IsCpuRenderSupported())
                throw new InvalidOperationException($"GPUレンダリングが失敗し、プラグインはCPUレンダリング非対応です。plugin={plugin.Identifier} status={gpuStatus}", backendException);
            // GPU画像をCurrentImageから外した後、同じ時刻・同じ入力でCPU画像へ差し替えて再試行する。
            var cpuStatus = RunRenderSequenceIterations(time, renderWindow, inputs, outputImage, null);
            if (cpuStatus != OfxStatus.OK)
                throw new InvalidOperationException($"GPU失敗後のCPUフォールバックも失敗しました。plugin={plugin.Identifier} gpuStatus={gpuStatus} cpuStatus={cpuStatus}", backendException);
        }

        void SynchronizeGpuBackendBestEffort()
        {
            try
            {
                gpuBackend?.Synchronize();
            }
            catch (Exception e) when (IsBackendException(e))
            {
                // 元のbackend例外を維持する。同期できなくても画像解放とCPUフォールバックを続行する。
            }
        }

        static bool IsGpuBackendSupported(IOfxGpuRenderBackend? backend, OfxPropertySet props)
        {
            if (backend is not { IsAvailable: true })
                return false;
            var supportProperty = backend.Kind switch
            {
                OfxGpuRenderKind.OpenGL => OfxConstants.ImageEffectPropOpenGLRenderSupported,
                OfxGpuRenderKind.Cuda => OfxConstants.ImageEffectPropCudaRenderSupported,
                OfxGpuRenderKind.OpenCLBuffer => OfxConstants.ImageEffectPropOpenCLRenderSupported,
                OfxGpuRenderKind.OpenCLImage => OfxConstants.ImageEffectPropOpenCLSupported,
                _ => throw new InvalidOperationException($"未対応のGPU系統です。kind={backend.Kind}"),
            };
            var declaration = props.GetStringOrDefault(supportProperty, "false");
            return declaration.Equals("true", StringComparison.OrdinalIgnoreCase)
                || declaration.Equals("needed", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsSupportedGpuRenderingDeclared(OfxPropertySet props)
        {
            return IsDeclared(props, OfxConstants.ImageEffectPropCudaRenderSupported)
                || IsDeclared(props, OfxConstants.ImageEffectPropOpenCLRenderSupported);
        }

        static bool IsDeclared(OfxPropertySet props, string property)
        {
            var declaration = props.GetStringOrDefault(property, "false");
            return declaration.Equals("true", StringComparison.OrdinalIgnoreCase)
                || declaration.Equals("needed", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsBackendException(Exception exception)
            => exception is CudaException or CudaUnavailableException or OpenClException or OpenClUnavailableException;

        static int GetFallbackStatus(Exception exception)
            => exception switch
            {
                CudaException cuda => cuda.FallbackStatus,
                OpenClException openCl => openCl.FallbackStatus,
                _ => OfxStatus.GPURenderFailed,
            };

        bool IsCpuRenderSupported()
            => !Props.GetStringOrDefault(OfxConstants.ImageEffectPropCPURenderSupported, "true")
                .Equals("false", StringComparison.OrdinalIgnoreCase);

        static bool IsGpuFailureStatus(int status)
            => status is OfxStatus.GPURenderFailed or OfxStatus.GPUOutOfMemory;

        void PrepareCpuRender(double time, OfxRectI renderWindow)
        {
            var snapshot = CaptureGpuAttemptSnapshot(time, renderWindow);
            if (preparedDirectRenderSnapshot == snapshot)
            {
                preparedDirectRenderSnapshot = null;
                return;
            }
            preparedDirectRenderSnapshot = null;
            Create();
            NotifyChangedParams(time);
            // プール画像は内容がレンダリング毎に変わるため、画像の同一性を表す識別子も毎回更新する。
            renderSerial++;
        }

        GpuAttemptSnapshot CaptureGpuAttemptSnapshot(double time, OfxRectI renderWindow)
            => new(time, renderWindow.x1, renderWindow.y1, renderWindow.x2, renderWindow.y2, parameterVersion);

        void ValidateD3D11Resources(
            (OfxClipInstance Clip, nint Resource)[] inputs,
            nint outputResource,
            OfxRectI renderWindow)
        {
            ValidateNonEmptyRenderWindow(renderWindow);
            foreach (var (_, resource) in inputs)
                ValidateD3D11TextureSize(resource, Width, Height);
            ValidateD3D11TextureSize(
                outputResource,
                renderWindow.x2 - renderWindow.x1,
                renderWindow.y2 - renderWindow.y1);
        }

        static void ValidateNonEmptyRenderWindow(OfxRectI renderWindow)
        {
            if (renderWindow.x2 <= renderWindow.x1 || renderWindow.y2 <= renderWindow.y1)
                throw new ArgumentException("renderWindowが空です。");
        }

        static void ValidateD3D11TextureSize(nint resource, int expectedWidth, int expectedHeight)
        {
            if (resource == 0)
                throw new D3D11TextureValidationException("D3D11テクスチャがnullです。");
            Marshal.AddRef(resource);
            using var texture = new ID3D11Texture2D(resource);
            var description = texture.Description;
            if (description.Width != expectedWidth || description.Height != expectedHeight)
            {
                throw new D3D11TextureValidationException(
                    $"D3D11テクスチャのサイズが一致しません。expected={expectedWidth}x{expectedHeight} actual={description.Width}x{description.Height}");
            }
        }

        int CallInstanceAction(string action, nint inArgs, nint outArgs, bool? useGpuContext = null)
        {
            if (useGpuContext != false && IsGpuBackendSupported(gpuBackend, Props))
                return gpuBackend!.ExecuteWithContext(() => plugin.CallAction(action, Handle, inArgs, outArgs));
            return plugin.CallAction(action, Handle, inArgs, outArgs);
        }

        sealed class D3D11TextureValidationException(string message) : ArgumentException(message)
        {
        }

        readonly record struct GpuAttemptSnapshot(
            double Time,
            int X1,
            int Y1,
            int X2,
            int Y2,
            long ParameterVersion);

        void LogGpuFailureOnce(int status, Exception? backendException = null)
        {
            if (hasLoggedGpuFailure)
                return;
            hasLoggedGpuFailure = true;
            OfxHostLog.Info($"OpenFX GPUレンダリングが失敗したためCPUで再試行します。plugin={plugin.Identifier} backend={gpuBackend?.Kind} status={status} error={backendException?.Message}");
        }

        OfxImage PrepareGpuInputImage(string clipName, int width, int height, int offsetX, int offsetY)
        {
            if (!pooledGpuInputImages.TryGetValue(clipName, out var image)
                || image.Width != width
                || image.Height != height
                || image.OffsetX != offsetX
                || image.OffsetY != offsetY)
            {
                image?.Dispose();
                image = new OfxImage(
                    width,
                    height,
                    offsetX,
                    offsetY,
                    $"{plugin.Identifier}/{clipName}/GPU",
                    gpuBackend!.CreateImageStorage(width, height, offsetX, offsetY, false));
                pooledGpuInputImages[clipName] = image;
            }
            image.Props.SetString(OfxConstants.ImagePropUniqueIdentifier, $"{plugin.Identifier}/{clipName}/GPU#{renderSerial}");
            return image;
        }

        OfxImage PrepareGpuOutputImage(OfxRectI renderWindow)
        {
            var width = renderWindow.x2 - renderWindow.x1;
            var height = renderWindow.y2 - renderWindow.y1;
            if (pooledGpuOutputImage is null
                || pooledGpuOutputImage.Width != width
                || pooledGpuOutputImage.Height != height
                || pooledGpuOutputImage.OffsetX != renderWindow.x1
                || pooledGpuOutputImage.OffsetY != renderWindow.y1)
            {
                pooledGpuOutputImage?.Dispose();
                pooledGpuOutputImage = new OfxImage(
                    width,
                    height,
                    renderWindow.x1,
                    renderWindow.y1,
                    $"{plugin.Identifier}/Output/GPU",
                    gpuBackend!.CreateImageStorage(width, height, renderWindow.x1, renderWindow.y1, true));
            }
            pooledGpuOutputImage.Props.SetString(OfxConstants.ImagePropUniqueIdentifier, $"{plugin.Identifier}/Output/GPU#{renderSerial}");
            if (pooledGpuOutputImage.Props.GetStringOrDefault(OfxConstants.ImageEffectPropPreMultiplication, "") != outputPreMultiplication)
                pooledGpuOutputImage.Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, outputPreMultiplication);
            return pooledGpuOutputImage;
        }

        void ReleaseGpuImages()
        {
            foreach (var image in pooledGpuInputImages.Values)
                image.Dispose();
            pooledGpuInputImages.Clear();
            pooledGpuOutputImage?.Dispose();
            pooledGpuOutputImage = null;
        }

        /// <summary>
        /// 入力・出力クリップへ画像を差し込み、Begin/EndSequenceRenderで括って1回のrenderアクションを駆動する。
        /// renderのステータスは呼び出し元がGPUフォールバック判定に使うため、そのまま返す。
        /// </summary>
        int RunSingleRenderSequence(
            double time,
            OfxRectI renderWindow,
            (OfxClipInstance Clip, OfxImage Image)[] inputs,
            OfxImage outputImage,
            IOfxGpuRenderBackend? activeGpuBackend)
        {
            CurrentTime = time;
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
                using var sequenceArgs = CreateSequenceRenderArgs(time, activeGpuBackend);
                var beginStatus = ExecuteAction(
                    activeGpuBackend,
                    OfxGpuRenderAction.BeginSequenceRender,
                    sequenceArgs,
                    () => plugin.CallAction(OfxConstants.ImageEffectActionBeginSequenceRender, Handle, sequenceArgs.Handle, 0));
                if (activeGpuBackend is not null && IsGpuFailureStatus(beginStatus))
                    return beginStatus;
                if (beginStatus is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                    throw new InvalidOperationException($"kOfxImageEffectActionBeginSequenceRender が失敗しました。plugin={plugin.Identifier} status={beginStatus}");

                var renderStatus = OfxStatus.Failed;
                var endStatus = OfxStatus.OK;
                try
                {
                    using var renderArgs = CreateRenderArgs(time, renderWindow, activeGpuBackend);
                    renderStatus = ExecuteRenderAction(activeGpuBackend, renderArgs);
                    // renderは必須実装のため kOfxStatReplyDefault（未処理）も失敗として扱う
                    // （成功扱いすると未描画のプール出力バッファがそのまま表示される）
                }
                finally
                {
                    // renderが失敗してもBegin/Endの対応を崩さない（シーケンス状態を持つプラグインが復帰不能になるため）
                    endStatus = ExecuteAction(
                        activeGpuBackend,
                        OfxGpuRenderAction.EndSequenceRender,
                        sequenceArgs,
                        () => plugin.CallAction(OfxConstants.ImageEffectActionEndSequenceRender, Handle, sequenceArgs.Handle, 0));
                    if (endStatus is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                        OfxHostLog.Info($"kOfxImageEffectActionEndSequenceRender が失敗しました。plugin={plugin.Identifier} status={endStatus}");
                }
                if (activeGpuBackend is not null
                    && renderStatus == OfxStatus.OK
                    && IsGpuFailureStatus(endStatus))
                {
                    return endStatus;
                }
                return renderStatus;
            }
            finally
            {
                foreach (var (clip, _) in inputs)
                    clip.CurrentImage = null;
                outputClip.CurrentImage = null;
            }
        }

        int RunRenderSequenceIterations(
            double time,
            OfxRectI renderWindow,
            (OfxClipInstance Clip, OfxImage Image)[] inputs,
            OfxImage outputImage,
            IOfxGpuRenderBackend? activeGpuBackend)
        {
#if DEBUG
            var iterations = Math.Max(1, RenderIterationsForTest);
#else
            const int iterations = 1;
#endif
            var status = OfxStatus.OK;
            for (var i = 0; i < iterations; i++)
            {
                status = RunSingleRenderSequence(time, renderWindow, inputs, outputImage, activeGpuBackend);
                if (status != OfxStatus.OK)
                    break;
            }
            return status;
        }

        static int ExecuteAction(
            IOfxGpuRenderBackend? backend,
            OfxGpuRenderAction action,
            OfxPropertySet inArgs,
            Func<int> actionBody)
            => backend is null ? actionBody() : backend.ExecuteAction(action, inArgs, actionBody);

        int ExecuteRenderAction(IOfxGpuRenderBackend? backend, OfxPropertySet renderArgs)
        {
#if DEBUG
            if (backend is null && CpuRenderStatusForTest is { } status)
                return status;
#endif
            return ExecuteAction(
                backend,
                OfxGpuRenderAction.Render,
                renderArgs,
                () => plugin.CallAction(OfxConstants.ImageEffectActionRender, Handle, renderArgs.Handle, 0));
        }

        OfxPropertySet CreateRenderArgs(double time, OfxRectI renderWindow, IOfxGpuRenderBackend? activeGpuBackend)
        {
            var args = new OfxPropertySet { DebugName = "render.inArgs" };
            args.SetDouble(OfxConstants.PropTime, time);
            args.SetString(OfxConstants.ImageEffectPropFieldToRender, OfxConstants.ImageFieldNone);
            args.SetIntN(OfxConstants.ImageEffectPropRenderWindow, renderWindow.x1, renderWindow.y1, renderWindow.x2, renderWindow.y2);
            args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            args.SetInt(OfxConstants.ImageEffectPropSequentialRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropInteractiveRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropRenderQualityDraft, 0);
            SetGpuRenderArgs(args, activeGpuBackend);
            return args;
        }

        OfxPropertySet CreateSequenceRenderArgs(double time, IOfxGpuRenderBackend? activeGpuBackend)
        {
            var args = new OfxPropertySet { DebugName = "sequenceRender.inArgs" };
            args.SetDoubleN(OfxConstants.ImageEffectPropFrameRange, time, time);
            args.SetDouble(OfxConstants.ImageEffectPropFrameStep, 1);
            args.SetInt(OfxConstants.PropIsInteractive, 0);
            args.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            args.SetInt(OfxConstants.ImageEffectPropSequentialRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropInteractiveRenderStatus, 0);
            args.SetInt(OfxConstants.ImageEffectPropRenderQualityDraft, 0);
            SetGpuRenderArgs(args, activeGpuBackend);
            return args;
        }

        void SetGpuRenderArgs(OfxPropertySet args, IOfxGpuRenderBackend? activeGpuBackend)
        {
            args.SetInt(OfxConstants.ImageEffectPropOpenGLEnabled, activeGpuBackend?.Kind == OfxGpuRenderKind.OpenGL ? 1 : 0);
            args.SetInt(OfxConstants.ImageEffectPropCudaEnabled, activeGpuBackend?.Kind == OfxGpuRenderKind.Cuda ? 1 : 0);
            args.SetInt(
                OfxConstants.ImageEffectPropOpenCLEnabled,
                activeGpuBackend?.Kind is OfxGpuRenderKind.OpenCLBuffer or OfxGpuRenderKind.OpenCLImage ? 1 : 0);
            if (activeGpuBackend?.CommandQueue is { } commandQueue && commandQueue != 0)
            {
                if (activeGpuBackend.Kind == OfxGpuRenderKind.Cuda
                    && Props.GetStringOrDefault(OfxConstants.ImageEffectPropCudaStreamSupported, "false")
                        .Equals("true", StringComparison.OrdinalIgnoreCase))
                    args.SetPointer(OfxConstants.ImageEffectPropCudaStream, commandQueue);
                else if (activeGpuBackend.Kind is OfxGpuRenderKind.OpenCLBuffer or OfxGpuRenderKind.OpenCLImage)
                    args.SetPointer(OfxConstants.ImageEffectPropOpenCLCommandQueue, commandQueue);
            }
        }

        public override void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            OpenFxSettings.Default.PropertyChanged -= OnOpenFxSettingsPropertyChanged;
            // destroyInstanceで画像ポインタへ触るプラグインに備え、画像の解放はアクションの後に行う
            if (isCreated)
            {
                isCreated = false;
                try
                {
                    var status = CallInstanceAction(OfxConstants.ActionDestroyInstance, 0, 0, createInstanceUsedGpuContext);
                    if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                        OfxHostLog.Info($"kOfxActionDestroyInstance が失敗しました。plugin={plugin.Identifier} status={status}");
                }
                catch (Exception e)
                {
                    // CUDAコンテキスト喪失後はdestroyInstance自体が失敗しうる。
                    // 画像・クリップ・パラメーター・バックエンドの解放は中断しない。
                    OfxHostLog.Info($"kOfxActionDestroyInstance の実行中に例外が発生しました。plugin={plugin.Identifier} error={e.Message}");
                }
            }
            foreach (var image in pooledInputImages.Values)
                image.Dispose();
            pooledInputImages.Clear();
            pooledOutputImage?.Dispose();
            pooledOutputImage = null;
            ReleaseGpuImages();
            if (gpuBackend is not null)
            {
                // 通常破棄ではstream・GPU画像・D3D11登録cacheなどインスタンス固有資源だけを解放する。
                // device単位のprimary context・変換module・scratchはTDR相当の無効化かプロセス終了まで常駐する。
                gpuBackend.ReleaseDeviceResources();
                gpuBackend.Dispose();
            }
            foreach (var clip in clips)
                clip.Dispose();
            clips.Clear();
            ParamSet.Dispose();
            Props.Dispose();
            base.Dispose();
        }
    }
}
