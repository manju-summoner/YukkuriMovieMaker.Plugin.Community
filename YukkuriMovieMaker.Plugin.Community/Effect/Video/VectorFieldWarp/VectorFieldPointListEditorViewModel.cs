using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
        const int PreviewMargin = 256;
        const int FloatsPerPoint = 8;
        const float PositionLimit = 65536f;

        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;

        bool isMutatingSelection;
        bool disposedValue;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;

        BitmapSource? baseCanvasImage;
        VectorFieldWarpPreviewRenderer? previewRenderer;
        bool isPreviewRendererFailed;
        bool isWarpUpdateScheduled;

        readonly Dispatcher dispatcher;
        readonly AnimationWatcher amountWatcher;
        readonly AnimationWatcher maxDisplacementWatcher;
        readonly float[] pointFloats = new float[VectorFieldWarpCustomEffect.MaxPoints * FloatsPerPoint];
        readonly byte[] pointBytes = new byte[VectorFieldWarpCustomEffect.MaxPoints * FloatsPerPoint * sizeof(float)];

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            if (isCanvasImageInitialized)
                return;
            isCanvasImageInitialized = true;
            RefreshCanvasImage();
        }

        public ImageSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        ImageSource? canvasImage;

        public Size CanvasImageSize { get => canvasImageSize; private set => Set(ref canvasImageSize, value); }
        Size canvasImageSize = Size.Empty;

        public double CanvasImageScale => 1.0;

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

            dispatcher = Dispatcher.CurrentDispatcher;

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
                CanvasImage = null;
                CanvasImageSize = Size.Empty;
                return;
            }

            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();
            baseCanvasImage = converted;

            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var renderer = EnsureRenderer();
            if (renderer is null)
            {
                CanvasImage = baseCanvasImage;
                CanvasImageSize = new Size(width, height);
                return;
            }

            try
            {
                var pixels = new byte[width * height * 4];
                converted.CopyPixels(pixels, width * 4, 0);
                renderer.SetSource(pixels, width, height, PreviewMargin);
                CanvasImage = renderer.ImageSource;
                CanvasImageSize = new Size(renderer.OutputWidth, renderer.OutputHeight);
                RenderWarpedImage();
            }
            catch
            {
                DisablePreviewRenderer();
            }
        }

        VectorFieldWarpPreviewRenderer? EnsureRenderer()
        {
            if (isPreviewRendererFailed)
                return null;
            if (previewRenderer is not null)
                return previewRenderer;
            try
            {
                var renderer = new VectorFieldWarpPreviewRenderer();
                if (!renderer.IsEnabled)
                {
                    renderer.Dispose();
                    isPreviewRendererFailed = true;
                    return null;
                }
                renderer.RedrawRequested += Renderer_RedrawRequested;
                previewRenderer = renderer;
                return renderer;
            }
            catch
            {
                isPreviewRendererFailed = true;
                return null;
            }
        }

        void Renderer_RedrawRequested(object? sender, EventArgs e)
        {
            ScheduleWarpUpdate();
        }

        void DisablePreviewRenderer()
        {
            if (previewRenderer is not null)
            {
                previewRenderer.RedrawRequested -= Renderer_RedrawRequested;
                previewRenderer.Dispose();
                previewRenderer = null;
            }
            isPreviewRendererFailed = true;
            CanvasImage = baseCanvasImage;
            CanvasImageSize = baseCanvasImage is null ? Size.Empty : new Size(baseCanvasImage.PixelWidth, baseCanvasImage.PixelHeight);
        }

        void ScheduleWarpUpdate()
        {
            if (disposedValue || isWarpUpdateScheduled)
                return;
            isWarpUpdateScheduled = true;
            dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                isWarpUpdateScheduled = false;
                RenderWarpedImage();
            });
        }

        void RenderWarpedImage()
        {
            if (disposedValue || previewRenderer is null)
                return;

            var pointCount = 0;
            foreach (var point in Effect.Points)
            {
                if (pointCount >= VectorFieldWarpCustomEffect.MaxPoints)
                    break;
                if (!point.IsEnabled)
                    continue;
                var radialStrength = Sanitize(point.RadialStrength.Values.FirstOrDefault()?.Value ?? 0, -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                var vortexStrength = Sanitize(point.VortexStrength.Values.FirstOrDefault()?.Value ?? 0, -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                if (radialStrength == 0f && vortexStrength == 0f)
                    continue;
                var offset = pointCount * FloatsPerPoint;
                pointFloats[offset] = Sanitize(point.X.Values.FirstOrDefault()?.Value ?? 0, -PositionLimit, PositionLimit, 0f);
                pointFloats[offset + 1] = Sanitize(point.Y.Values.FirstOrDefault()?.Value ?? 0, -PositionLimit, PositionLimit, 0f);
                pointFloats[offset + 2] = radialStrength;
                pointFloats[offset + 3] = vortexStrength;
                pointFloats[offset + 4] = Sanitize(point.Radius.Values.FirstOrDefault()?.Value ?? 1, 1f, VectorFieldPoint.RadiusLimit, 1f);
                pointFloats[offset + 5] = 0f;
                pointFloats[offset + 6] = 0f;
                pointFloats[offset + 7] = 0f;
                pointCount++;
            }
            Buffer.BlockCopy(pointFloats, 0, pointBytes, 0, pointBytes.Length);

            var amount = Sanitize((Effect.Amount.Values.FirstOrDefault()?.Value ?? 0) / 100, 0f, 1f, 0f);
            var maxDisplacement = Sanitize(Effect.MaxDisplacement.Values.FirstOrDefault()?.Value ?? 0, 0f, VectorFieldWarpCustomEffect.MaxDisplacementLimit, 0f);
            var steps = Math.Clamp(Effect.IntegrationSteps, 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps);

            try
            {
                if (!previewRenderer.Render(pointBytes, pointCount, amount, maxDisplacement, steps))
                    ScheduleWarpUpdate();
            }
            catch
            {
                DisablePreviewRenderer();
            }
        }

        static float Sanitize(double value, float minimum, float maximum, float fallback)
        {
            if (!double.IsFinite(value))
                return fallback;
            return (float)Math.Clamp(value, minimum, maximum);
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
                amountWatcher.Dispose();
                maxDisplacementWatcher.Dispose();
                Effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.VisualChanged -= Item_VisualChanged;
                    item.Dispose();
                }
                if (previewRenderer is not null)
                {
                    previewRenderer.RedrawRequested -= Renderer_RedrawRequested;
                    previewRenderer.Dispose();
                    previewRenderer = null;
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
