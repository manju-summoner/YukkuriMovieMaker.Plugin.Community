using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

public class NodeEffect : VideoEffectBase
{
    public override string Label => $"{TextUi.Node} {Graph.Nodes.Count}Nodes";

    public GraphSnapshot Graph
    {
        get;
        set => Set(ref field, value);
    } = new();

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

public sealed class Processor : IVideoEffectProcessor
{
    private readonly NodeEffect _nodeEffect;

    public Processor(IGraphicsDevicesAndContext devices, NodeEffect effect)
    {
        _nodeEffect = effect;
    }

    public ID2D1Image Output { get; }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        throw new NotImplementedException();
    }

    public void SetInput(ID2D1Image? input)
    {
        throw new NotImplementedException();
    }

    public void ClearInput()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ReleaseUnmanagedResources()
    {
        // TODO アンマネージリソースをここで解放します
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing) Output.Dispose();
    }

    ~Processor()
    {
        Dispose(false);
    }
}