using System.ComponentModel;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

internal sealed class NodeBrushSource : IBrushSource
{
    public NodeBrushSource(ID2D1Brush brush)
    {
        Brush = brush;
    }

    public ID2D1Brush Brush { get; }

    public bool Update(TimelineItemSourceDescription desc)
    {
        return true;
    }

    public void Dispose()
    {
    }
}

internal sealed class NodeBrushParameter : IBrushParameter
{
    private readonly ID2D1Brush _brush;

    public NodeBrushParameter(ID2D1Brush brush)
    {
        _brush = brush;
    }

    public IBrushSource CreateBrush(
        IGraphicsDevicesAndContext devices)
    {
        return new NodeBrushSource(_brush);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;
    public event EventHandler<UndoRedoEventArgs>? UndoRedoCommandCreated;

    public void BeginEdit()
    {
    }

    public ValueTask EndEditAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void SetKeyFrames(KeyFrames? keyFrames)
    {
    }

    public void SetAnimationParameters(int animationLength, int videoFps)
    {
    }
}

internal static class NodeBrushFactory
{
    public static Plugin.Brush.Brush Create(BrushWrapper wrapper)
    {
        var brush = new Plugin.Brush.Brush
        {
            Parameter = new NodeBrushParameter(wrapper.Brush!)
        };

        return brush;
    }
}