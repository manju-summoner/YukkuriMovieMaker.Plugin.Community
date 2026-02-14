using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

public class NodeEffect : VideoEffectBase
{
    public override string Label => TextUi.Node;

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        return [];
    }

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex,
        ExoOutputDescription exoOutputDescription)
    {
        return [""];
    }

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        return new Processor(devices, this);
    }
}

public class Processor : VideoEffectProcessorBase
{
    private IGraphicsDevicesAndContext _devices;
    private NodeEffect _effect;

    public Processor(IGraphicsDevicesAndContext devices, NodeEffect effect) : base(devices)
    {
        _effect = effect;
        _devices = devices;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        throw new NotImplementedException();
    }

    protected override void setInput(ID2D1Image? input)
    {
        throw new NotImplementedException();
    }

    protected override void ClearEffectChain()
    {
        throw new NotImplementedException();
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        throw new NotImplementedException();
    }
}