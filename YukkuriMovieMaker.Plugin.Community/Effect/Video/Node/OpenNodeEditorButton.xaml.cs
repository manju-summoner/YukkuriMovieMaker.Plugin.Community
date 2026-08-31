using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Layout;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Brush;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using PortDefinition = YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port.PortDefinition;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

public partial class OpenNodeEditorButton : IPropertyEditorControl2
{
    private Window? _boundWindow;
    private EventHandler<CommittedEventArgs>? _committedHandler;
    private IEditorInfo? _editorInfo;
    private EventHandler? _graphUpdatedHandler;
    private NodeEditorViewModel? _lastResolvedViewModel;
    private EventHandler? _layoutIsActiveChangedHandler;
    private KeyBinding[]? _nodeBindings;
    private NodeGraph? _subscribedGraph;
    private LayoutAnchorable? _subscribedLayout;
    private NodeEffect? _subscribedNodeEffect;

    public OpenNodeEditorButton()
    {
        InitializeComponent();
    }

    public ItemProperty[]? ItemProperties { get; set; }
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public void SetEditorInfo(IEditorInfo? info)
    {
        _editorInfo = info;

        if (_lastResolvedViewModel == null) return;
        if (ItemProperties is not { Length: > 0 } || ItemProperties[0].Item is not NodeEffect pluginItem) return;
        if (pluginItem.InternalGraph is not { } graph) return;

        _lastResolvedViewModel.UpdateEditorInfo(graph, info);
    }

    public void ReleaseSubscriptions()
    {
        if (_subscribedGraph != null && _committedHandler != null)
            _subscribedGraph.Committed -= _committedHandler;
        if (_subscribedNodeEffect != null && _graphUpdatedHandler != null)
            _subscribedNodeEffect.GraphUpdated -= _graphUpdatedHandler;
        ReleaseShortcutBindings();

        _committedHandler = null;
        _graphUpdatedHandler = null;
        _subscribedGraph = null;
        _subscribedNodeEffect = null;
        _lastResolvedViewModel = null;
    }

    private void ReleaseShortcutBindings()
    {
        if (_subscribedLayout != null && _layoutIsActiveChangedHandler != null)
            _subscribedLayout.IsActiveChanged -= _layoutIsActiveChangedHandler;

        if (_boundWindow != null && _nodeBindings != null)
            foreach (var kb in _nodeBindings)
                _boundWindow.InputBindings.Remove(kb);

        _subscribedLayout = null;
        _layoutIsActiveChangedHandler = null;
        _nodeBindings = null;
        _boundWindow = null;
    }

