using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXホスト記述子。YMM4がホストとして持つ能力をプロパティセットで宣言し、
    /// プラグインへ渡す OfxHost 構造体（fetchSuite 含む）を提供する。
    /// プロセス内で1つだけ生成され、プロセス終了まで生存する。
    /// </summary>
    internal static unsafe class OfxHostDescriptor
    {
        static readonly object sync = new();
        static OfxPropertySet? hostProps;
        static nint hostStructPointer;

        /// <summary>プラグインの setHost へ渡す OfxHost* （プロセス生存中は有効）</summary>
        public static nint HostStructPointer
        {
            get
            {
                lock (sync)
                {
                    if (hostStructPointer == 0)
                    {
                        hostProps = CreateHostProperties();
                        var host = (OfxHostNative*)NativeMemory.AllocZeroed((nuint)sizeof(OfxHostNative));
                        host->host = hostProps.Handle;
                        host->fetchSuite = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint>)&FetchSuite;
                        hostStructPointer = (nint)host;
                    }
                    return hostStructPointer;
                }
            }
        }

        static OfxPropertySet CreateHostProperties()
        {
            var props = new OfxPropertySet { DebugName = "host" };
            props.SetString(OfxConstants.PropType, OfxConstants.TypeImageEffectHost);
            props.SetString(OfxConstants.PropName, "net.manjubox.YukkuriMovieMaker4");
            props.SetString(OfxConstants.PropLabel, "YukkuriMovieMaker4");
            var version = typeof(OfxHostDescriptor).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            props.SetIntN(OfxConstants.PropVersion, version.Major, version.Minor, version.Build, version.Revision);
            props.SetString(OfxConstants.PropVersionLabel, version.ToString());
            // 実装済みのOFX APIバージョン（1.4相当。1.5固有のDrawSuite等は未実装）。
            // kOfxPropAPIVersion の型は int×N（例: 1.4 → {1,4}）
            props.SetIntN(OfxConstants.PropAPIVersion, 1, 4);

            // 画像エフェクトホストとしての能力
            props.SetInt(OfxConstants.ImageEffectHostPropIsBackground, 0);
            props.SetInt(OfxConstants.ImageEffectPropSupportsOverlays, 0);
            // multi-resolution = 入出力画像が原点からオフセットしうる、の意。
            // RoD拡張時に出力画像へオフセット付きboundsを渡すため1を宣言する
            props.SetInt(OfxConstants.ImageEffectPropSupportsMultiResolution, 1);
            props.SetInt(OfxConstants.ImageEffectPropSupportsTiles, 0);
            props.SetInt(OfxConstants.ImageEffectPropTemporalClipAccess, 0);
            props.SetStringN(OfxConstants.ImageEffectPropSupportedContexts, OfxConstants.ImageEffectContextFilter, OfxConstants.ImageEffectContextTransition, OfxConstants.ImageEffectContextGenerator);
            props.SetStringN(OfxConstants.ImageEffectPropSupportedComponents, OfxConstants.ImageComponentRGBA);
            props.SetStringN(OfxConstants.ImageEffectPropSupportedPixelDepths, OfxConstants.BitDepthFloat);
            props.SetInt(OfxConstants.ImageEffectPropSupportsMultipleClipDepths, 0);
            props.SetInt(OfxConstants.ImageEffectPropSupportsMultipleClipPARs, 0);
            props.SetInt(OfxConstants.ImageEffectPropSetableFrameRate, 0);
            props.SetInt(OfxConstants.ImageEffectPropSetableFielding, 0);
            props.SetInt(OfxConstants.ImageEffectPropRenderQualityDraft, 0);
            props.SetInt(OfxConstants.ImageEffectInstancePropSequentialRender, 0);
            props.SetPointer(OfxConstants.PropHostOSHandle, 0);
            // OFXの標準座標系（Y軸上向き・左下原点）をそのまま宣言する。D2DとのY軸変換はホスト側で行う
            props.SetString(OfxConstants.ImageEffectHostPropNativeOrigin, OfxConstants.HostNativeOriginBottomLeft);

            // パラメータホストとしての能力
            props.SetInt(OfxConstants.ParamHostPropSupportsCustomAnimation, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsStringAnimation, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsBooleanAnimation, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsChoiceAnimation, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsStrChoice, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsStrChoiceAnimation, 0);
            props.SetInt(OfxConstants.ParamHostPropSupportsCustomInteract, 0);
            props.SetInt(OfxConstants.ParamHostPropMaxParameters, -1);
            props.SetInt(OfxConstants.ParamHostPropMaxPages, 0);
            props.SetIntN(OfxConstants.ParamHostPropPageRowColumnCount, 0, 0);
            // ホストプロパティは全プラグインで共有・使い回されるため、propReset で消えた値が永続しないよう既定値を確定しておく
            props.SealDefaults();
            return props;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static nint FetchSuite(nint host, byte* suiteName, int suiteVersion)
        {
            try
            {
                var name = Marshal.PtrToStringUTF8((nint)suiteName);
                switch (name)
                {
                    case OfxConstants.PropertySuite when suiteVersion == 1:
                        return OfxPropertySuite.Pointer;
                    case OfxConstants.ImageEffectSuite when suiteVersion == 1:
                        return OfxImageEffectSuite.Pointer;
                    case OfxConstants.ParameterSuite when suiteVersion == 1:
                        return OfxParameterSuite.Pointer;
                    case OfxConstants.MemorySuite when suiteVersion == 1:
                        return OfxMemorySuite.Pointer;
                    case OfxConstants.MultiThreadSuite when suiteVersion == 1:
                        return OfxMultiThreadSuite.Pointer;
                    case OfxConstants.MessageSuite when suiteVersion == 1:
                        return OfxMessageSuite.PointerV1;
                    case OfxConstants.MessageSuite when suiteVersion == 2:
                        return OfxMessageSuite.PointerV2;
                    default:
                        OfxHostLog.Debug($"未対応のスイート要求: {name} v{suiteVersion}");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"fetchSuite で例外: {ex}");
                return 0;
            }
        }
    }
}
