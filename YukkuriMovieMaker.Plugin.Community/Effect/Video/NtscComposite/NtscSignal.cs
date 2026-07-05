using System;
using System.Collections.Generic;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジット信号（NTSC-M / NTSC-J）の仕様定数とFIRフィルタ設計。
    /// フィルタ係数は全てC#側で事前計算し、定数バッファでシェーダーに渡す。
    /// FIRは窓関数法（カイザー窓）で設計する。
    /// </summary>
    internal static class NtscSignal
    {
        //---------------------------------------------------------------
        // 信号仕様定数（SMPTE 170M / ITU-R BT.1700 準拠）
        //---------------------------------------------------------------

        /// <summary>色副搬送波周波数 fsc = 315/88 MHz = 3.579545... MHz（NTSC-M/J共通）</summary>
        public const double SubcarrierFrequencyHz = 315e6 / 88;

        /// <summary>サンプリングレート 4fsc = 14.318181... MHz（1ライン910サンプル）</summary>
        public const double SampleRateHz = SubcarrierFrequencyHz * 4;

        /// <summary>1ラインの総サンプル数（910 = 4fsc × 63.556µs。参考値、ブランキング含む）</summary>
        public const int SamplesPerLine = 910;

        /// <summary>有効映像期間のサンプル数（約52.66µs × 4fsc ≈ 754）＝仮想ラスターの横解像度</summary>
        public const int ActiveSamples = 754;

        /// <summary>有効走査線数の既定値（480ライン相当）</summary>
        public const int ActiveLines = 480;

        /// <summary>Y（輝度）帯域 4.2 MHz</summary>
        public const double LumaBandwidthHz = 4.2e6;
        /// <summary>I（橙-シアン軸）帯域 1.3 MHz</summary>
        public const double IBandwidthHz = 1.3e6;
        /// <summary>Q（緑-マゼンタ軸）帯域 0.4 MHz</summary>
        public const double QBandwidthHz = 0.4e6;

        /// <summary>NTSC-Mのセットアップ（黒レベル）7.5 IRE。NTSC-Jは0 IRE</summary>
        public const double SetupLevel75Ire = 7.5 / 100.0;

        //---------------------------------------------------------------
        // VHS（標準モード）の帯域仕様
        //---------------------------------------------------------------

        /// <summary>VHS輝度帯域（劣化度0のときの上限）。実機の水平解像度約240本≒3MHz前後</summary>
        public const double VhsLumaBandwidthMaxHz = 3.4e6;
        /// <summary>VHS輝度帯域（劣化度1のときの下限）</summary>
        public const double VhsLumaBandwidthMinHz = 2.4e6;
        /// <summary>VHSクロマ帯域（カラーアンダー方式、劣化度0）</summary>
        public const double VhsChromaBandwidthMaxHz = 0.8e6;
        /// <summary>VHSクロマ帯域（カラーアンダー方式、劣化度1）。仕様上約500kHz</summary>
        public const double VhsChromaBandwidthMinHz = 0.3e6;

        //---------------------------------------------------------------
        // FIRタップ数（シェーダー側cbufferの配列長と一致させること）
        //---------------------------------------------------------------
        // 全てのFIRは対称（線形位相）なので、中心タップ＋片側のみを保持する。
        // 「半幅H」のカーネルの配列長は H+1、全タップ数は 2H+1。

        /// <summary>エンコード側Y送信LPFの半幅（NtscEncode.hlsl の yPre[7] と対応）</summary>
        public const int EncodeLumaHalfTaps = 6;
        /// <summary>エンコード側I送信帯域制限FIRの半幅（iPre[13] と対応）</summary>
        public const int EncodeIHalfTaps = 12;
        /// <summary>エンコード側Q送信帯域制限FIRの半幅（qPre[25] と対応）</summary>
        public const int EncodeQHalfTaps = 24;
        /// <summary>デコード側Y抽出FIR（ノッチ＋LPF合成後）の半幅（yTaps[17] と対応）</summary>
        public const int DecodeLumaHalfTaps = 16;
        /// <summary>デコード側I/Q復調LPFの半幅（iTaps[25]/qTaps[25] と対応）</summary>
        public const int DecodeChromaHalfTaps = 24;

        /// <summary>ノッチ用fscバンドパスの半幅。ノッチLPF半幅との和が DecodeLumaHalfTaps になる</summary>
        public const int NotchBandpassHalfTaps = 10;
        /// <summary>ノッチと合成するY LPFの半幅</summary>
        public const int NotchLowpassHalfTaps = DecodeLumaHalfTaps - NotchBandpassHalfTaps;

        /// <summary>ノッチの片側帯域幅。fsc±0.6MHzを除去（クロマ主要帯域相当）</summary>
        public const double NotchHalfBandwidthHz = 0.6e6;

        //---------------------------------------------------------------
        // フィルタ設計パラメータ
        //---------------------------------------------------------------

        /// <summary>
        /// カイザー窓の形状係数βの既定値。β=4.0は阻止域減衰約45dB相当
        /// （経験式 A ≈ 21 + β/0.1102 の逆算）。遷移帯域はタップ数に依存し、
        /// 概ね Δf ≈ (A-8)/(2.285・2π・H) × fs となる。
        /// </summary>
        public const double DefaultKaiserBeta = 4.0;

        /// <summary>
        /// VHSモードの輝度FIRに使うβ。劣化度に応じて 3.0 → 0.2 まで下げ、
        /// 矩形窓に近づけることでギブス現象によるオーバーシュート（輪郭の白縁・
        /// リンギング）を意図的に発生させる。矩形窓のオーバーシュートは約9%。
        /// </summary>
        public const double VhsKaiserBetaMax = 3.0;
        public const double VhsKaiserBetaMin = 0.2;

        /// <summary>デコード側クロマLPFカットオフの上限。にじみ強度→0でもこれ以上は広げない
        /// （これより広いと復調LPFとして機能せず色が破綻する）</summary>
        public const double ChromaCutoffMaxHz = 2.0e6;
        /// <summary>デコード側クロマLPFカットオフの下限（にじみ強度2でもカーネルで実現可能な範囲に留める）</summary>
        public const double ChromaCutoffMinHz = 0.15e6;

        //---------------------------------------------------------------
        // 副搬送波位相
        //---------------------------------------------------------------

        /// <summary>1サンプルあたりの副搬送波位相増分。4fscサンプリングなので 2π・fsc/4fsc = π/2</summary>
        public const double PhasePerSample = Math.PI / 2;

        /// <summary>
        /// ライン・フレームによる副搬送波位相オフセット φ(line, frame)。
        /// 1ラインは227.5サイクル（半サイクル余り）のため隣接ラインで位相が180°反転し、
        /// 1フレームは525ライン×227.5 = 119437.5サイクルのためフレーム間でも180°反転する
        /// （2フレーム＝4フィールドで一巡する4フィールドシーケンス）。
        /// </summary>
        public static double SubcarrierPhaseOffset(int line, long frame)
            => Math.PI * (((line + frame) % 2 + 2) % 2);

        //---------------------------------------------------------------
        // FIR設計（窓関数法・カイザー窓）
        //---------------------------------------------------------------

        /// <summary>
        /// カイザー窓LPFの片側カーネルを設計する。戻り値は [中心, +1, +2, ... +halfTaps]（長さ halfTaps+1）。
        /// DCゲインは1に正規化する。カットオフがナイキスト以上の場合は素通し（デルタ）を返す。
        /// </summary>
        /// <param name="cutoffHz">カットオフ周波数 [Hz]（-6dB点）</param>
        /// <param name="halfTaps">片側タップ数H（全タップ数 2H+1）</param>
        /// <param name="beta">カイザー窓β</param>
        public static double[] DesignKaiserLowPass(double cutoffHz, int halfTaps, double beta)
        {
            var kernel = new double[halfTaps + 1];

            //正規化カットオフ [cycles/sample]
            var fc = cutoffHz / SampleRateHz;
            if (fc >= 0.5)
            {
                //ナイキスト以上＝帯域制限なし
                kernel[0] = 1;
                return kernel;
            }
            //半幅Hで実現できないほど狭いカットオフはカーネル長側の限界にクランプする
            //（主ローブ幅 ≈ 1/H より狭い遮断は表現できない）
            fc = Math.Max(fc, 0.5 / (halfTaps + 1));

            var i0Beta = BesselI0(beta);
            for (var k = 0; k <= halfTaps; k++)
            {
                //理想LPFのインパルス応答 2fc・sinc(2fc・k)
                var ideal = 2 * fc * Sinc(2 * fc * k);
                //カイザー窓
                var t = (double)k / (halfTaps + 1);
                var window = BesselI0(beta * Math.Sqrt(Math.Max(0, 1 - t * t))) / i0Beta;
                kernel[k] = ideal * window;
            }
            NormalizeDcGain(kernel);
            return kernel;
        }

        /// <summary>
        /// デコード側Y抽出カーネルを設計する。
        /// ノッチモード: (δ - fscバンドパス) と YLPF の畳み込み。
        /// コムモード: ライン間コムでY/C分離済みのため、プレーンなYLPFのみ。
        /// </summary>
        /// <param name="lumaCutoffHz">Y LPFカットオフ [Hz]</param>
        /// <param name="beta">カイザー窓β</param>
        /// <param name="useNotch">ノッチを合成するか（ノッチモードのみtrue）</param>
        public static double[] DesignDecodeLumaKernel(double lumaCutoffHz, double beta, bool useNotch)
        {
            if (!useNotch)
                return DesignKaiserLowPass(lumaCutoffHz, DecodeLumaHalfTaps, beta);

            //fscを中心とする狭帯域バンドパス: bp[k] = 2・lp(帯域幅)[k]・cos(π/2・k)
            //H_bp(fsc) = H_lp(0) = 1（変調により低域応答がfscへ平行移動する）
            var narrow = DesignKaiserLowPass(NotchHalfBandwidthHz, NotchBandpassHalfTaps, beta);
            var bandpass = new double[NotchBandpassHalfTaps + 1];
            for (var k = 0; k <= NotchBandpassHalfTaps; k++)
            {
                //cos(π/2・k)は k mod 4 = 0,1,2,3 に対して 1,0,-1,0
                var cos = (k % 4) switch { 0 => 1.0, 2 => -1.0, _ => 0.0 };
                bandpass[k] = 2 * narrow[k] * cos;
            }
            //fscでのゲイン: H(fsc) = Σ lp[n]・2cos²(ω0 n) = LP(0) + LP(2fsc) ≈ 1
            //DCでのゲイン:  H(0)   = Σ lp[n]・2cos(ω0 n)  = 2・LP(fsc)      ≈ 0

            //ノッチ = δ - バンドパス
            var notch = new double[NotchBandpassHalfTaps + 1];
            notch[0] = 1 - bandpass[0];
            for (var k = 1; k <= NotchBandpassHalfTaps; k++)
                notch[k] = -bandpass[k];

            //ノッチとY LPFを畳み込んで1本のカーネルに合成する（半幅 10+6=16）
            var lowpass = DesignKaiserLowPass(lumaCutoffHz, NotchLowpassHalfTaps, beta);
            return ConvolveSymmetric(notch, lowpass);
        }

        /// <summary>
        /// 対称カーネル同士の畳み込み。半幅Ha＋Hbの対称カーネル（片側表現）を返す。
        /// </summary>
        public static double[] ConvolveSymmetric(double[] halfA, double[] halfB)
        {
            var fullA = ToFullKernel(halfA);
            var fullB = ToFullKernel(halfB);
            var ha = halfA.Length - 1;
            var hb = halfB.Length - 1;
            var h = ha + hb;
            var result = new double[h + 1];
            //全長畳み込み c[n] = Σ a[i]・b[n-i] の中心は n = ha + hb。
            //結果は対称なので中心から片側（k = 0..h）だけ計算する
            for (var k = 0; k <= h; k++)
            {
                var n = ha + hb + k;
                double sum = 0;
                for (var i = 0; i < fullA.Length; i++)
                {
                    var j = n - i;
                    if (0 <= j && j < fullB.Length)
                        sum += fullA[i] * fullB[j];
                }
                result[k] = sum;
            }
            return result;
        }

        /// <summary>片側カーネルを全長カーネル（長さ2H+1、中心インデックスH）に展開する</summary>
        public static double[] ToFullKernel(double[] half)
        {
            var h = half.Length - 1;
            var full = new double[2 * h + 1];
            for (var k = -h; k <= h; k++)
                full[h + k] = half[Math.Abs(k)];
            return full;
        }

        /// <summary>
        /// 片側カーネルの周波数応答 H(f) を評価する（テスト・検証用）。
        /// f は正規化周波数 [cycles/sample]（0.25 = fsc）。
        /// </summary>
        public static double EvaluateResponse(double[] halfKernel, double normalizedFrequency)
        {
            var response = halfKernel[0];
            for (var k = 1; k < halfKernel.Length; k++)
                response += 2 * halfKernel[k] * Math.Cos(2 * Math.PI * normalizedFrequency * k);
            return response;
        }

        //---------------------------------------------------------------
        // エフェクトパラメータ → フィルタカットオフの変換ポリシー
        //---------------------------------------------------------------

        /// <summary>
        /// シャープネス（0～2、1=標準）からYカットオフを求める。帯域のスケール係数として直接乗算する。
        /// 2以上でナイキストを超え帯域制限なしになる。
        /// </summary>
        public static double GetLumaCutoffHz(double sharpness, bool isVhs, double vhsDegradation)
        {
            var baseHz = isVhs
                ? Lerp(VhsLumaBandwidthMaxHz, VhsLumaBandwidthMinHz, vhsDegradation)
                : LumaBandwidthHz;
            return baseHz * Math.Max(sharpness, 0.05);
        }

        /// <summary>
        /// にじみ強度（0～2、1=標準）からクロマ（I/Q）カットオフを求める。
        /// 「にじみ強度」は帯域のスケール係数の逆数として作用する（強度2倍→帯域半分→にじみ倍増）。
        /// VHSモードではカラーアンダー方式の帯域上限も併せて適用する。
        /// </summary>
        public static double GetChromaCutoffHz(double baseBandwidthHz, double bleed, bool isVhs, double vhsDegradation)
        {
            var cutoff = baseBandwidthHz / Math.Max(bleed, 0.25);
            if (isVhs)
                cutoff = Math.Min(cutoff, Lerp(VhsChromaBandwidthMaxHz, VhsChromaBandwidthMinHz, vhsDegradation));
            return Math.Clamp(cutoff, ChromaCutoffMinHz, ChromaCutoffMaxHz);
        }

        /// <summary>Y系FIRに使うカイザーβ。VHSモードでは劣化度に応じて下げてリンギングを出す</summary>
        public static double GetLumaKaiserBeta(bool isVhs, double vhsDegradation)
            => isVhs ? Lerp(VhsKaiserBetaMax, VhsKaiserBetaMin, vhsDegradation) : DefaultKaiserBeta;

        //---------------------------------------------------------------
        // 定数バッファ書き込みヘルパー
        //---------------------------------------------------------------

        /// <summary>
        /// HLSLのcbuffer内の float 配列は1要素が1レジスタ（16バイト）を占有するため、
        /// 各係数の後に3つのパディングを挿入してレジスタ境界に揃える。
        /// </summary>
        public static void AppendTapsAsRegisters(List<float> destination, double[] halfKernel, int expectedLength)
        {
            if (halfKernel.Length != expectedLength)
                throw new ArgumentException($"カーネル長が不正です: {halfKernel.Length} != {expectedLength}");
            foreach (var v in halfKernel)
            {
                destination.Add((float)v);
                destination.Add(0f);
                destination.Add(0f);
                destination.Add(0f);
            }
        }

        //---------------------------------------------------------------
        // 内部ヘルパー
        //---------------------------------------------------------------

        static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0, 1);

        static double Sinc(double x)
        {
            if (Math.Abs(x) < 1e-12)
                return 1;
            var px = Math.PI * x;
            return Math.Sin(px) / px;
        }

        /// <summary>第1種変形ベッセル関数 I0(x)。級数展開（カイザー窓の計算に使用）</summary>
        static double BesselI0(double x)
        {
            double sum = 1, term = 1;
            var halfX = x / 2;
            for (var k = 1; k < 64; k++)
            {
                term *= halfX / k;
                var add = term * term;
                sum += add;
                if (add < sum * 1e-15)
                    break;
            }
            return sum;
        }

        static void NormalizeDcGain(double[] halfKernel)
        {
            var sum = halfKernel[0];
            for (var k = 1; k < halfKernel.Length; k++)
                sum += 2 * halfKernel[k];
            if (Math.Abs(sum) < 1e-12)
                return;
            for (var k = 0; k < halfKernel.Length; k++)
                halfKernel[k] /= sum;
        }
    }
}
