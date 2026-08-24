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
    /// NTSCコンポジットシミュレーション パス3: デコード。
    /// コンポジット信号をノッチ/コムでY/C分離し、fscの同期検波とLPFでI/Qを復調して
    /// RGBA(乗算済みアルファ)へ復元する。ノイズ・VHS劣化もこのパスで付加する。
    /// FIR係数はC#側(NtscSignal)でカイザー窓設計し、定数バッファで渡す。
    /// </summary>
    sealed class NtscDecode(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>
        /// VHSのトラッキング揺れ・ヘッドスイッチングによる水平ずれの上限(サンプル)。
        /// NtscDecode.hlsl の MAX_TRACK_SHIFT と一致させること。
        /// </summary>
        public const int MaxTrackingShift = 40;

        /// <summary>フレーム番号(位相交番・ドットクロール・ノイズシード用)</summary>
        public float Frame
        {
            set => SetValue((int)EffectImpl.Properties.Frame, value);
        }
        /// <summary>セットアップレベル(NTSC-M: 0.075 / NTSC-J: 0)。エンコード側と同じ値を渡す</summary>
        public float Setup
        {
            set => SetValue((int)EffectImpl.Properties.Setup, value);
        }
        /// <summary>にじみ強度(0～2、1=標準)</summary>
        public float Bleed
        {
            set => SetValue((int)EffectImpl.Properties.Bleed, value);
        }
        /// <summary>シャープネス(0～2、1=標準)</summary>
        public float Sharpness
        {
            set => SetValue((int)EffectImpl.Properties.Sharpness, value);
        }
        /// <summary>信号ノイズ量(0～1)</summary>
        public float Noise
        {
            set => SetValue((int)EffectImpl.Properties.Noise, value);
        }
        /// <summary>Y/C分離方式(0=ノッチ, 1=コム)</summary>
        public float CombMode
        {
            set => SetValue((int)EffectImpl.Properties.CombMode, value);
        }
        /// <summary>VHSモード(0=OFF, 1=ON)</summary>
        public float VhsMode
        {
            set => SetValue((int)EffectImpl.Properties.VhsMode, value);
        }
        /// <summary>VHSテープ劣化(帯域・にじみ・リンギング)(0～1)</summary>
        public float VhsTapeDegradation
        {
            set => SetValue((int)EffectImpl.Properties.VhsTapeDegradation, value);
        }
        /// <summary>VHSトラッキング揺れ(横揺れ・ヘッドスイッチング)(0～1)</summary>
        public float VhsTracking
        {
            set => SetValue((int)EffectImpl.Properties.VhsTracking, value);
        }
        /// <summary>VHS常時ノイズ量(0～1)</summary>
        public float VhsNoise
        {
            set => SetValue((int)EffectImpl.Properties.VhsNoise, value);
        }
        /// <summary>ドロップアウト頻度(0～1)</summary>
        public float VhsDropout
        {
            set => SetValue((int)EffectImpl.Properties.VhsDropout, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>, ID2D1DrawTransform
        {
            //水平方向の矩形膨張量。復調FIRの最大タップ幅 + VHSの水平ずれ上限
            const int ExpandX = NtscSignal.DecodeChromaHalfTaps + MaxTrackingShift;
            //垂直方向の矩形膨張量。コムフィルタが前後1ラインを参照する
            //(モードで膨張量を変えると切替時に無効化漏れが起きるため常に膨張させる)
            const int ExpandY = 1;

            float frame;
            float setup;
            float bleed = 1f;
            float sharpness = 1f;
            float noise;
            float combMode;
            float vhsMode;
            float vhsTapeDegradation = 0.5f;
            float vhsTracking = 0.5f;
            float vhsNoise = 0.5f;
            float vhsDropout = 0.5f;

            //復調FIR(片側)。パラメータ変更時のみ再設計する
            bool kernelsDirty = true;
            double[] yTaps = [];
            double[] iTaps = [];
            double[] qTaps = [];

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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Noise)]
            public float Noise
            {
                get => noise;
                set
                {
                    noise = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.CombMode)]
            public float CombMode
            {
                get => combMode;
                set
                {
                    var clamped = Math.Clamp(value, 0f, 1f);
                    if (combMode != clamped)
                        kernelsDirty = true; //ノッチの有無でYカーネルが変わる
                    combMode = clamped;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.VhsMode)]
            public float VhsMode
            {
                get => vhsMode;
                set
                {
                    var clamped = Math.Clamp(value, 0f, 1f);
                    if (vhsMode != clamped)
                        kernelsDirty = true;
                    vhsMode = clamped;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.VhsTapeDegradation)]
            public float VhsTapeDegradation
            {
                get => vhsTapeDegradation;
                set
                {
                    var clamped = Math.Clamp(value, 0f, 1f);
                    if (vhsTapeDegradation != clamped && vhsMode > 0.5f)
                        kernelsDirty = true; //帯域・リンギングはカーネル設計に効くため再設計が要る
                    vhsTapeDegradation = clamped;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.VhsTracking)]
            public float VhsTracking
            {
                get => vhsTracking;
                set
                {
                    vhsTracking = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.VhsNoise)]
            public float VhsNoise
            {
                get => vhsNoise;
                set
                {
                    vhsNoise = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.VhsDropout)]
            public float VhsDropout
            {
                get => vhsDropout;
                set
                {
                    vhsDropout = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("NtscDecode")) { }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                if (kernelsDirty)
                {
                    var isVhs = vhsMode > 0.5f;
                    var useNotch = combMode <= 0.5f;

                    //Y抽出: VHSでは帯域を狭め、カイザーβを下げてリンギング(輪郭の白縁)を出す
                    yTaps = NtscSignal.DesignDecodeLumaKernel(
                        NtscSignal.GetLumaCutoffHz(sharpness, isVhs, vhsTapeDegradation),
                        NtscSignal.GetLumaKaiserBeta(isVhs, vhsTapeDegradation),
                        useNotch);
                    //I/Q復調LPF: VHSではカラーアンダー方式相当まで帯域を狭めて色を盛大ににじませる
                    iTaps = NtscSignal.DesignKaiserLowPass(
                        NtscSignal.GetChromaCutoffHz(NtscSignal.IBandwidthHz, bleed, isVhs, vhsTapeDegradation),
                        NtscSignal.DecodeChromaHalfTaps, NtscSignal.DefaultKaiserBeta);
                    qTaps = NtscSignal.DesignKaiserLowPass(
                        NtscSignal.GetChromaCutoffHz(NtscSignal.QBandwidthHz, bleed, isVhs, vhsTapeDegradation),
                        NtscSignal.DecodeChromaHalfTaps, NtscSignal.DefaultKaiserBeta);
                    kernelsDirty = false;
                }

                //HLSL側cbufferのレジスタ配置と一致させる(NtscDecode.hlsl参照)
                var buffer = new List<float>(4 * (4 + 17 + 25 + 25))
                {
                    //c0: inputRect
                    inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom,
                    //c1: rasterW, rasterH, frame, setup
                    inputRect.Right - inputRect.Left, inputRect.Bottom - inputRect.Top, frame, setup,
                    //c2: combMode, noise, vhsMode, vhsTracking
                    combMode, noise, vhsMode, vhsTracking,
                    //c3: vhsNoise, vhsDropout, 予備, 予備
                    vhsNoise, vhsDropout, 0f, 0f,
                };
                NtscSignal.AppendTapsAsRegisters(buffer, yTaps, NtscSignal.DecodeLumaHalfTaps + 1);
                NtscSignal.AppendTapsAsRegisters(buffer, iTaps, NtscSignal.DecodeChromaHalfTaps + 1);
                NtscSignal.AppendTapsAsRegisters(buffer, qTaps, NtscSignal.DecodeChromaHalfTaps + 1);
                drawInformation.SetPixelShaderConstantBuffer(buffer.ToArray());
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                //復調後のRGBAも後段の走査線合成があるため精度を保つ
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
                //水平: 復調FIR+VHS水平ずれ、垂直: コムフィルタの前後ライン
                inputRects[0] = new RawRect(
                    outputRect.Left - ExpandX,
                    outputRect.Top - ExpandY,
                    outputRect.Right + ExpandX,
                    outputRect.Bottom + ExpandY);
            }

            /// <summary>入力の無効領域はFIRタップ・コム参照・水平ずれのぶん広がって出力へ影響する</summary>
            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => new(
                    invalidInputRect.Left - ExpandX,
                    invalidInputRect.Top - ExpandY,
                    invalidInputRect.Right + ExpandX,
                    invalidInputRect.Bottom + ExpandY);

            public enum Properties : int
            {
                Frame = 0,
                Setup = 1,
                Bleed = 2,
                Sharpness = 3,
                Noise = 4,
                CombMode = 5,
                VhsMode = 6,
                VhsTapeDegradation = 7,
                VhsTracking = 8,
                VhsNoise = 9,
                VhsDropout = 10,
            }
        }
    }
}
