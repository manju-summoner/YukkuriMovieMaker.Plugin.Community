using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointListEditorViewModel : Bindable, IDisposable
    {
        const int PreviewMarginStep = 64;
        const int FloatsPerPoint = 8;
        const float PositionLimit = 65536f;

        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;

        bool isMutatingSelection;
        bool disposedValue;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;
        int lastCanvasImageFrame = -1;

        BitmapSource? baseCanvasImage;
        Rect baseCanvasBounds = Rect.Empty;
        byte[]? basePixels;
        int baseWidth;
        int baseHeight;
        int currentPreviewMargin = -1;
        VectorFieldWarpPreviewRenderer? previewRenderer;
        bool isPreviewRendererFailed;
        bool isWarpPending;
        bool isRenderingHooked;

        readonly AnimationWatcher amountWatcher;
        readonly AnimationWatcher maxDisplacementWatcher;
        readonly float[] pointFloats = new float[VectorFieldWarpCustomEffect.MaxPoints * FloatsPerPoint];
        readonly byte[] pointBytes = new byte[VectorFieldWarpCustomEffect.MaxPoints * FloatsPerPoint * sizeof(float)];

        public void SetEditorInfo(IEditorInfo? info)
        {
            editorInfo = info;
            //エディタのデタッチ時などにnullが渡される
            if (info is null)
                return;
            if (isCanvasImageInitialized)
            {
                if (info.ItemPosition.Frame != lastCanvasImageFrame)
                    RefreshCanvasImage();
                else
                    ScheduleWarpUpdate();
                return;
            }
            isCanvasImageInitialized = true;
            RefreshCanvasImage();
        }

        public ImageSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        ImageSource? canvasImage;

        public Size CanvasImageSize { get => canvasImageSize; private set => Set(ref canvasImageSize, value); }
        Size canvasImageSize = Size.Empty;

        public Size CanvasBaseSize { get => canvasBaseSize; private set => Set(ref canvasBaseSize, value); }
        Size canvasBaseSize = Size.Empty;

        public Rect CanvasBaseBounds { get => canvasBaseBounds; private set => Set(ref canvasBaseBounds, value); }
        Rect canvasBaseBounds = Rect.Empty;

        public Rect CanvasImageBounds { get => canvasImageBounds; private set => Set(ref canvasImageBounds, value); }
        Rect canvasImageBounds = Rect.Empty;

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
            lastCanvasImageFrame = editorInfo.ItemPosition.Frame;
            try
            {
                using var itemVideoSource = editorInfo.CreateItemVideoSource(
                    new ItemVideoSourceCreationParameter(VideoEffectSelection.UpTo(Effect)));
                if (itemVideoSource is null)
                {
                    SetBaseImage(null, Rect.Empty);
                    return;
                }

                var time = editorInfo.ItemPosition.Time;
                if (time < TimeSpan.Zero)
                    time = TimeSpan.Zero;
                else if (editorInfo.ItemDuration.Time <= time && editorInfo.ItemDuration.Frame > 0)
                    time = editorInfo.VideoInfo.GetTimeFrom(editorInfo.ItemDuration.Frame - 1);

                itemVideoSource.Update(time, Player.Video.TimelineSourceUsage.Paused);
                var bounds = itemVideoSource.Devices.DeviceContext.GetImageLocalBounds(itemVideoSource.Output);
                var image = itemVideoSource.RenderBitmapSource();
                SetBaseImage(image, new Rect(bounds.Left, bounds.Top, image.PixelWidth, image.PixelHeight));
            }
            catch
            {
                SetBaseImage(null, Rect.Empty);
            }
        }

        void SetBaseImage(BitmapSource? source, Rect imageBounds)
        {
            if (source is null)
            {
                baseCanvasImage = null;
                baseCanvasBounds = Rect.Empty;
                basePixels = null;
                currentPreviewMargin = -1;
                CanvasImage = null;
                CanvasImageSize = Size.Empty;
                CanvasBaseSize = Size.Empty;
                CanvasImageBounds = Rect.Empty;
                CanvasBaseBounds = Rect.Empty;
                return;
            }

            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();
            baseCanvasImage = converted;
            baseWidth = converted.PixelWidth;
            baseHeight = converted.PixelHeight;
            baseCanvasBounds = imageBounds;
            CanvasBaseSize = new Size(baseWidth, baseHeight);

            var renderer = EnsureRenderer();
            if (renderer is null)
            {
                basePixels = null;
                CanvasImage = baseCanvasImage;
                CanvasImageSize = CanvasBaseSize;
                CanvasImageBounds = baseCanvasBounds;
                CanvasBaseBounds = baseCanvasBounds;
                return;
            }

            basePixels = new byte[baseWidth * baseHeight * 4];
            converted.CopyPixels(basePixels, baseWidth * 4, 0);
            currentPreviewMargin = -1;
            RenderWarpedImage();
            //表示画像・サイズ・表示Boundsが揃った後に通知し、Canvasの表示リセットを新しいレイアウトで行う。
            CanvasBaseBounds = baseCanvasBounds;
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
            CanvasImageBounds = baseCanvasImage is null ? Rect.Empty : baseCanvasBounds;
        }

        void ScheduleWarpUpdate()
        {
            if (disposedValue)
                return;
            isWarpPending = true;
            HookRendering();
        }

        void HookRendering()
        {
            if (isRenderingHooked)
                return;
            isRenderingHooked = true;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        void UnhookRendering()
        {
            if (!isRenderingHooked)
                return;
            isRenderingHooked = false;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (disposedValue || previewRenderer is null || !isWarpPending)
            {
                isWarpPending = false;
                UnhookRendering();
                return;
            }
            isWarpPending = false;
            RenderWarpedImage();
            if (!isWarpPending)
                UnhookRendering();
        }

        void RenderWarpedImage()
        {
            if (disposedValue || previewRenderer is null || basePixels is null)
                return;

            var pointCount = 0;
            var velocityBound = 0f;
            foreach (var point in Effect.IsEnabled ? Effect.Points : [])
            {
                if (pointCount >= VectorFieldWarpCustomEffect.MaxPoints)
                    break;
                if (!point.IsEnabled)
                    continue;
                var radialStrength = Sanitize(GetDisplayValue(point.RadialStrength), -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                var vortexStrength = Sanitize(GetDisplayValue(point.VortexStrength), -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                if (radialStrength == 0f && vortexStrength == 0f)
                    continue;
                velocityBound += 0.5f * MathF.Sqrt(radialStrength * radialStrength + vortexStrength * vortexStrength);
                var offset = pointCount * FloatsPerPoint;
                //プレビューの入力Bitmapは元画像のBounds左上を(0,0)へ移しているため、
                //アイテム座標からBounds左上を差し引いたBitmap座標を渡す。
                var itemPoint = new Point(
                    Sanitize(GetDisplayValue(point.X), -PositionLimit, PositionLimit, 0f),
                    Sanitize(GetDisplayValue(point.Y), -PositionLimit, PositionLimit, 0f));
                var imagePoint = VectorFieldCoordinateMapper.ItemToImage(itemPoint, baseCanvasBounds, CanvasImageScale);
                pointFloats[offset] = (float)imagePoint.X;
                pointFloats[offset + 1] = (float)imagePoint.Y;
                pointFloats[offset + 2] = radialStrength;
                pointFloats[offset + 3] = vortexStrength;
                pointFloats[offset + 4] = Sanitize(GetDisplayValue(point.Radius), 1f, VectorFieldPoint.RadiusLimit, 1f);
                pointFloats[offset + 5] = 0f;
                pointFloats[offset + 6] = 0f;
                pointFloats[offset + 7] = 0f;
                pointCount++;
            }
            Buffer.BlockCopy(pointFloats, 0, pointBytes, 0, pointBytes.Length);

            var amount = Sanitize(GetDisplayValue(Effect.Amount) / 100, 0f, 1f, 0f);
            var maxDisplacement = Sanitize(GetDisplayValue(Effect.MaxDisplacement), 0f, VectorFieldWarpCustomEffect.MaxDisplacementLimit, 0f);
            var steps = Math.Clamp(Effect.IntegrationSteps, 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps);
            var margin = ComputePreviewMargin(amount, maxDisplacement, velocityBound);

            try
            {
                if (margin != currentPreviewMargin)
                {
                    previewRenderer.SetSource(basePixels, baseWidth, baseHeight, margin);
                    currentPreviewMargin = margin;
                    CanvasImage = previewRenderer.ImageSource;
                    CanvasImageSize = new Size(previewRenderer.OutputWidth, previewRenderer.OutputHeight);
                    CanvasImageBounds = VectorFieldCoordinateMapper.InflateByPixels(baseCanvasBounds, margin, CanvasImageScale);
                }
                //ロックが取れず描画できなかった場合はCompositionTarget.Rendering経由で再試行する
                //（SetBaseImageからの直接呼び出しではフック未登録のことがある）
                if (!previewRenderer.Render(pointBytes, pointCount, amount, maxDisplacement, steps))
                    ScheduleWarpUpdate();
            }
            catch
            {
                DisablePreviewRenderer();
            }
        }

        public double GetDisplayValue(Animation animation)
        {
            if (editorInfo is null)
                return animation.Values.FirstOrDefault()?.Value ?? 0;
            return animation.GetValue(editorInfo.ItemPosition.Frame, editorInfo.ItemDuration.Frame, editorInfo.VideoInfo.FPS);
        }

        static int ComputePreviewMargin(float amount, float maxDisplacement, float velocityBound)
        {
            var required = (int)Math.Ceiling(amount * Math.Min(maxDisplacement, velocityBound));
            if (required <= 0)
                return 0;
            return (required + PreviewMarginStep - 1) / PreviewMarginStep * PreviewMarginStep;
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
            //複数アイテム選択時は、先頭アイテムの選択状態を基準に同じインデックスの制御点へ適用する
            var points = Effect.Points;
            for (var index = 0; index < points.Count; index++)
            {
                if (!points[index].IsSelected)
                    continue;
                foreach (var itemProperty in ItemProperties)
                {
                    if (itemProperty.PropertyOwner is not VectorFieldWarpEffect effect || index >= effect.Points.Count)
                        continue;
                    var point = effect.Points[index];
                    point.X.AddToEachValues(deltaX);
                    point.Y.AddToEachValues(deltaY);
                }
            }
        }

        public void EndDragFromCanvas() => EndEdit?.Invoke(this, EventArgs.Empty);

        void CommitStructuralChange(ImmutableList<VectorFieldPoint> newPoints)
        {
            //複数アイテム選択時は全アイテムへ反映する。Animation等の参照共有を避けるためアイテムごとにクローンする
            foreach (var itemProperty in ItemProperties)
            {
                var clones = newPoints.Select(p =>
                {
                    var clone = JsonConvert.DeserializeObject<VectorFieldPoint>(JsonConvert.SerializeObject(p))
                                ?? VectorFieldPoint.Create(0, 0);
                    clone.IsSelected = p.IsSelected;
                    return clone;
                }).ToImmutableList();
                itemProperty.SetValue(clones);
            }
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VectorFieldWarpEffect.Points))
            {
                RebuildViewModels();
                OnPropertyChanged(nameof(CanAddPoint));
                ScheduleWarpUpdate();
            }
            else if (e.PropertyName is nameof(VectorFieldWarpEffect.IntegrationSteps) or nameof(VectorFieldWarpEffect.IsEnabled))
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
                UnhookRendering();
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
            //Animation.Valuesはリストごと置換されるため、購読時のリストを保持して確実に解除する
            ImmutableList<AnimationValue> subscribedValues = ImmutableList<AnimationValue>.Empty;

            public AnimationWatcher(Animation animation, Action callback)
            {
                this.animation = animation;
                this.callback = callback;
                animation.PropertyChanged += Animation_PropertyChanged;
                Subscribe();
            }

            void Subscribe()
            {
                subscribedValues = animation.Values;
                foreach (var value in subscribedValues)
                    value.PropertyChanged += Value_PropertyChanged;
            }

            void Unsubscribe()
            {
                foreach (var value in subscribedValues)
                    value.PropertyChanged -= Value_PropertyChanged;
                subscribedValues = ImmutableList<AnimationValue>.Empty;
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
