using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Localization;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;
using GradientStop = YukkuriMovieMaker.Brush.GradientStop;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;

public partial class GradientEditor : UserControl, IPropertyEditorControl
{
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public static readonly DependencyProperty ItemPropertiesProperty =
        DependencyProperty.Register(
            nameof(ItemProperties),
            typeof(ItemProperty[]),
            typeof(GradientEditor),
            new PropertyMetadata(Array.Empty<ItemProperty>()));

    public ItemProperty[] ItemProperties
    {
        get => (ItemProperty[])GetValue(ItemPropertiesProperty);
        set => SetValue(ItemPropertiesProperty, value);
    }

    public ICommand ExportAsGrdCommand { get; }
    public ICommand ExportAsPngCommand { get; }

    public GradientEditor()
    {
        InitializeComponent();

        ExportAsGrdCommand = new ActionCommand(_ => CanExport(), _ => ExportAsGrd());
        ExportAsPngCommand = new ActionCommand(_ => CanExport(), _ => ExportAsPng());

        stopsEditor.BeginEdit += OnInnerBeginEdit;
        stopsEditor.EndEdit += OnInnerEndEdit;
    }

    private void OnInnerBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

    private void OnInnerEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private bool CanExport() => GetCurrentStops().Count >= 2;

    private IReadOnlyList<GradientStop> GetCurrentStops()
    {
        var properties = ItemProperties;
        if (properties is null || properties.Length == 0)
            return [];
        var stops = properties[0].GetValue<IEnumerable<GradientStop>>();
        return stops?.ToList() ?? [];
    }

    private GradientColorStop[] ToSortedColorStops()
    {
        var stops = GetCurrentStops();
        var result = new GradientColorStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            result[i] = new GradientColorStop((float)s.Offset, s.Color.R, s.Color.G, s.Color.B, s.Color.A);
        }
        Array.Sort(result, (a, b) => a.Position.CompareTo(b.Position));
        return result;
    }

    private void ExportAsGrd()
    {
        var stops = ToSortedColorStops();
        if (stops.Length < 2) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Texts.ExportGrdFilterName,
            DefaultExt = ".grd",
        };
        if (dialog.ShowDialog() != true) return;

        GradientExportService.ExportAsGrd(dialog.FileName, "Custom Gradient", stops);
    }

    private void ExportAsPng()
    {
        var stops = ToSortedColorStops();
        if (stops.Length < 2) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Texts.ExportPngFilterName,
            DefaultExt = ".png",
        };
        if (dialog.ShowDialog() != true) return;

        GradientExportService.ExportAsPng(dialog.FileName, stops);
    }
}
