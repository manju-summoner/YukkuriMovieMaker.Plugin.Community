using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジット信号シミュレーションのProcessor。
    /// 仮想ラスター化 → エンコード → デコード → 復元 の4パスを連結し、
    /// 映像を一度コンポジット信号(Y+変調色信号の1次元波形)へ落としてから復調することで、
    /// クロスカラー・ドットクロール・色にじみを信号処理の必然として発生させる。
    /// 全パスはステートレスで、時間変化はフレーム番号パラメータのみで表現する。
    /// </summary>
    class NtscCompositeEffectProcessor(IGraphicsDevicesAndContext devices, NtscCompositeEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;

        NtscRasterize rasterize = null!;
        NtscEncode encode = null!;
        NtscDecode decode = null!;
        NtscRestore restore = null!;

        bool isFirst = true;
        Vector4 sourceRect;
        float rasterHeight;
        float frame;
        float setup;
        float bleed;
        float sharpness;
        float noise;
        float combMode;
        float vhsMode;
        float vhsTapeDegradation;
        float vhsTracking;
        float vhsNoise;
        float vhsDropout;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            rasterize = new NtscRasterize(devices);
            if (!rasterize.IsEnabled)
            {
                rasterize.Dispose();
                rasterize = null!;
                return null;
            }
            disposer.Collect(rasterize);

            encode = new NtscEncode(devices);
            if (!encode.IsEnabled)
            {
                encode.Dispose();
                encode = null!;
                return null;
            }
            disposer.Collect(encode);

            decode = new NtscDecode(devices);
            if (!decode.IsEnabled)
            {
                decode.Dispose();
                decode = null!;
                return null;
            }
            disposer.Collect(decode);

            restore = new NtscRestore(devices);
            if (!restore.IsEnabled)
            {
                restore.Dispose();
                restore = null!;
                return null;
            }
            disposer.Collect(restore);

            //4パスを直列に接続する
            using (var output = rasterize.Output)
                encode.SetInput(0, output, true);
            using (var output = encode.Output)
                decode.SetInput(0, output, true);
            using (var output = decode.Output)
                restore.SetInput(0, output, true);

            //ラスター横解像度は固定(4fscの有効サンプル数)
            rasterize.RasterWidth = NtscSignal.ActiveSamples;

            var effectOutput = restore.Output;
            disposer.Collect(effectOutput);
            return effectOutput;
        }

        protected override void setInput(ID2D1Image? input)
        {
            rasterize?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            rasterize?.SetInput(0, null, true);
            encode?.SetInput(0, null, true);
            decode?.SetInput(0, null, true);
            restore?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect)
                return desc;

            var frameIndex = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var bleed = (float)(item.ColorBleed.GetValue(frameIndex, length, fps) / 100);
            var sharpness = (float)(item.Sharpness.GetValue(frameIndex, length, fps) / 100);
            var noise = (float)(item.Noise.GetValue(frameIndex, length, fps) / 100);
            var vhsTapeDegradation = (float)(item.VhsTapeDegradation.GetValue(frameIndex, length, fps) / 100);
            var vhsTracking = (float)(item.VhsTracking.GetValue(frameIndex, length, fps) / 100);
            var vhsNoise = (float)(item.VhsNoise.GetValue(frameIndex, length, fps) / 100);
            var vhsDropout = (float)(item.VhsDropout.GetValue(frameIndex, length, fps) / 100);
            var vhsMode = item.IsVhsMode ? 1f : 0f;
            var combMode = item.YCSeparation == NtscYCSeparationMode.Comb ? 1f : 0f;
            var setup = item.SetupLevel == NtscSetupLevel.Ire75
                ? (float)NtscSignal.SetupLevel75Ire
                : 0f;
            var rasterHeight = (float)(int)item.ScanlineCount;

            //フレーム番号はfloatで正確に表せる範囲へ折り返す。折り返し幅は偶数なので
            //位相交番(偶奇)は保存され、ノイズの周期(約73分@60fps)も知覚できない
            var frame = (float)(frameIndex % 262144);

            //入力画像の実際の矩形をラスター化・復元パスへ伝える
            if (input is not null)
            {
                var bounds = devices.DeviceContext.GetImageLocalBounds(input);
                var rect = new Vector4(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                if (isFirst || sourceRect != rect)
                {
                    rasterize.SourceRect = rect;
                    restore.SourceRect = rect;
                }
                sourceRect = rect;
            }

            if (isFirst || this.rasterHeight != rasterHeight)
                rasterize.RasterHeight = rasterHeight;
            if (isFirst || this.frame != frame)
            {
                encode.Frame = frame;
                decode.Frame = frame;
            }
            if (isFirst || this.setup != setup)
            {
                encode.Setup = setup;
                decode.Setup = setup;
            }
            if (isFirst || this.bleed != bleed)
            {
                encode.Bleed = bleed;
                decode.Bleed = bleed;
            }
            if (isFirst || this.sharpness != sharpness)
            {
                encode.Sharpness = sharpness;
                decode.Sharpness = sharpness;
            }
            if (isFirst || this.noise != noise)
                decode.Noise = noise;
            if (isFirst || this.combMode != combMode)
                decode.CombMode = combMode;
            if (isFirst || this.vhsMode != vhsMode)
                decode.VhsMode = vhsMode;
            if (isFirst || this.vhsTapeDegradation != vhsTapeDegradation)
                decode.VhsTapeDegradation = vhsTapeDegradation;
            if (isFirst || this.vhsTracking != vhsTracking)
                decode.VhsTracking = vhsTracking;
            if (isFirst || this.vhsNoise != vhsNoise)
                decode.VhsNoise = vhsNoise;
            if (isFirst || this.vhsDropout != vhsDropout)
                decode.VhsDropout = vhsDropout;

            isFirst = false;
            this.rasterHeight = rasterHeight;
            this.frame = frame;
            this.setup = setup;
            this.bleed = bleed;
            this.sharpness = sharpness;
            this.noise = noise;
            this.combMode = combMode;
            this.vhsMode = vhsMode;
            this.vhsTapeDegradation = vhsTapeDegradation;
            this.vhsTracking = vhsTracking;
            this.vhsNoise = vhsNoise;
            this.vhsDropout = vhsDropout;

            return desc;
        }
    }
}
