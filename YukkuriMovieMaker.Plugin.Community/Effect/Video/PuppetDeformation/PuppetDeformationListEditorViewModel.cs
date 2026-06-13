using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationListEditorViewModel : Bindable, IDisposable
    {
        readonly ICommand selectRestCommand;
        readonly ICommand selectOffsetCommand;

        ImmutableList<PuppetDeformationItemViewModel> allViewModels = ImmutableList<PuppetDeformationItemViewModel>.Empty;

        object? selectedTarget;
        PuppetDeformationItemViewModel? selectedItem;
        int columns = 1;
        int rows = 1;
        object[] verticalLines = [];
        object[] horizontalLines = [];

        bool isMutatingSelection;
        bool disposedValue;

        EditSnapshot? activeSnapshot;

        public void SetEditorInfo(IEditorInfo info) { }

        public int Columns { get => columns; private set => Set(ref columns, value); }
        public int Rows { get => rows; private set => Set(ref rows, value); }
        public object[] VerticalLines { get => verticalLines; private set => Set(ref verticalLines, value); }
        public object[] HorizontalLines { get => horizontalLines; private set => Set(ref horizontalLines, value); }

        public ImmutableList<PuppetDeformationItemViewModel?> Items { get => items; private set => Set(ref items, value); }
        ImmutableList<PuppetDeformationItemViewModel?> items = ImmutableList<PuppetDeformationItemViewModel?>.Empty;

        public object? SelectedTarget { get => selectedTarget; set => Set(ref selectedTarget, value); }

        public ICommand AddPinCommand { get; }
        public ICommand RemovePinCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand OnBeginEditPointCommand { get; }
        public ICommand OnEndEditPointCommand { get; }

        public MessageBoxViewModel MessageBox { get; } = new MessageBoxViewModel();

        public bool CanAddPin => Effect.Pins.Count < PuppetDeformationCustomEffect.MaxPins;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[] ItemProperties { get; internal set; }

        PuppetDeformationEffect Effect => (PuppetDeformationEffect)ItemProperties[0].PropertyOwner;

        public PuppetDeformationListEditorViewModel(ItemProperty[] itemProperties)
        {
            ItemProperties = itemProperties;

            Effect.PropertyChanged += Effect_PropertyChanged;

            selectRestCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: false));
            selectOffsetCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: true));

            AddPinCommand = new ActionCommand(_ => CanAddPin, _ =>
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                CommitStructuralChange(Effect.Pins.Add(PuppetDeformation.Create(0, 0)));
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            RemovePinCommand = new ActionCommand(_ => selectedItem != null, _ =>
            {
                if (selectedItem == null) return;
                var target = selectedItem.Model;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                CommitStructuralChange(Effect.Pins.Remove(target));
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            ResetCommand = new ActionCommand(_ => Effect.Pins.Count > 0, _ =>
            {
                if (MessageBox.Show(Texts.PuppetDeformationListResetMessage, Texts.PuppetDeformationListResetTitle, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    return;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                foreach (var pin in Effect.Pins)
                {
                    foreach (var v in pin.OffsetX.Values) v.Value = 0;
                    foreach (var v in pin.OffsetY.Values) v.Value = 0;
                }
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => OnBeginEditPoint());
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => OnEndEditPoint());

            RebuildViewModels();
        }

        void CommitStructuralChange(ImmutableList<PuppetDeformation> newPins)
        {
            var cloned = newPins.Select(p =>
            {
                var clone = JsonConvert.DeserializeObject<PuppetDeformation>(JsonConvert.SerializeObject(p))
                            ?? PuppetDeformation.Create(0, 0);
                clone.IsRestSelected = p.IsRestSelected;
                clone.IsOffsetSelected = p.IsOffsetSelected;
                return clone;
            }).ToImmutableList();
            ItemProperties[0].SetValue(cloned);
        }

        void HandleSelect(object? arg, bool isOffset)
        {
            if (arg is not PuppetDeformationItemViewModel vm) return;

            isMutatingSelection = true;
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    SelectExclusively(vm, !isOffset);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (isOffset)
                    {
                        var next = !vm.IsOffsetSelected;
                        vm.IsOffsetSelected = next;
                        if (next) vm.IsRestSelected = false;
                    }
                    else
                    {
                        var next = !vm.IsRestSelected;
                        vm.IsRestSelected = next;
                        if (next) vm.IsOffsetSelected = false;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    ExtendRangeSelection(vm, isOffset);
                }
                else
                {
                    SelectExclusively(vm, isOffset);
                }
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        void SelectExclusively(PuppetDeformationItemViewModel target, bool isOffset)
        {
            foreach (var item in allViewModels)
            {
                if (item == target) continue;
                item.IsRestSelected = false;
                item.IsOffsetSelected = false;
            }
            if (isOffset)
            {
                target.IsRestSelected = false;
                target.IsOffsetSelected = true;
            }
            else
            {
                target.IsOffsetSelected = false;
                target.IsRestSelected = true;
            }
        }

        void ExtendRangeSelection(PuppetDeformationItemViewModel target, bool isOffset)
        {
            var selectedIndices = allViewModels
                .Select((x, i) => (x, i))
                .Where(pair => pair.x.IsRestSelected || pair.x.IsOffsetSelected)
                .Select(pair => pair.i)
                .ToList();

            if (selectedIndices.Count == 0)
            {
                SelectExclusively(target, isOffset);
                return;
            }

            var min = selectedIndices.Min();
            var max = selectedIndices.Max();
            var targetIndex = allViewModels.IndexOf(target);
            var start = Math.Min(min, targetIndex);
            var end = Math.Max(max, targetIndex);

            for (var i = start; i <= end; i++)
            {
                var item = allViewModels[i];
                if (isOffset)
                {
                    item.IsRestSelected = false;
                    item.IsOffsetSelected = true;
                }
                else
                {
                    item.IsOffsetSelected = false;
                    item.IsRestSelected = true;
                }
            }
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PuppetDeformationEffect.Pins)) return;
            RebuildViewModels();
            OnPropertyChanged(nameof(CanAddPin));
        }

        void RebuildViewModels()
        {
            var pins = Effect.Pins;
            var existingByModel = allViewModels.ToDictionary(x => x.Model);
            var newAllViewModels = new List<PuppetDeformationItemViewModel>(pins.Count);
            foreach (var pin in pins)
            {
                var vm = existingByModel.TryGetValue(pin, out var existing)
                         ? existing
                         : new PuppetDeformationItemViewModel(pin, selectRestCommand, selectOffsetCommand);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.RestChanged -= Item_RestChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
                newVm.RestChanged += Item_RestChanged;
            }

            allViewModels = ImmutableList.CreateRange(newAllViewModels);

            RefreshGridLayout();
            EnsureSelectionAfterRebuild();
            UpdateSelection();
        }

        void RefreshGridLayout()
        {
            var layout = ComputeGridLayout(allViewModels);
            Columns = layout.Columns;
            Rows = layout.Rows;

            if (VerticalLines.Length != Columns) VerticalLines = new object[Columns];
            if (HorizontalLines.Length != Rows) HorizontalLines = new object[Rows];

            Items = ImmutableList.CreateRange(layout.Cells);
        }

        readonly struct GridLayout(int columns, int rows, PuppetDeformationItemViewModel?[] cells)
        {
            public int Columns { get; } = columns;
            public int Rows { get; } = rows;
            public PuppetDeformationItemViewModel?[] Cells { get; } = cells;
        }

        static GridLayout ComputeGridLayout(ImmutableList<PuppetDeformationItemViewModel> viewModels)
        {
            if (viewModels.Count == 0)
                return new GridLayout(1, 1, new PuppetDeformationItemViewModel?[1]);

            var xs = viewModels.Select(v => v.Model.RestX.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();
            var ys = viewModels.Select(v => v.Model.RestY.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();

            var bboxW = xs.Max() - xs.Min();
            var bboxH = ys.Max() - ys.Min();
            var tolerance = Math.Max(Math.Max(bboxW, bboxH) * 0.1, 1e-3);

            var colsAssign = ClusterCoordinates(xs, tolerance, out var colCount);
            var rowsAssign = ClusterCoordinates(ys, tolerance, out var rowCount);

            var cells = new PuppetDeformationItemViewModel?[rowCount * colCount];
            var pending = new List<(int Index, int Row, int Col)>();

            for (var i = 0; i < viewModels.Count; i++)
            {
                var r = rowsAssign[i];
                var c = colsAssign[i];
                var slot = r * colCount + c;
                if (cells[slot] == null)
                    cells[slot] = viewModels[i];
                else
                    pending.Add((i, r, c));
            }

            foreach (var p in pending)
            {
                var slot = FindNearestEmptyCell(cells, p.Row, p.Col, rowCount, colCount);
                if (slot >= 0)
                {
                    cells[slot] = viewModels[p.Index];
                    continue;
                }

                var expanded = new PuppetDeformationItemViewModel?[(rowCount + 1) * colCount];
                Array.Copy(cells, expanded, cells.Length);
                rowCount++;
                cells = expanded;
                var newSlot = FindNearestEmptyCell(cells, p.Row, p.Col, rowCount, colCount);
                if (newSlot >= 0)
                    cells[newSlot] = viewModels[p.Index];
            }

            return new GridLayout(colCount, rowCount, cells);
        }

        static int FindNearestEmptyCell(PuppetDeformationItemViewModel?[] cells, int row, int col, int rowCount, int colCount)
        {
            for (var radius = 1; radius <= rowCount + colCount; radius++)
            {
                for (var dr = -radius; dr <= radius; dr++)
                {
                    for (var dc = -radius; dc <= radius; dc++)
                    {
                        if (Math.Abs(dr) != radius && Math.Abs(dc) != radius) continue;
                        var r = row + dr;
                        var c = col + dc;
                        if (r < 0 || r >= rowCount || c < 0 || c >= colCount) continue;
                        var slot = r * colCount + c;
                        if (cells[slot] == null) return slot;
                    }
                }
            }
            return -1;
        }

        static int[] ClusterCoordinates(double[] values, double tolerance, out int clusterCount)
        {
            var n = values.Length;
            var result = new int[n];
            if (n == 0) { clusterCount = 0; return result; }

            var indexed = values.Select((v, i) => (Value: v, Index: i)).OrderBy(p => p.Value).ToArray();
            var cluster = 0;
            result[indexed[0].Index] = 0;
            for (var i = 1; i < n; i++)
            {
                if (indexed[i].Value - indexed[i - 1].Value > tolerance)
                    cluster++;
                result[indexed[i].Index] = cluster;
            }
            clusterCount = cluster + 1;
            return result;
        }

        void UpdateSelection()
        {
            if (isMutatingSelection) return;
            if (disposedValue) return;
            selectedItem = allViewModels.FirstOrDefault(x => x.IsRestSelected || x.IsOffsetSelected);
            SelectedTarget = selectedItem?.Model;
        }

        void EnsureSelectionAfterRebuild()
        {
            if (allViewModels.FirstOrDefault(x => x.IsRestSelected || x.IsOffsetSelected) != null) return;
            if (allViewModels.Count == 0) return;

            isMutatingSelection = true;
            try
            {
                allViewModels[0].IsRestSelected = true;
            }
            finally
            {
                isMutatingSelection = false;
            }
        }

        void Item_PropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PuppetDeformationItemViewModel.IsRestSelected)
                || args.PropertyName == nameof(PuppetDeformationItemViewModel.IsOffsetSelected))
            {
                UpdateSelection();
            }
        }

        void Item_RestChanged(object? sender, EventArgs e)
        {
            RefreshGridLayout();
        }

        void SyncRestValues()
        {
            if (selectedItem == null || activeSnapshot == null) return;

            var m = selectedItem.Model;
            var selectedPins = Effect.Pins.Where(x => x.IsRestSelected).ToList();
            if (selectedPins.Count <= 1) return;

            var changedAnimType = activeSnapshot.RestXAnimationType != m.RestX.AnimationType || activeSnapshot.RestYAnimationType != m.RestY.AnimationType;
            var changedSpan = activeSnapshot.RestXSpan != m.RestX.Span || activeSnapshot.RestYSpan != m.RestY.Span;

            if (changedAnimType)
            {
                foreach (var p in selectedPins)
                {
                    p.RestX.AnimationType = m.RestX.AnimationType;
                    p.RestY.AnimationType = m.RestY.AnimationType;
                }
                return;
            }
            if (changedSpan)
            {
                foreach (var p in selectedPins)
                {
                    p.RestX.Span = m.RestX.Span;
                    p.RestY.Span = m.RestY.Span;
                }
                return;
            }

            var changedXIndex = FindChangedValueIndex(activeSnapshot.RestXValues, m.RestX);
            var changedYIndex = FindChangedValueIndex(activeSnapshot.RestYValues, m.RestY);
            if (changedXIndex < 0 && changedYIndex < 0) return;

            foreach (var point in selectedPins.Where(p => p != m))
            {
                ApplyValueDelta(changedXIndex, m.RestX, point.RestX, activeSnapshot.RestXValues, 1f);
                ApplyValueDelta(changedYIndex, m.RestY, point.RestY, activeSnapshot.RestYValues, 1f);
            }
        }

        void SyncOffsetValues()
        {
            if (selectedItem == null || activeSnapshot == null) return;

            var m = selectedItem.Model;
            var selectedPins = Effect.Pins.Where(x => x.IsOffsetSelected).ToList();
            if (selectedPins.Count <= 1) return;

            var changedAnimType = activeSnapshot.OffsetXAnimationType != m.OffsetX.AnimationType || activeSnapshot.OffsetYAnimationType != m.OffsetY.AnimationType;
            var changedSpan = activeSnapshot.OffsetXSpan != m.OffsetX.Span || activeSnapshot.OffsetYSpan != m.OffsetY.Span;

            if (changedAnimType)
            {
                foreach (var p in selectedPins)
                {
                    p.OffsetX.AnimationType = m.OffsetX.AnimationType;
                    p.OffsetY.AnimationType = m.OffsetY.AnimationType;
                }
                return;
            }
            if (changedSpan)
            {
                foreach (var p in selectedPins)
                {
                    p.OffsetX.Span = m.OffsetX.Span;
                    p.OffsetY.Span = m.OffsetY.Span;
                }
                return;
            }

            var syncMode = Effect.SyncMode;
            if (syncMode == PuppetDeformationEditorPointsSync.None) return;

            var changedXIndex = FindChangedValueIndex(activeSnapshot.OffsetXValues, m.OffsetX);
            var changedYIndex = FindChangedValueIndex(activeSnapshot.OffsetYValues, m.OffsetY);
            if (changedXIndex < 0 && changedYIndex < 0) return;

            var sourceVector = new Vector2(
                (float)(m.RestX.Values.FirstOrDefault()?.Value ?? 0),
                (float)(m.RestY.Values.FirstOrDefault()?.Value ?? 0));

            var maxDistance = 1f;
            if (syncMode == PuppetDeformationEditorPointsSync.Distance)
            {
                var minX = selectedPins.Min(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var maxX = selectedPins.Max(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var minY = selectedPins.Min(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                var maxY = selectedPins.Max(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                Vector2[] corners = [new(minX, minY), new(maxX, minY), new(minX, maxY), new(maxX, maxY)];
                maxDistance = corners.Max(x => Vector2.Distance(x, sourceVector)) + 1f;
            }

            foreach (var point in selectedPins.Where(p => p != m))
            {
                var ratio = ComputeDistanceRatio(syncMode, point, sourceVector, maxDistance);
                ApplyValueDelta(changedXIndex, m.OffsetX, point.OffsetX, activeSnapshot.OffsetXValues, ratio);
                ApplyValueDelta(changedYIndex, m.OffsetY, point.OffsetY, activeSnapshot.OffsetYValues, ratio);
            }
        }

        static int FindChangedValueIndex(double[] oldValues, Animation animation)
        {
            for (var i = 0; i < Math.Min(oldValues.Length, animation.Values.Count); i++)
            {
                if (oldValues[i] != animation.Values[i].Value) return i;
            }
            return -1;
        }

        static float ComputeDistanceRatio(PuppetDeformationEditorPointsSync syncMode, PuppetDeformation point, Vector2 sourceVector, float maxDistance)
        {
            if (syncMode != PuppetDeformationEditorPointsSync.Distance) return 1f;
            var px = (float)(point.RestX.Values.FirstOrDefault()?.Value ?? 0);
            var py = (float)(point.RestY.Values.FirstOrDefault()?.Value ?? 0);
            var distance = Vector2.Distance(new Vector2(px, py), sourceVector);
            return Math.Max(0f, 1f - distance / maxDistance);
        }

        static void ApplyValueDelta(int changedIndex, Animation source, Animation target, double[] oldValues, float ratio)
        {
            if (changedIndex < 0) return;
            if (changedIndex >= source.Values.Count || changedIndex >= target.Values.Count || changedIndex >= oldValues.Length) return;
            var delta = source.Values[changedIndex].Value - oldValues[changedIndex];
            target.Values[changedIndex].Value += delta * ratio;
        }

        void OnBeginEditPoint()
        {
            if (selectedItem != null)
            {
                var m = selectedItem.Model;
                activeSnapshot = new EditSnapshot(m);
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        void OnEndEditPoint()
        {
            if (selectedItem != null)
            {
                SyncRestValues();
                SyncOffsetValues();
            }

            activeSnapshot = null;

            EndEdit?.Invoke(this, EventArgs.Empty);
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
                    item.RestChanged -= Item_RestChanged;
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

        sealed class EditSnapshot
        {
            public AnimationType RestXAnimationType { get; }
            public AnimationType RestYAnimationType { get; }
            public double RestXSpan { get; }
            public double RestYSpan { get; }
            public double[] RestXValues { get; }
            public double[] RestYValues { get; }

            public AnimationType OffsetXAnimationType { get; }
            public AnimationType OffsetYAnimationType { get; }
            public double OffsetXSpan { get; }
            public double OffsetYSpan { get; }
            public double[] OffsetXValues { get; }
            public double[] OffsetYValues { get; }

            public EditSnapshot(PuppetDeformation model)
            {
                RestXAnimationType = model.RestX.AnimationType;
                RestYAnimationType = model.RestY.AnimationType;
                RestXSpan = model.RestX.Span;
                RestYSpan = model.RestY.Span;
                RestXValues = model.RestX.Values.Select(x => x.Value).ToArray();
                RestYValues = model.RestY.Values.Select(x => x.Value).ToArray();

                OffsetXAnimationType = model.OffsetX.AnimationType;
                OffsetYAnimationType = model.OffsetY.AnimationType;
                OffsetXSpan = model.OffsetX.Span;
                OffsetYSpan = model.OffsetY.Span;
                OffsetXValues = model.OffsetX.Values.Select(x => x.Value).ToArray();
                OffsetYValues = model.OffsetY.Values.Select(x => x.Value).ToArray();
            }
        }
    }
}
