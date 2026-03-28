using System.Collections;
using System.Windows;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Layout;
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

        var layoutService = mainViewModel.GetType().GetProperty("LayoutService")!.GetValue(mainViewModel);
        var dockingManager = layoutService?.GetType().GetProperty("Manager")!.GetValue(layoutService) as DockingManager;
        var layout = dockingManager!.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(anchorable =>
        {
            var id = toolAreaViewModel?.GetType().GetProperty("Id")!.GetValue(toolAreaViewModel) as string;
            return anchorable.ContentId == id;
        });

        toolAreaViewModel?.GetType().GetProperty("IsVisible")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsSelected")?.SetValue(toolAreaViewModel, true);
        toolAreaViewModel?.GetType().GetProperty("IsActive")?.SetValue(toolAreaViewModel, true);
        var vm =
            toolAreaViewModel?.GetType().GetProperty("ViewModel")?.GetValue(toolAreaViewModel) as NodeEditorViewModel;

        vm?.OpenGraph(pluginItem.InternalGraph!);

        pluginItem.InternalGraph!.Committed += async (_, _) =>
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            pluginItem.InternalGraphSnapshot = await Serializer.CreateAsync(pluginItem.InternalGraph);
            EndEdit?.Invoke(this, EventArgs.Empty);
        };

        pluginItem.GraphUpdated += (_, _) => { vm?.OnGraphUpdated(); };

        layout?.IsSelectedChanged += (_, _) =>
        {
            if (!layout.IsSelected)
                toolAreaViewModel?.GetType().GetProperty("ViewModel")?.SetValue(toolAreaViewModel, vm);
        };

        if (vm is null) return;
        if (layout is null) return;

        var nodeBindings = new[]
        {
            new KeyBinding(vm.ZoomUpCommand, Key.Add, ModifierKeys.Control),
            new KeyBinding(vm.ZoomUpCommand, Key.OemPlus, ModifierKeys.Control),
            new KeyBinding(vm.ZoomDownCommand, Key.Subtract, ModifierKeys.Control),
            new KeyBinding(vm.ZoomDownCommand, Key.OemMinus, ModifierKeys.Control),
            new KeyBinding(vm.ResetZoomCommand, Key.D0, ModifierKeys.Control),
            new KeyBinding(vm.DeleteSelectedCommand, Key.Delete, ModifierKeys.None),
            new KeyBinding(vm.DeleteSelectedCommand, Key.Back, ModifierKeys.None),
            new KeyBinding(vm.CopyCommand, Key.C, ModifierKeys.Control),
            new KeyBinding(vm.CutCommand, Key.X, ModifierKeys.Control),
            new KeyBinding(vm.PasteCommand, Key.V, ModifierKeys.Control)
        };

        layout.IsActiveChanged += (_, _) =>
        {
            if (layout.IsActive)
                foreach (var kb in nodeBindings)
                    parentWindow.InputBindings.Add(kb);
            else
                foreach (var kb in nodeBindings)
                    parentWindow.InputBindings.Remove(kb);
        };
    }
}