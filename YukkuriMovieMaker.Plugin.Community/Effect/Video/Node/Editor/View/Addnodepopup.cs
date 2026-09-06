using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public sealed class AddNodePopup : Popup
{
    private readonly StackPanel _itemsHost;
    private readonly Dictionary<string, List<NodeTypeInfo>> _nodeCategories;
    private readonly Action<Type> _onNodeSelected;
    private readonly List<Popup> _openSubmenus = [];
    private readonly TextBox _searchBox;

    public AddNodePopup(Dictionary<string, List<NodeTypeInfo>> nodeCategories, Action<Type> onNodeSelected)
    {
        Style = null;

        _nodeCategories = nodeCategories;
        _onNodeSelected = onNodeSelected;

        _searchBox = new TextBox
        {
            Style = null,
            MinWidth = 220,
            Margin = new Thickness(6, 4, 6, 4),
            Background = SystemColors.WindowBrush,
            Foreground = SystemColors.WindowTextBrush
        };
        _searchBox.TextChanged += (_, _) => Rebuild();

        _itemsHost = new StackPanel { Style = null };
        var scroll = new ScrollViewer
        {
            Style = null,
            MaxHeight = 480,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _itemsHost
        };

        var root = new StackPanel { Style = null };
        root.Children.Add(_searchBox);
        root.Children.Add(scroll);

        AllowsTransparency = false;
        StaysOpen = false;
        Placement = PlacementMode.MousePoint;
        PlacementTarget = Application.Current?.MainWindow;
        Child = new Border
        {
            Style = null,
            BorderThickness = new Thickness(1),
            BorderBrush = SystemColors.ActiveBorderBrush,
            Background = SystemColors.MenuBrush,
            Child = root
        };

        Opened += OnOpened;
        Closed += OnClosed;
        PreviewGotKeyboardFocus += OnPreviewGotKeyboardFocus;
        PreviewKeyDown += OnPreviewKeyDown;

        Rebuild();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(Child) is HwndSource source)
            SetFocus(source.Handle);

        _searchBox.Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CloseAllSubmenus();
        Application.Current?.MainWindow?.Activate();
    }

    private void OnPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ReferenceEquals(e.NewFocus, _searchBox))
        {
            e.Handled = true;
            _searchBox.Focus();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        IsOpen = false;
        e.Handled = true;
    }

    private void CloseAllSubmenus()
    {
        foreach (var p in _openSubmenus)
            if (p.IsOpen)
                p.IsOpen = false;
        _openSubmenus.Clear();
    }

    private void Rebuild()
    {
        _itemsHost.Children.Clear();
        CloseAllSubmenus();

        var keyword = _searchBox.Text.Trim();
        if (keyword.Length == 0)
            PopulateRows(_itemsHost, BuildCategoryTree());
        else
            PopulateSearchRows(_itemsHost, keyword);
    }

    private CategoryNode BuildCategoryTree()
    {
        var root = new CategoryNode();

        foreach (var (path, nodes) in _nodeCategories)
        {
            var current = root;
            foreach (var part in path.Split('/'))
            {
                if (!current.Children.TryGetValue(part, out var child))
                {
                    child = new CategoryNode { Name = part };
                    current.Children[part] = child;
                }

                current = child;
            }

            current.Nodes.AddRange(nodes);
        }

        return root;
    }

    private void PopulateRows(Panel host, CategoryNode node, DispatcherTimer? parentTimer = null)
    {
        Popup? openChild = null;
        Border? openChildRow = null;
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };

        foreach (var child in node.Children.Values.OrderBy(c => c.Name))
        {
            var row = CreateRow(child.Name, true);

            row.MouseEnter += (_, _) =>
            {
                parentTimer?.Stop();

                closeTimer.Stop();
                if (openChildRow == row) return;
                CloseChild();

                var childHost = new StackPanel { Style = null };
                var childPopup = new Popup
                {
                    Style = null,
                    AllowsTransparency = false,
                    StaysOpen = true,
                    Placement = PlacementMode.Right,
                    PlacementTarget = row,
                    Child = new Border
                    {
                        Style = null,
                        BorderThickness = new Thickness(1),
                        BorderBrush = SystemColors.ActiveBorderBrush, // 親Popupと合わせるために追加を推奨
                        Background = SystemColors.MenuBrush, // 親Popupと合わせるために追加を推奨
                        Child = new ScrollViewer
                        {
                            Style = null,
                            MaxHeight = 480,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = childHost
                        }
                    }
                };
                childPopup.MouseEnter += (_, _) =>
                {
                    closeTimer.Stop();
                    parentTimer?.Stop();
                };
                childPopup.MouseLeave += (_, _) => { closeTimer.Start(); };

                PopulateRows(childHost, child, closeTimer);

                _openSubmenus.Add(childPopup);
                openChild = childPopup;
                openChildRow = row;
                childPopup.IsOpen = true;
            };

            row.MouseLeave += (_, _) =>
            {
                if (openChild is { IsOpen: true })
                    return;
                closeTimer.Start();
            };

            host.Children.Add(row);
        }

        foreach (var nodeInfo in node.Nodes.OrderBy(n => n.Label))
        {
            var row = CreateLeafRow(nodeInfo, nodeInfo.Label);
            row.MouseEnter += (_, _) => parentTimer?.Stop();
            host.Children.Add(row);
        }

        void CloseChild()
        {
            closeTimer.Stop();
            if (openChild == null) return;
            _openSubmenus.Remove(openChild);
            if (openChild.IsOpen) openChild.IsOpen = false;
            openChild = null;
            openChildRow = null;
        }
    }

    private void PopulateSearchRows(Panel host, string keyword)
    {
        var matches = _nodeCategories
            .SelectMany(c => c.Value)
            .Where(n => n.Label.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
                        || n.Description.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(n => n.Category)
            .ThenBy(n => n.Label);

        var found = false;
        foreach (var nodeInfo in matches)
        {
            found = true;
            host.Children.Add(CreateLeafRow(nodeInfo, $"{nodeInfo.Category} / {nodeInfo.Label}"));
        }

        if (!found)
            host.Children.Add(new TextBlock
            {
                Style = null,
                Text = TextNode.NoMatchingResults,
                Foreground = SystemColors.GrayTextBrush,
                Margin = new Thickness(8, 4, 4, 4)
            });
    }

    private static Border CreateRow(string text, bool hasArrow)
    {
        var grid = new Grid { Style = null };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Style = null,
            Text = text, Margin = new Thickness(8, 3, 8, 3), Foreground = SystemColors.WindowTextBrush
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        if (hasArrow)
        {
            var arrow = new TextBlock
            {
                Style = null,
                Text = "▶", FontSize = 9, Margin = new Thickness(0, 3, 6, 3),
                Foreground = SystemColors.WindowTextBrush
            };
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);
        }

        var row = new Border { Style = null, Background = Brushes.Transparent, Child = grid };
        row.MouseEnter += (_, _) =>
        {
            row.Background = SystemColors.HighlightBrush;
            label.Foreground = SystemColors.HighlightTextBrush;
            if (hasArrow && grid.Children.Count > 1 && grid.Children[1] is TextBlock arrowBlock)
                arrowBlock.Foreground = SystemColors.HighlightTextBrush;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            label.Foreground = SystemColors.WindowTextBrush;
            if (hasArrow && grid.Children.Count > 1 && grid.Children[1] is TextBlock arrowBlock)
                arrowBlock.Foreground = SystemColors.WindowTextBrush;
        };
        return row;
    }

    private Border CreateLeafRow(NodeTypeInfo nodeInfo, string header)
    {
        var grid = new Grid { Style = null };
        var icon = new Rectangle
        {
            Style = null,
            Width = 5,
            Height = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(8, 3, 0, 3),
            Fill = ColorToBrushConverter.Convert(nodeInfo.Color)
        };
        var label = new TextBlock
        {
            Style = null,
            Text = header, Margin = new Thickness(18, 3, 8, 3), Foreground = SystemColors.WindowTextBrush
        };
        grid.Children.Add(icon);
        grid.Children.Add(label);

        var row = new Border
            { Style = null, Background = Brushes.Transparent, ToolTip = nodeInfo.Description, Child = grid };
        row.MouseEnter += (_, _) =>
        {
            row.Background = SystemColors.HighlightBrush;
            label.Foreground = SystemColors.HighlightTextBrush;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            label.Foreground = SystemColors.WindowTextBrush;
        };
        row.MouseLeftButtonUp += (_, _) =>
        {
            _onNodeSelected(nodeInfo.Type);
            IsOpen = false;
        };
        return row;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    private sealed class CategoryNode
    {
        public readonly Dictionary<string, CategoryNode> Children = new();
        public readonly List<NodeTypeInfo> Nodes = [];
        public string Name = "";
    }
}