    private static void EnsureInternalGraph(NodeEffect pluginItem)
    {
        if (pluginItem.InternalGraph != null) return;

        if (pluginItem.Graph.Nodes.Count > 0)
        {
            pluginItem.InternalGraph = Serializer.Restore(pluginItem.Graph);
        }
        else
        {
            var graph = new NodeGraph();

            var inputNode = new ArgumentsNode(
                new PortDefinition("InputImage", typeof(ImageWrapper)),
                new PortDefinition("FrameIndex", typeof(int))
            )
            {
                Id = Guid.NewGuid()
            };

            var outputNode = new ReturnNode(
                new PortDefinition("OutputImage", typeof(ImageWrapper))
            )
            {
                Id = Guid.NewGuid()
            };

            graph.AddNode(inputNode);
            graph.AddNode(outputNode);

            graph.SetVisualState(inputNode.Id, 100, 100);
            graph.SetVisualState(outputNode.Id, 500, 100);

            graph.Connect(inputNode.Id, "InputImage", outputNode.Id, "OutputImage");

            pluginItem.InternalGraph = graph;
            pluginItem.Graph = Serializer.Create(graph);
        }

        pluginItem.InvokeGraphUpdated();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Button_Click_Core();
        }
        catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
        {
            Debug.WriteLine($"[OpenNodeEditorButton] Failed to open node editor: {ex}");
        }
    }

    private void Button_Click_Core()
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

        _lastResolvedViewModel = vm;

        EnsureInternalGraph(pluginItem);

        vm?.OpenGraph(pluginItem.InternalGraph!, editorInfo: _editorInfo);

        if (vm != null)
        {
            var dynamicTypes = EffectNodeFactory.Create();
            vm.AddDynamicNodeTypes(dynamicTypes);
            var dynamicBrushTypes = DynamicBrushNodeFactory.Create();
            vm.AddDynamicNodeTypes(dynamicBrushTypes);
        }

        if (_subscribedGraph != null && _committedHandler != null)
            _subscribedGraph.Committed -= _committedHandler;
        if (_subscribedNodeEffect != null && _graphUpdatedHandler != null)
            _subscribedNodeEffect.GraphUpdated -= _graphUpdatedHandler;

        _committedHandler = async void (_, _) =>
        {
            try
            {
                if (pluginItem.InternalGraph == null) return;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                try
                {
                    pluginItem.InternalGraphSnapshot = await Serializer.CreateAsync(pluginItem.InternalGraph);
                }
                finally
                {
                    EndEdit?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }
        };

        var previousGraph = pluginItem.InternalGraph;
        _graphUpdatedHandler = (_, _) =>
        {
            try
            {
                var newGraph = pluginItem.InternalGraph;
                if (newGraph == null!) return;

                if (_committedHandler != null)
                {
                    newGraph.Committed -= _committedHandler;
                    newGraph.Committed += _committedHandler;
                }

                _subscribedGraph = newGraph;

                if (!ReferenceEquals(previousGraph, newGraph))
                {
                    if (previousGraph != null)
                        vm?.CloseGraphTab(previousGraph);
                    previousGraph = newGraph;
                }

                vm?.OnGraphUpdated();
                vm?.OpenGraph(newGraph, editorInfo: _editorInfo);
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] GraphUpdated handler failed: {ex}");
            }
        };

        pluginItem.InternalGraph!.Committed += _committedHandler;
        pluginItem.GraphUpdated += _graphUpdatedHandler;
        _subscribedGraph = pluginItem.InternalGraph;
        _subscribedNodeEffect = pluginItem;

        layout?.IsSelectedChanged += (_, _) =>
        {
            try
            {
                if (!layout.IsSelected)
                    toolAreaViewModel?.GetType().GetProperty("ViewModel")?.SetValue(toolAreaViewModel, vm);
                else
                    vm?.RefreshAllOpenGraphs();
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] IsSelectedChanged handler failed: {ex}");
            }
        };

        if (vm is null) return;
        if (layout is null) return;

        // 既存のショートカット登録・ハンドラを解除してから積み直す。
        // 解除しないと、パネルを閉じずにボタンを複数回押した場合や
        // プロパティエディタが再生成された場合に古いハンドラ・ショートカットが残り続ける。
        ReleaseShortcutBindings();

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

        _nodeBindings = nodeBindings;
        _boundWindow = parentWindow;
        _subscribedLayout = layout;

        _layoutIsActiveChangedHandler = (_, _) =>
        {
            try
            {
                if (layout.IsActive)
                {
                    foreach (var kb in nodeBindings)
                        parentWindow.InputBindings.Add(kb);
                    // フォーカスを取り戻したタイミングでも同様に再同期しておく。
                    vm.RefreshAllOpenGraphs();
                }
                else
                {
                    foreach (var kb in nodeBindings)
                        parentWindow.InputBindings.Remove(kb);
                }
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                Debug.WriteLine($"[OpenNodeEditorButton] IsActiveChanged handler failed: {ex}");
            }
        };
        layout.IsActiveChanged += _layoutIsActiveChangedHandler;

        if (layout.IsActive)
        {
            foreach (var kb in nodeBindings)
                parentWindow.InputBindings.Add(kb);

            vm.RefreshAllOpenGraphs();
        }
    }
}