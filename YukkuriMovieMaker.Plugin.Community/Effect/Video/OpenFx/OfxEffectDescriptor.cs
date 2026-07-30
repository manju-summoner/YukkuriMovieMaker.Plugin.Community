using System;
using System.Collections.Generic;
using System.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// クリップのディスクリプタ（clipDefine で定義される入出力スロット）。
    /// describe時点ではクリップハンドルは存在せず、プロパティセットのみを持つ。
    /// </summary>
    internal sealed class OfxClipDescriptor : IDisposable
    {
        public string Name { get; }
        public OfxPropertySet Props { get; }

        public OfxClipDescriptor(string name)
        {
            Name = name;
            Props = new OfxPropertySet { DebugName = $"clip({name})" };
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeClip);
            Props.SetString(OfxConstants.PropName, name);
            Props.SetString(OfxConstants.PropLabel, name);
            Props.SetString(OfxConstants.PropShortLabel, name);
            Props.SetString(OfxConstants.PropLongLabel, name);
            Props.SetEmpty(OfxConstants.ImageEffectPropSupportedComponents, OfxPropertyType.String);
            Props.SetInt(OfxConstants.ImageEffectPropTemporalClipAccess, 0);
            Props.SetInt(OfxConstants.ImageClipPropOptional, 0);
            Props.SetString(OfxConstants.ImageClipPropFieldExtraction, OfxConstants.ImageFieldDoubled);
            Props.SetInt(OfxConstants.ImageClipPropIsMask, 0);
            Props.SetInt(OfxConstants.ImageEffectPropSupportsTiles, 1);
            Props.SealDefaults();
        }

        public void Dispose()
        {
            Props.Dispose();
        }
    }

    /// <summary>
    /// 画像エフェクトのディスクリプタ（describe / describeInContext の対象となる OfxImageEffectHandle の実体）。
    /// </summary>
    internal sealed class OfxEffectDescriptor : OfxObject, IOfxImageEffectObject
    {
        readonly List<OfxClipDescriptor> clips = [];

        public OfxPropertySet Props { get; }
        public OfxParamSet ParamSet { get; } = new();

        /// <summary>describeInContext 対象のコンテキスト。グローバルディスクリプタでは null</summary>
        public string? Context { get; }

        public IReadOnlyList<OfxClipDescriptor> Clips => clips;

        /// <summary>グローバル（describe用）ディスクリプタを作る</summary>
        public OfxEffectDescriptor(string binaryPath, string pluginIdentifier)
        {
            Props = new OfxPropertySet { DebugName = $"effectDescriptor({pluginIdentifier})" };
            FillDefaultProperties(binaryPath);
            Props.SealDefaults();
        }

        /// <summary>
        /// kOfxPluginPropFilePath 用のパスを得る。仕様上このプロパティはバンドルの場所を指すため、
        /// バンドル形式（(名前).ofx.bundle\Contents\Win64\(名前).ofx）ならバンドルルートを返す
        /// （プラグインはこれを基準に Contents\Resources 等の同梱リソースを探索する）。
        /// バンドル外の単体 .ofx はバイナリ自身のパスを返す
        /// </summary>
        internal static string ResolveBundlePath(string binaryPath)
        {
            var win64 = System.IO.Path.GetDirectoryName(binaryPath);
            var contents = win64 is null ? null : System.IO.Path.GetDirectoryName(win64);
            var bundle = contents is null ? null : System.IO.Path.GetDirectoryName(contents);
            if (bundle is not null
                && bundle.EndsWith(".ofx.bundle", StringComparison.OrdinalIgnoreCase)
                && string.Equals(System.IO.Path.GetFileName(win64), "Win64", StringComparison.OrdinalIgnoreCase)
                && string.Equals(System.IO.Path.GetFileName(contents), "Contents", StringComparison.OrdinalIgnoreCase))
                return bundle;
            return binaryPath;
        }

        /// <summary>describeInContext 用に、グローバルディスクリプタのプロパティを引き継いだ派生ディスクリプタを作る</summary>
        public OfxEffectDescriptor(OfxEffectDescriptor global, string context)
        {
            Context = context;
            Props = new OfxPropertySet { DebugName = $"{global.Props.DebugName}:{context}" };
            Props.CopyFrom(global.Props);
            Props.SetString(OfxConstants.ImageEffectPropContext, context);
            Props.SealDefaults();
        }

        void FillDefaultProperties(string binaryPath)
        {
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeImageEffect);
            Props.SetString(OfxConstants.PropLabel, "");
            Props.SetString(OfxConstants.PropShortLabel, "");
            Props.SetString(OfxConstants.PropLongLabel, "");
            Props.SetString(OfxConstants.PropPluginDescription, "");
            Props.SetIntN(OfxConstants.PropVersion, 0);
            Props.SetString(OfxConstants.PropVersionLabel, "");
            Props.SetStringN(OfxConstants.PropIcon, "", "");
            Props.SetEmpty(OfxConstants.ImageEffectPropSupportedContexts, OfxPropertyType.String);
            Props.SetEmpty(OfxConstants.ImageEffectPropSupportedPixelDepths, OfxPropertyType.String);
            Props.SetString(OfxConstants.ImageEffectPluginPropGrouping, "");
            Props.SetInt(OfxConstants.ImageEffectPluginPropSingleInstance, 0);
            Props.SetString(OfxConstants.ImageEffectPluginRenderThreadSafety, OfxConstants.ImageEffectRenderFullySafe);
            Props.SetInt(OfxConstants.ImageEffectPluginPropHostFrameThreading, 0);
            Props.SetInt(OfxConstants.ImageEffectPropSupportsMultiResolution, 1);
            Props.SetInt(OfxConstants.ImageEffectPropSupportsTiles, 1);
            Props.SetInt(OfxConstants.ImageEffectPropTemporalClipAccess, 0);
            Props.SetInt(OfxConstants.ImageEffectPluginPropFieldRenderTwiceAlways, 1);
            Props.SetInt(OfxConstants.ImageEffectPropSupportsMultipleClipDepths, 0);
            Props.SetInt(OfxConstants.ImageEffectPropSupportsMultipleClipPARs, 0);
            Props.SetEmpty(OfxConstants.ImageEffectPropClipPreferencesSlaveParam, OfxPropertyType.String);
            Props.SetPointer(OfxConstants.ImageEffectPluginPropOverlayInteractV1, 0);
            // GPUレンダリング能力はプラグインがdescribeで上書きする。
            // ここではofxGPURender.hが定めるプラグインdescriptorの既定値だけを宣言する
            Props.SetString(OfxConstants.ImageEffectPropOpenGLRenderSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropCudaRenderSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropCudaStreamSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropOpenCLRenderSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropOpenCLSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropMetalRenderSupported, "false");
            Props.SetString(OfxConstants.ImageEffectPropCPURenderSupported, "true");
            Props.SetString(OfxConstants.PluginPropFilePath, ResolveBundlePath(binaryPath));
        }

        public OfxClipDescriptor DefineClip(string name)
        {
            var existing = FindClip(name);
            if (existing is not null)
                return existing;
            var clip = new OfxClipDescriptor(name);
            clips.Add(clip);
            return clip;
        }

        public OfxClipDescriptor? FindClip(string name) => clips.FirstOrDefault(c => c.Name == name);

        // describe結果を読み取るための補助プロパティ
        public string Label => Props.GetStringOrDefault(OfxConstants.PropLabel, "");
        public string Grouping => Props.GetStringOrDefault(OfxConstants.ImageEffectPluginPropGrouping, "");
        public string Description => Props.GetStringOrDefault(OfxConstants.PropPluginDescription, "");
        public string[] SupportedContexts => Props.GetStrings(OfxConstants.ImageEffectPropSupportedContexts);
        public string[] SupportedPixelDepths => Props.GetStrings(OfxConstants.ImageEffectPropSupportedPixelDepths);

        public override void Dispose()
        {
            foreach (var clip in clips)
                clip.Dispose();
            clips.Clear();
            ParamSet.Dispose();
            Props.Dispose();
            base.Dispose();
        }
    }
}
