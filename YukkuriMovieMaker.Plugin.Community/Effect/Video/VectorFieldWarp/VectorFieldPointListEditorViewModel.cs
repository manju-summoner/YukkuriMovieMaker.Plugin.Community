using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointListEditorViewModel : Bindable, IDisposable
    {
        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;

        bool isMutatingSelection;
        bool disposedValue;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            if (isCanvasImageInitialized)
                return;
            isCanvasImageInitialized = true;
            RefreshCanvasImage();
        }

        public System.Windows.Media.Imaging.BitmapSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        System.Windows.Media.Imaging.BitmapSource? canvasImage;

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
                    CanvasImage = null;
                    return;
                }

                var time = editorInfo.ItemPosition.Time;
                if (time < TimeSpan.Zero)
                    time = TimeSpan.Zero;
                else if (editorInfo.ItemDuration.Time <= time && editorInfo.ItemDuration.Frame > 0)
                    time = editorInfo.VideoInfo.GetTimeFrom(editorInfo.ItemDuration.Frame - 1);

                itemVideoSource.Update(time, Player.Video.TimelineSourceUsage.Paused);
                CanvasImage = itemVideoSource.RenderBitmapSource();
            }
            catch
            {
                CanvasImage = null;
            }
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
            if (e.PropertyName != nameof(VectorFieldWarpEffect.Points))
                return;
            RebuildViewModels();
            OnPropertyChanged(nameof(CanAddPoint));
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
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
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
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                Effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
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
    }
}
