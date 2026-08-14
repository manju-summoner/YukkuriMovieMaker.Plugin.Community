using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputSwitch
{
    internal class OutputSwitchEffectProcessor(IGraphicsDevicesAndContext devices, OutputSwitchEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly OutputSwitchEffect item = item;

        readonly IGraphicsDevicesAndContext devices = devices;

        D2DEffects.AffineTransform2D? sink;

        bool isFirst = true;
        ID2D1Image? lastSinkInput;
        ID2D1CommandList? branchCommandList;
        ID2D1Image? lastBranchSource;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            //Cachedを有効にすると出力が中間ビットマップにラスタライズされ、
            //後続のカスタムシェーダー（縁取りの膨張など）がそれを補間サンプリングする。
            //テキストのようにバウンズが非整数の素材ではピクセルグリッドが半ピクセルずれて輪郭がぼけ、
            //縁取りが本来より太くなるため有効にしない。
            sink = new D2DEffects.AffineTransform2D(devices.DeviceContext);
            disposer.Collect(sink);

            var output = sink.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || sink is null)
                return desc;

            var cur = desc.GetCustomValue<int>("OutputBranch.CurrentIndex");
            var target = item.TargetIndex;

            ID2D1Image? sinkInput;
            if (target == cur)
            {
                sinkInput = input;
            }
            else if (!desc.TryGetCustomValue<ID2D1Image>(out var targetImage, $"OutputBranch.Branch{target}"))
            {
                sinkInput = input;
            }
            else
            {
                desc = desc.SetCustomValue<ID2D1Image>(GetBranchImage(input), $"OutputBranch.Branch{cur}");
                desc = desc.SetCustomValue<int>(target, "OutputBranch.CurrentIndex");
                sinkInput = targetImage;
            }

            if (isFirst || !ReferenceEquals(lastSinkInput, sinkInput))
                sink.SetInput(0, sinkInput, true);

            isFirst = false;
            lastSinkInput = sinkInput;

            return desc;
        }

        /// <summary>
        /// 分岐画像はエフェクトの出力そのものではなくCommandListに包んでから配る。
        /// エフェクトの出力をそのまま配ると、「前景塗りつぶし」のように入力をCommandListへ記録するエフェクトを間に挟んだとき、
        /// そのCommandListが「自身を消費するエフェクトグラフに属するエフェクト」を参照する不正なトポロジになり、
        /// D2Dが D2DERR_INVALID_GRAPH_CONFIGURATION(0x8899001E) を返す（GetImageLocalBounds等が失敗する）。
        /// CommandListはエフェクトを参照で保持するため、入力が差し替わったときだけ記録し直せばよい。
        /// </summary>
        ID2D1Image GetBranchImage(ID2D1Image image)
        {
            if (branchCommandList is not null && ReferenceEquals(lastBranchSource, image))
                return branchCommandList;

            if (branchCommandList is not null)
                disposer.RemoveAndDispose(ref branchCommandList);

            var dc = devices.DeviceContext;
            var commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(image);
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            branchCommandList = commandList;
            lastBranchSource = image;
            return commandList;
        }

        protected override void setInput(ID2D1Image? input)
        {
            sink?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            sink?.SetInput(0, null, true);
            lastSinkInput = null;
            lastBranchSource = null;
        }
    }
}
