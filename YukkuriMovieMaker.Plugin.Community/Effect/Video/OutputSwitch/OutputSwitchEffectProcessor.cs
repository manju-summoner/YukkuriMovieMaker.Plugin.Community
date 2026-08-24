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
            //上流チェーンの再評価を抑えるためキャッシュする。
            //Cachedのままエフェクトの出力を後続へ渡すと、出力が中間ビットマップにラスタライズされ、
            //後続のカスタムシェーダー（縁取りの膨張など）がそれを補間サンプリングするため、
            //テキストのようにバウンズが非整数の素材では輪郭がぼけて縁取りが太くなる。
            //CommandListに包んでから渡すことで、キャッシュを効かせたままこの影響を断つ。
            sink = new D2DEffects.AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
            disposer.Collect(sink);

            var output = sink.Output;
            disposer.Collect(output);

            return Record(devices, output);
        }

        /// <summary>
        /// 画像をCommandListに記録する。CommandListはエフェクトを参照で保持するため、
        /// 入力を設定する前に記録しても内容は常に最新のものになる。
        /// </summary>
        ID2D1CommandList Record(IGraphicsDevicesAndContext devices, ID2D1Image image)
        {
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

            return commandList;
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

            branchCommandList = Record(devices, image);
            lastBranchSource = image;
            return branchCommandList;
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
