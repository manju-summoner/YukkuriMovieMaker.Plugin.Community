using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointListEditorViewModel : Bindable, IDisposable
    {
        const int MaxComputeSize = 1280;
        const double PreviewMargin = 256;

        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;

        bool isMutatingSelection;
        bool disposedValue;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;

        BitmapSource? baseCanvasImage;
        byte[]? basePixels;
        int baseWidth;
        int baseHeight;
        double baseScale = 1.0;

        readonly DispatcherTimer warpTimer;
        readonly TaskScheduler uiScheduler;
        readonly AnimationWatcher amountWatcher;
        readonly AnimationWatcher maxDisplacementWatcher;
        int warpVersion;

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            if (isCanvasImageInitialized)
                return;
            isCanvasImageInitialized = true;
            RefreshCanvasImage();
        }

        public BitmapSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        BitmapSource? canvasImage;

        public double CanvasImageScale { get => canvasImageScale; private set => Set(ref canvasImageScale, value); }
        double canvasImageScale = 1.0;

        public ImmutableList<VectorFieldPointItemViewModel> CanvasPoints { get => canvasPoints; private set => Set(ref canvasPoints, value); }
        ImmutableList<VectorFieldPointItemViewModel> canvasPoints = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        public object? SelectedTarget { get => selectedTarget; private set => Set(ref selectedTarget, value, nameof(SelectedTarget), nameof(HasNoSelection)); }

        public bool HasNoSelection => selectedTarget is null;

        public string NoSelectionMessage => Texts.VectorFieldWarpNoPointSelected;

        public bool CanAddPoint => Effect.Points.Count < VectorFieldWarpCustomEffect.MaxPoints;

        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
        public ICommand RefreshImageCommand { get; }
        public ICommand OnBeginEditPointCommand { get; }
        public ICommand OnEndEditPointCommand { get; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[] ItemProperties { get; }

        VectorFieldWarpEffect Effect => (VectorFieldWarpEffect)ItemProperties[0].PropertyOwner;

        public VectorFieldPointListEditorViewModel(ItemProperty[] itemProperties)
        {
            ItemProperties = itemProperties;

            uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            warpTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            warpTimer.Tick += WarpTimer_Tick;

            Effect.PropertyChanged += Effect_PropertyChanged;
            amountWatcher = new AnimationWatcher(Effect.Amount, ScheduleWarpUpdate);
            maxDisplacementWatcher = new AnimationWatcher(Effect.MaxDisplacement, ScheduleWarpUpdate);

            AddPointCommand = new ActionCommand(_ => CanAddPoint, _ => AddPointFromCanvas(0, 0));
            RemovePointCommand = new ActionCommand(_ => selectedItem != null, _ => RemoveSelectedPointsFromCanvas());
            RefreshImageCommand = new ActionCommand(_ => true, _ => RefreshCanvasImage());
            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => BeginEdit?.Invoke(this, EventArgs.Empty));
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => EndEdit?.Invoke(this, EventArgs.Empty));

            RebuildViewModels();
        }

        void RefreshCanvasImage()
        {
            if (editorInfo is null)
                return;
            try
            {
                using var itemVideoSource = editorInfo.CreateItemVideoSource(
                    new ItemVideoSourceCreationParameter(VideoEffectSelection.UpTo(Effect)));
                if (itemVideoSource is null)
                {
                    SetBaseImage(null);
                    return;
                }

                var time = editorInfo.ItemPosition.Time;
                if (time < TimeSpan.Zero)
                    time = TimeSpan.Zero;
                else if (editorInfo.ItemDuration.Time <= time && editorInfo.ItemDuration.Frame > 0)
                    time = editorInfo.VideoInfo.GetTimeFrom(editorInfo.ItemDuration.Frame - 1);

                itemVideoSource.Update(time, Player.Video.TimelineSourceUsage.Paused);
                SetBaseImage(itemVideoSource.RenderBitmapSource());
            }
            catch
            {
                SetBaseImage(null);
            }
        }

        void SetBaseImage(BitmapSource? source)
        {
            if (source is null)
            {
                baseCanvasImage = null;
                basePixels = null;
                warpVersion++;
                CanvasImage = null;
                CanvasImageScale = 1.0;
                return;
            }

            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            BitmapSource bitmap = converted;
            var scale = Math.Min(1.0, (double)MaxComputeSize / Math.Max(1, Math.Max(converted.PixelWidth, converted.PixelHeight)));
            if (scale < 1.0)
                bitmap = new TransformedBitmap(converted, new ScaleTransform(scale, scale));
            bitmap.Freeze();

            baseWidth = bitmap.PixelWidth;
            baseHeight = bitmap.PixelHeight;
            baseScale = baseWidth / (double)converted.PixelWidth;
            basePixels = new byte[baseWidth * baseHeight * 4];
            bitmap.CopyPixels(basePixels, baseWidth * 4, 0);
            baseCanvasImage = bitmap;
            CanvasImageScale = baseScale;
            UpdateWarpedImage();
        }

        void ScheduleWarpUpdate()
        {
            if (disposedValue)
                return;
            warpTimer.Stop();
            warpTimer.Start();
        }

        void WarpTimer_Tick(object? sender, EventArgs e)
        {
            warpTimer.Stop();
            UpdateWarpedImage();
        }

        void UpdateWarpedImage()
        {
            if (disposedValue || basePixels is null)
                return;

            var pixels = basePixels;
            var width = baseWidth;
            var height = baseHeight;
            var scale = baseScale;
            var sources = Effect.Points
                .Where(p => p.IsEnabled)
                .Select(p => new WarpSource(
                    Sanitize(p.X.Values.FirstOrDefault()?.Value ?? 0, -65536, 65536, 0),
                    Sanitize(p.Y.Values.FirstOrDefault()?.Value ?? 0, -65536, 65536, 0),
                    Sanitize(p.RadialStrength.Values.FirstOrDefault()?.Value ?? 0, -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0),
                    Sanitize(p.VortexStrength.Values.FirstOrDefault()?.Value ?? 0, -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0),
                    Sanitize(p.Radius.Values.FirstOrDefault()?.Value ?? 1, 1, VectorFieldPoint.RadiusLimit, 1)))
                .Where(s => s.Radial != 0 || s.Vortex != 0)
                .ToArray();
            var amount = Sanitize((Effect.Amount.Values.FirstOrDefault()?.Value ?? 0) / 100, 0, 1, 0);
            var maxDisplacement = Sanitize(Effect.MaxDisplacement.Values.FirstOrDefault()?.Value ?? 0, 0, VectorFieldWarpCustomEffect.MaxDisplacementLimit, 0);
            var steps = Math.Clamp(Effect.IntegrationSteps, 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps);

            var version = ++warpVersion;
            Task.Run(() => ComputeWarpedImage(pixels, width, height, scale, sources, amount, maxDisplacement, steps))
                .ContinueWith(
                    task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion && version == warpVersion && !disposedValue)
                            CanvasImage = task.Result;
                    },
                    uiScheduler);
        }

        readonly record struct WarpSource(double X, double Y, double Radial, double Vortex, double Radius);

        static BitmapSource ComputeWarpedImage(byte[] source, int width, int height, double scale, WarpSource[] sources, double amount, double maxDisplacement, int steps)
        {
            var margin = (int)Math.Ceiling(PreviewMargin * scale);
            var outputWidth = width + margin * 2;
            var outputHeight = height + margin * 2;
            var output = new byte[outputWidth * outputHeight * 4];

            if (sources.Length == 0 || amount <= 0 || maxDisplacement <= 0)
            {
                for (var y = 0; y < height; y++)
                    Buffer.BlockCopy(source, y * width * 4, output, ((y + margin) * outputWidth + margin) * 4, width * 4);
            }
            else
            {
                var stepSize = amount / steps;
                var fullWidth = width / scale;
                var fullHeight = height / scale;
                Parallel.For(0, outputHeight, outputY =>
                {
                    for (var outputX = 0; outputX < outputWidth; outputX++)
                    {
                        var px = (outputX - margin - width * 0.5 + 0.5) / scale;
                        var py = (outputY - margin - height * 0.5 + 0.5) / scale;

                        for (var step = 0; step < steps; step++)
                        {
                            EvaluateField(sources, maxDisplacement, px, py, out var vx, out var vy);
                            var mx = px - vx * stepSize * 0.5;
                            var my = py - vy * stepSize * 0.5;
                            EvaluateField(sources, maxDisplacement, mx, my, out vx, out vy);
                            px -= vx * stepSize;
                            py -= vy * stepSize;
                        }

                        if (px < -fullWidth * 0.5 || px >= fullWidth * 0.5 || py < -fullHeight * 0.5 || py >= fullHeight * 0.5)
                            continue;

                        var sx = px * scale + width * 0.5 - 0.5;
                        var sy = py * scale + height * 0.5 - 0.5;
                        SampleBilinear(source, width, height, sx, sy, output, (outputY * outputWidth + outputX) * 4);
                    }
                });
            }

            var bitmap = BitmapSource.Create(outputWidth, outputHeight, 96, 96, PixelFormats.Pbgra32, null, output, outputWidth * 4);
            bitmap.Freeze();
            return bitmap;
        }

        static void EvaluateField(WarpSource[] sources, double maxDisplacement, double x, double y, out double vx, out double vy)
        {
            vx = 0;
            vy = 0;
            foreach (var source in sources)
            {
                var dx = x - source.X;
                var dy = y - source.Y;
                var denominator = Math.Max(dx * dx + dy * dy + source.Radius * source.Radius, 1e-6);
                var factor = source.Radius / denominator;
                vx += factor * (source.Radial * dx - source.Vortex * dy);
                vy += factor * (source.Radial * dy + source.Vortex * dx);
            }
            var length = Math.Sqrt(vx * vx + vy * vy);
            if (length > maxDisplacement && length > 1e-6)
            {
                var factor = maxDisplacement / length;
                vx *= factor;
                vy *= factor;
            }
        }

        static void SampleBilinear(byte[] source, int width, int height, double x, double y, byte[] output, int outputIndex)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var fx = x - x0;
            var fy = y - y0;
            var x1 = Math.Clamp(x0 + 1, 0, width - 1);
            var y1 = Math.Clamp(y0 + 1, 0, height - 1);
            x0 = Math.Clamp(x0, 0, width - 1);
            y0 = Math.Clamp(y0, 0, height - 1);

            var i00 = (y0 * width + x0) * 4;
            var i10 = (y0 * width + x1) * 4;
            var i01 = (y1 * width + x0) * 4;
            var i11 = (y1 * width + x1) * 4;
            var w00 = (1 - fx) * (1 - fy);
            var w10 = fx * (1 - fy);
            var w01 = (1 - fx) * fy;
            var w11 = fx * fy;

            for (var channel = 0; channel < 4; channel++)
            {
                var value =
                    source[i00 + channel] * w00 +
                    source[i10 + channel] * w10 +
                    source[i01 + channel] * w01 +
                    source[i11 + channel] * w11;
                output[outputIndex + channel] = (byte)Math.Clamp(value + 0.5, 0, 255);
            }
        }

        static double Sanitize(double value, double minimum, double maximum, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;
            return Math.Clamp(value, minimum, maximum);
        }

        public void SelectFromCanvas(VectorFieldPointItemViewModel vm, bool toggle)
        {
            isMutatingSelection = true;
            try
            {
                if (toggle)
                {
                    if (vm.IsSelected && allViewModels.Count(x => x.IsSelected) <= 1)
                        return;
                    vm.IsSelected = !vm.IsSelected;
                }
                else if (!vm.IsSelected)
                {
                    foreach (var item in allViewModels)
                        item.IsSelected = item == vm;
                }
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        public void AddPointFromCanvas(double x, double y)
        {
            if (!CanAddPoint)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var point in Effect.Points)
                point.IsSelected = false;
            var newPoint = VectorFieldPoint.Create(x, y);
            newPoint.IsSelected = true;
            CommitStructuralChange(Effect.Points.Add(newPoint));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemovePointFromCanvas(VectorFieldPointItemViewModel vm)
        {
            if (!Effect.Points.Contains(vm.Model))
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Points.Remove(vm.Model));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveSelectedPointsFromCanvas()
        {
            var targets = Effect.Points.Where(p => p.IsSelected).ToList();
            if (targets.Count == 0)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Points.RemoveRange(targets));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void BeginDragFromCanvas() => BeginEdit?.Invoke(this, EventArgs.Empty);

        public void MoveSelectedFromCanvas(double deltaX, double deltaY)
        {
            foreach (var point in Effect.Points)
            {
                if (!point.IsSelected)
                    continue;
                point.X.AddToEachValues(deltaX);
                point.Y.AddToEachValues(deltaY);
            }
        }

        public void EndDragFromCanvas() => EndEdit?.Invoke(this, EventArgs.Empty);

        void CommitStructuralChange(ImmutableList<VectorFieldPoint> newPoints)
        {
            var clones = newPoints.Select(p =>
            {
                var clone = JsonConvert.DeserializeObject<VectorFieldPoint>(JsonConvert.SerializeObject(p))
                            ?? VectorFieldPoint.Create(0, 0);
                clone.IsSelected = p.IsSelected;
                return clone;
            }).ToImmutableList();
            ItemProperties[0].SetValue(clones);
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VectorFieldWarpEffect.Points))
            {
                RebuildViewModels();
                OnPropertyChanged(nameof(CanAddPoint));
                ScheduleWarpUpdate();
            }
            else if (e.PropertyName == nameof(VectorFieldWarpEffect.IntegrationSteps))
            {
                ScheduleWarpUpdate();
            }
        }

        void RebuildViewModels()
        {
            var points = Effect.Points;
            var existingByModel = allViewModels.ToDictionary(x => x.Model);
            var newAllViewModels = new List<VectorFieldPointItemViewModel>(points.Count);
            foreach (var point in points)
            {
                var vm = existingByModel.TryGetValue(point, out var existing)
                    ? existing
                    : new VectorFieldPointItemViewModel(point);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.VisualChanged -= Item_VisualChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
                newVm.VisualChanged += Item_VisualChanged;
            }

            allViewModels = ImmutableList.CreateRange(newAllViewModels);
            CanvasPoints = allViewModels;

            EnsureSelectionAfterRebuild();
            UpdateSelection();
        }

        void UpdateSelection()
        {
            if (isMutatingSelection) return;
            if (disposedValue) return;
            selectedItem = allViewModels.FirstOrDefault(x => x.IsSelected);
            SelectedTarget = selectedItem?.Model;
        }

        void EnsureSelectionAfterRebuild()
        {
            if (allViewModels.FirstOrDefault(x => x.IsSelected) != null) return;
            if (allViewModels.Count == 0) return;

            isMutatingSelection = true;
            try
            {
                allViewModels[0].IsSelected = true;
            }
            finally
            {
                isMutatingSelection = false;
            }
        }

        void Item_PropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(VectorFieldPointItemViewModel.IsSelected))
                UpdateSelection();
            else if (args.PropertyName == nameof(VectorFieldPointItemViewModel.IsEnabled))
                ScheduleWarpUpdate();
        }

        void Item_VisualChanged(object? sender, EventArgs e)
        {
            ScheduleWarpUpdate();
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                warpTimer.Stop();
                warpTimer.Tick -= WarpTimer_Tick;
                amountWatcher.Dispose();
                maxDisplacementWatcher.Dispose();
                Effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.VisualChanged -= Item_VisualChanged;
                    item.Dispose();
                }
            }
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        sealed class AnimationWatcher : IDisposable
        {
            readonly Animation animation;
            readonly Action callback;

            public AnimationWatcher(Animation animation, Action callback)
            {
                this.animation = animation;
                this.callback = callback;
                animation.PropertyChanged += Animation_PropertyChanged;
                Subscribe();
            }

            void Subscribe()
            {
                foreach (var value in animation.Values)
                    value.PropertyChanged += Value_PropertyChanged;
            }

            void Unsubscribe()
            {
                foreach (var value in animation.Values)
                    value.PropertyChanged -= Value_PropertyChanged;
            }

            void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                    return;
                Unsubscribe();
                Subscribe();
                callback();
            }

            void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                callback();
            }

            public void Dispose()
            {
                Unsubscribe();
                animation.PropertyChanged -= Animation_PropertyChanged;
            }
        }
    }
}
