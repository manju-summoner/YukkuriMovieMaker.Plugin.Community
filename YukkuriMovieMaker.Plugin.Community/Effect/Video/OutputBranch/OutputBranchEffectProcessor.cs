using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    internal class OutputBranchEffectProcessor(IGraphicsDevicesAndContext devices, OutputBranchEffect item) : VideoEffectProcessorBase(devices)
    {
        private AffineTransform2D? transformEffect;
        private ID2D1CommandList? branchCommandList;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || branchCommandList is null)
                return desc;

            var next = desc.GetCustomValue<int>("OutputBranch.NextBranchIndex");
            if (next <= 0)
                next = 1;

            var count = item.BranchCount;
            if (count < 1)
                count = 1;

            for (var i = 0; i < count; i++)
                desc = desc.SetCustomValue<ID2D1Image>(branchCommandList, $"OutputBranch.Branch{next + i}");
            desc = desc.SetCustomValue<int>(next + count, "OutputBranch.NextBranchIndex");
            return desc;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            //上流チェーンの再評価を抑えるためキャッシュする。
            //Cachedのままエフェクトの出力を後続へ渡すと、出力が中間ビットマップにラスタライズされ、
            //後続のカスタムシェーダー（縁取りの膨張など）がそれを補間サンプリングするため、
            //テキストのようにバウンズが非整数の素材では輪郭がぼけて縁取りが太くなる。
            //下のCommandListに包んでから渡すことで、キャッシュを効かせたままこの影響を断つ。
            transformEffect = new AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
            disposer.Collect(transformEffect);

            var transformOutput = transformEffect.Output;
            disposer.Collect(transformOutput);

            //エフェクトの出力をそのまま後続と分岐先の両方へ渡すと、
            //「前景塗りつぶし」のように入力をCommandListへ記録するエフェクトが間に入ったときに、
            //そのCommandListが「自身を消費するエフェクトグラフに属するエフェクト」を参照する不正なトポロジになり、
            //D2Dが D2DERR_INVALID_GRAPH_CONFIGURATION(0x8899001E) を返す（GetImageLocalBounds等が失敗する）。
            //後続と分岐先の両方へ同じCommandListを渡せば、エフェクト本体はCommandListの中にしか現れないため成立する。
            //CommandListはエフェクトを参照で保持するので、入力を設定する前に記録しても内容は常に最新のものになる。
            var dc = devices.DeviceContext;
            var commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(transformOutput);
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            branchCommandList = commandList;
            return commandList;
        }

        protected override void setInput(ID2D1Image? input)
        {
            transformEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            transformEffect?.SetInput(0, null, true);
        }
    }
}
