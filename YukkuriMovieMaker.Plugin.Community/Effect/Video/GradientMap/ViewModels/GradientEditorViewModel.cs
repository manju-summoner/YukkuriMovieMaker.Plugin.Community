using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Localization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

public sealed class GradientEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<GradientColorStopViewModel> _stops = [];
    private LinearGradientBrush _gradientBrush;
    private bool _serializationSuspended;
    private bool _disposed;

    private readonly DelegateCommand _deleteStopCommand;
    private readonly DelegateCommand _exportAsGrdCommand;
    private readonly DelegateCommand _exportAsPngCommand;

    public GradientEditorViewModel()
    {
        Stops = new ReadOnlyObservableCollection<GradientColorStopViewModel>(_stops);
        _gradientBrush = BuildDefaultBrush();

        _deleteStopCommand = new DelegateCommand(
            p => { if (p is GradientColorStopViewModel vm) RemoveStop(vm); },
            p => p is GradientColorStopViewModel && _stops.Count > 2);

        _exportAsGrdCommand = new DelegateCommand(_ => ExportAsGrd(), _ => CanExport);
        _exportAsPngCommand = new DelegateCommand(_ => ExportAsPng(), _ => CanExport);

        DeleteStopCommand = _deleteStopCommand;
        ExportAsGrdCommand = _exportAsGrdCommand;
        ExportAsPngCommand = _exportAsPngCommand;
    }

    public ReadOnlyObservableCollection<GradientColorStopViewModel> Stops { get; }
    public LinearGradientBrush GradientBrush => _gradientBrush;
    public bool CanExport => _stops.Count >= 2;

    public ICommand DeleteStopCommand { get; }
    public ICommand ExportAsGrdCommand { get; }
    public ICommand ExportAsPngCommand { get; }

    public event Action<string>? GradientJsonChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadFromJson(string json)
    {
        DetachAll();
        _stops.Clear();

        var models = GradientStopSerializer.Deserialize(json);
        if (models.Length >= 2)
        {
            Array.Sort(models, (a, b) => a.Position.CompareTo(b.Position));
            for (var i = 0; i < models.Length; i++)
            {
                var m = models[i];
                var vm = new GradientColorStopViewModel(m.Position, Color.FromArgb(m.A, m.R, m.G, m.B));
                vm.PropertyChanged += OnStopChanged;
                _stops.Add(vm);
            }
        }
        else
        {
            var black = new GradientColorStopViewModel(0f, Colors.Black);
            var white = new GradientColorStopViewModel(1f, Colors.White);
            black.PropertyChanged += OnStopChanged;
            white.PropertyChanged += OnStopChanged;
            _stops.Add(black);
            _stops.Add(white);
        }

        RefreshBrush();
        RaiseCommandStates();
    }

    public void AddStopAt(float position, Color color)
    {
        for (var i = 0; i < _stops.Count; i++)
        {
            if (Math.Abs(position - _stops[i].Position) < 1e-3f)
                return;
        }

        var vm = new GradientColorStopViewModel(position, color);
        vm.PropertyChanged += OnStopChanged;

        var insertIndex = 0;
        for (var i = 0; i < _stops.Count; i++)
        {
            if (_stops[i].Position <= position)
                insertIndex = i + 1;
        }

        _stops.Insert(insertIndex, vm);
        RefreshBrush();
        Commit();
        RaiseCommandStates();
    }

    public void RemoveStop(GradientColorStopViewModel stop)
    {
        if (_stops.Count <= 2) return;
        stop.PropertyChanged -= OnStopChanged;
        _stops.Remove(stop);
        RefreshBrush();
        Commit();
        RaiseCommandStates();
    }

    public void SuspendSerialization() => _serializationSuspended = true;

    public void ResumeAndFinalizeDrag()
    {
        _serializationSuspended = false;

        var count = _stops.Count;
        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));

        DetachAll();
        _stops.Clear();
        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i].PropertyChanged += OnStopChanged;
            _stops.Add(sorted[i]);
        }

        RefreshBrush();
        Commit();
        RaiseCommandStates();
    }

    public Color SampleColorAt(float position)
    {
        var count = _stops.Count;
        if (count == 0) return Colors.Black;

        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));

        if (position <= sorted[0].Position) return sorted[0].Color;
        if (position >= sorted[^1].Position) return sorted[^1].Color;

        for (var i = 0; i < sorted.Length - 1; i++)
        {
            var l = sorted[i];
            var r = sorted[i + 1];
            if (position < l.Position || position > r.Position) continue;
            var span = r.Position - l.Position;
            if (span < 1e-6f) return r.Color;
            var t = (position - l.Position) / span;
            return Color.FromArgb(
                LerpByte(l.Color.A, r.Color.A, t),
                LerpByte(l.Color.R, r.Color.R, t),
                LerpByte(l.Color.G, r.Color.G, t),
                LerpByte(l.Color.B, r.Color.B, t));
        }
        return sorted[^1].Color;
    }

    private void OnStopChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshBrush();
        if (!_serializationSuspended) Commit();
    }

    private void RefreshBrush()
    {
        var count = _stops.Count;
        var wpfStops = new GradientStop[count];
        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));
        for (var i = 0; i < count; i++)
            wpfStops[i] = new GradientStop(sorted[i].Color, sorted[i].Position);

        var brush = new LinearGradientBrush(
            [.. wpfStops],
            new Point(0, 0),
            new Point(1, 0));
        brush.Freeze();
        _gradientBrush = brush;
        Raise(nameof(GradientBrush));
    }

    private void Commit()
    {
        var count = _stops.Count;
        var models = new GradientColorStop[count];
        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));
        for (var i = 0; i < count; i++)
            models[i] = sorted[i].ToModel();

        var json = GradientStopSerializer.Serialize(models);
        GradientJsonChanged?.Invoke(json);
    }

    private void DetachAll()
    {
        for (var i = 0; i < _stops.Count; i++)
            _stops[i].PropertyChanged -= OnStopChanged;
    }

    private void RaiseCommandStates()
    {
        Raise(nameof(CanExport));
        _deleteStopCommand.RaiseCanExecuteChanged();
        _exportAsGrdCommand.RaiseCanExecuteChanged();
        _exportAsPngCommand.RaiseCanExecuteChanged();
    }

    private void ExportAsGrd()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Texts.ExportGrdFilterName,
            DefaultExt = ".grd"
        };
        if (dialog.ShowDialog() != true) return;

        var count = _stops.Count;
        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));
        var stops = new GradientColorStop[count];
        for (var i = 0; i < count; i++)
            stops[i] = sorted[i].ToModel();

        GradientExportService.ExportAsGrd(dialog.FileName, "Custom Gradient", stops);
    }

    private void ExportAsPng()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Texts.ExportPngFilterName,
            DefaultExt = ".png"
        };
        if (dialog.ShowDialog() != true) return;

        var count = _stops.Count;
        var sorted = new GradientColorStopViewModel[count];
        for (var i = 0; i < count; i++)
            sorted[i] = _stops[i];
        Array.Sort(sorted, (a, b) => a.Position.CompareTo(b.Position));
        var stops = new GradientColorStop[count];
        for (var i = 0; i < count; i++)
            stops[i] = sorted[i].ToModel();

        GradientExportService.ExportAsPng(dialog.FileName, stops);
    }

    private static byte LerpByte(byte a, byte b, float t) => (byte)(a + (b - a) * t);

    private static LinearGradientBrush BuildDefaultBrush()
    {
        var brush = new LinearGradientBrush(
            [new GradientStop(Colors.Black, 0), new GradientStop(Colors.White, 1)],
            new Point(0, 0),
            new Point(1, 0));
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachAll();
    }

    private void Raise([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(n!));
}
