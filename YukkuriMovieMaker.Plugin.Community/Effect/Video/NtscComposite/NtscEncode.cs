using System;
using System.Collections.Generic;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジットシミュレーション パス2: エンコード。
    /// ラスター空間のRGBA画像をYIQへ変換し、送信側帯域制限FIRを適用した上で
    /// コンポジット信号(R)+アルファ(G)のfloat16テクスチャを出力する。
    /// FIR係数はC#側(NtscSignal)でカイザー窓設計し、定数バッファで渡す。
    /// </summary>
    sealed class NtscEncode(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>フレーム番号(位相交番・ドットクロール用)</summary>
        public float Frame
        {
            set => SetValue((int)EffectImpl.Properties.Frame, value);
        }
        /// <summary>セットアップレベル(NTSC-M: 0.075 / NTSC-J: 0)</summary>
        public float Setup
        {
            set => SetValue((int)EffectImpl.Properties.Setup, value);
        }
        /// <summary>にじみ強度(0～2、1=標準)。I/Q帯域の逆スケール</summary>
        public float Bleed
        {
            set => SetValue((int)EffectImpl.Properties.Bleed, value);
        }
        /// <summary>シャープネス(0～2、1=標準)。Y帯域のスケール</summary>
        public float Sharpness
        {
            set => SetValue((int)EffectImpl.Properties.Sharpness, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>, ID2D1DrawTransform
        {
            float frame;
            float setup;
            float bleed = 1f;
            float sharpness = 1f;

            //送信側帯域制限FIR(片側)。bleed/sharpness変更時のみ再設計する
            bool kernelsDirty = true;
            double[] yPre = [];
            double[] iPre = [];
            double[] qPre = [];

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Frame)]
            public float Frame
            {
                get => frame;
                set
                {
                    frame = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Setup)]
            public float Setup
            {
                get => setup;
                set
                {
                    setup = Math.Clamp(value, 0f, 0.5f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Bleed)]
            public float Bleed
            {
                get => bleed;
                set
                {
                    var clamped = Math.Clamp(value, 0f, 4f);
                    if (bleed != clamped)
                        kernelsDirty = true;
                    bleed = clamped;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Sharpness)]
            public float Sharpness
            {
                get => sharpness;
                set
                {
                    var clamped = Math.Clamp(value, 0f, 4f);
                    if (sharpness != clamped)
                        kernelsDirty = true;
                    sharpness = clamped;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("NtscEncode")) { }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                if (kernelsDirty)
                {
                    //送信側の帯域制限。VHSの劣化はデコード側で加えるため、ここでは放送仕様のみ
                    yPre = NtscSignal.DesignKaiserLowPass(
                        NtscSignal.GetLumaCutoffHz(sharpness, isVhs: false, vhsDegradation: 0),
                        NtscSignal.EncodeLumaHalfTaps, NtscSignal.DefaultKaiserBeta);
                    iPre = NtscSignal.DesignKaiserLowPass(
                        NtscSignal.GetChromaCutoffHz(NtscSignal.IBandwidthHz, bleed, isVhs: false, vhsDegradation: 0),
                        NtscSignal.EncodeIHalfTaps, NtscSignal.DefaultKaiserBeta);
                    qPre = NtscSignal.DesignKaiserLowPass(
                        NtscSignal.GetChromaCutoffHz(NtscSignal.QBandwidthHz, bleed, isVhs: false, vhsDegradation: 0),
                        NtscSignal.EncodeQHalfTaps, NtscSignal.DefaultKaiserBeta);
                    kernelsDirty = false;
                }

                //HLSL側cbufferのレジスタ配置と一致させる(NtscEncode.hlsl参照)
                var buffer = new List<float>(4 * (2 + 7 + 13 + 25))
                {
                    //c0: inputRect
                    inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom,
                    //c1: rasterW, rasterH, frame, setup
                    inputRect.Right - inputRect.Left, inputRect.Bottom - inputRect.Top, frame, setup,
                };
                NtscSignal.AppendTapsAsRegisters(buffer, yPre, NtscSignal.EncodeLumaHalfTaps + 1);
                NtscSignal.AppendTapsAsRegisters(buffer, iPre, NtscSignal.EncodeIHalfTaps + 1);
                NtscSignal.AppendTapsAsRegisters(buffer, qPre, NtscSignal.EncodeQHalfTaps + 1);
                drawInformation.SetPixelShaderConstantBuffer(buffer.ToArray());
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                //コンポジット信号は[0,1]範囲外の値を取るため中間バッファをfloat16にする
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];
                UpdateConstants();
                outputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //送信側FIRの最大タップ幅(Q: 半幅24)ぶん水平に膨張させる
                inputRects[0] = new RawRect(
                    outputRect.Left - NtscSignal.EncodeQHalfTaps,
                    outputRect.Top,
                    outputRect.Right + NtscSignal.EncodeQHalfTaps,
                    outputRect.Bottom);
            }

            /// <summary>入力の無効領域はFIRタップ幅ぶん水平に広がって出力へ影響する</summary>
            public new RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => new(
                    invalidInputRect.Left - NtscSignal.EncodeQHalfTaps,
                    invalidInputRect.Top,
                    invalidInputRect.Right + NtscSignal.EncodeQHalfTaps,
                    invalidInputRect.Bottom);

            public enum Properties : int
            {
                Frame = 0,
                Setup = 1,
                Bleed = 2,
                Sharpness = 3,
            }
        }
    }
}
