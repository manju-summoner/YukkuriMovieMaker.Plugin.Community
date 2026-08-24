using System;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// クリップのインスタンス（OfxImageClipHandle の実体）。
    /// レンダリング時にホストが <see cref="CurrentImage"/> へ画像を差し込み、
    /// プラグインは clipGetImage 経由で取得する。
    /// </summary>
    internal sealed class OfxClipInstance : OfxObject
    {
        public string Name { get; }
        public OfxPropertySet Props { get; }

        /// <summary>現在のフレームの画像（レンダリング中のみ非null。所有はホスト側）</summary>
        public OfxImage? CurrentImage { get; set; }

        /// <summary>現在レンダリング中のフレーム時刻（clipGetImageのテンポラルアクセス検出用）</summary>
        public double CurrentTime { get; set; }

        /// <summary>クリップの定義域（プロジェクトサイズ）</summary>
        public int Width { get; }
        public int Height { get; }

        public OfxClipInstance(OfxClipDescriptor descriptor, string context, int width, int height, double frameRate, double durationFrames)
        {
            Name = descriptor.Name;
            Width = width;
            Height = height;
            Props = new OfxPropertySet { DebugName = $"clipInstance({Name})" };
            Props.CopyFrom(descriptor.Props);

            // インスタンス固有のプロパティ（本ホストは RGBA float・premultiplied 固定）
            Props.SetString(OfxConstants.ImageEffectPropPixelDepth, OfxConstants.BitDepthFloat);
            Props.SetString(OfxConstants.ImageEffectPropComponents, OfxConstants.ImageComponentRGBA);
            Props.SetString(OfxConstants.ImageClipPropUnmappedPixelDepth, OfxConstants.BitDepthFloat);
            Props.SetString(OfxConstants.ImageClipPropUnmappedComponents, OfxConstants.ImageComponentRGBA);
            Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, OfxConstants.ImagePreMultiplied);
            Props.SetDouble(OfxConstants.ImagePropPixelAspectRatio, 1);
            Props.SetDouble(OfxConstants.ImageEffectPropFrameRate, frameRate);
            Props.SetDoubleN(OfxConstants.ImageEffectPropFrameRange, 0, Math.Max(0, durationFrames - 1));
            Props.SetDouble(OfxConstants.ImageEffectPropUnmappedFrameRate, frameRate);
            Props.SetDoubleN(OfxConstants.ImageEffectPropUnmappedFrameRange, 0, Math.Max(0, durationFrames - 1));
            Props.SetInt(OfxConstants.ImageClipPropContinuousSamples, 0);
            Props.SetString(OfxConstants.ImageClipPropFieldOrder, OfxConstants.ImageFieldNone);
            // ホストが画像を供給するクリップ（コンテキストの必須入力とOutput）のみ接続済みと申告する。
            // それ以外（オプションのMask等）を接続済みと申告すると、プラグインが取得不能なクリップ画像を
            // 前提に動いてしまう（例: openfx-miscのマスク合成が「マスク量0」となり出力が素通しになる）。
            // 予約名と同名のオプションクリップを別コンテキストで定義するプラグインを誤申告しないよう、
            // 名前だけでなく実行中のコンテキストで判定する
            var isConnected = Name == OfxConstants.ImageEffectOutputClipName
                || context switch
                {
                    // ジェネレーターは入力なし（Sourceをオプション定義するプラグインがあっても供給しない）
                    OfxConstants.ImageEffectContextGenerator => false,
                    OfxConstants.ImageEffectContextTransition => Name is OfxConstants.ImageEffectTransitionSourceFromClipName or OfxConstants.ImageEffectTransitionSourceToClipName,
                    _ => Name == OfxConstants.ImageEffectSimpleSourceClipName,
                };
            Props.SetInt(OfxConstants.ImageClipPropConnected, isConnected ? 1 : 0);
            // インスタンス生成完了時点の値を propReset の復元先にする（CopyFrom後の再スナップショット）
            Props.SealDefaults();
        }

        public override void Dispose()
        {
            Props.Dispose();
            base.Dispose();
        }
    }
}
