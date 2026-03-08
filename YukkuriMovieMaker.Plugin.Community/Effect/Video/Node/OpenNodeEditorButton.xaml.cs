using System.Collections;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

public partial class OpenNodeEditorButton : IPropertyEditorControl2
{
    public OpenNodeEditorButton()
    {
        InitializeComponent();
    }

    public ItemProperty[]? ItemProperties { get; set; }
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void SetEditorInfo(IEditorInfo info)
    {
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (ItemProperties is null) throw new InvalidOperationException(TextUi.ItemPropertiesNotSet);

        var pluginItem = (NodeEffect)ItemProperties[0].Item;

        var parentWindow = Window.GetWindow(this)!;
        var mainViewModel = parentWindow.DataContext!;
        var toolAreaViewModels =
            mainViewModel.GetType().GetProperty("AnchorableAreaViewModels")!.GetValue(mainViewModel) as IEnumerable;
        var toolAreaViewModel =
            toolAreaViewModels
                ?.Cast<object>()
                .FirstOrDefault(x => (string)x.GetType().GetProperty("Title")?.GetValue(x)! == TextUi.Node);
        toolAreaViewModel?.GetType().GetProperty("IsVisible")?.SetValue(toolAreaViewModel, true);
        (toolAreaViewModel?.GetType().GetProperty("ViewModel")?.GetValue(toolAreaViewModel) as NodeEditorViewModel)
            ?.OpenGraph(pluginItem.InternalGraph!);
        pluginItem.InternalGraph!.Committed += async (_, _) =>
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            pluginItem.Graph = await Serializer.CreateAsync(pluginItem.InternalGraph);
            EndEdit?.Invoke(this, EventArgs.Empty);
        };
    }
}