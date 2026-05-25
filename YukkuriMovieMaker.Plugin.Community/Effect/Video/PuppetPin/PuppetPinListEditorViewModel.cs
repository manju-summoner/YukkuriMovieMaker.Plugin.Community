using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    internal sealed class PuppetPinListEditorViewModel : Bindable, IDisposable, IPropertyEditorControl2, IPropertyEditorControl, INotifyPropertyChanged
    {
        readonly ICommand selectRestCommand;
        readonly ICommand selectOffsetCommand;

        ImmutableList<PuppetPin> pins;
        ImmutableList<PuppetPinItemViewModel?> items = ImmutableList<PuppetPinItemViewModel?>.Empty;
        ImmutableList<PuppetPinItemViewModel> allViewModels = ImmutableList<PuppetPinItemViewModel>.Empty;

        object? selectedTarget;
        PuppetPinItemViewModel? selectedItem;
        IEditorInfo? editorInfo;
        int columns = 1;
        int rows = 1;
        object[] verticalLines = Array.Empty<object>();
        object[] horizontalLines = Array.Empty<object>();

        bool isMutatingSelection;
        bool isSyncing;
        bool disposedValue;

        AnimationType oldXAnimationType;
        AnimationType oldYAnimationType;
        double oldXSpan;
        double oldYSpan;

        readonly Dictionary<PuppetPin, double[]> lastPinXValues = new();
        readonly Dictionary<PuppetPin, double[]> lastPinYValues = new();

        public IEditorInfo? EditorInfo
        {
            get => editorInfo;
            private set => Set(ref editorInfo, value);
        }

        public void SetEditorInfo(IEditorInfo info) => EditorInfo = info;

        public int Columns { get => columns; private set => Set(ref columns, value); }
        public int Rows { get => rows; private set => Set(ref rows, value); }
        public object[] VerticalLines { get => verticalLines; private set => Set(ref verticalLines, value); }
        public object[] HorizontalLines { get => horizontalLines; private set => Set(ref horizontalLines, value); }

        public ImmutableList<PuppetPinItemViewModel?> Items { get => items; private set => Set(ref items, value); }
        public object? SelectedTarget { get => selectedTarget; set => Set(ref selectedTarget, value); }

        public ICommand AddPinCommand { get; }
        public ICommand RemovePinCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand OnBeginEditPointCommand { get; }
        public ICommand OnEndEditPointCommand { get; }

        public MessageBoxViewModel MessageBox { get; } = new MessageBoxViewModel();

        public bool CanAddPin => pins.Count < PuppetPinCustomEffect.MaxPins;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[] ItemProperties { get; internal set; }

        public PuppetPinListEditorViewModel(ItemProperty[] itemProperties)
        {
            ItemProperties = itemProperties;

            var effect = (PuppetPinEffect)itemProperties[0].PropertyOwner;
            pins = effect.Pins;
            effect.PropertyChanged += Effect_PropertyChanged;

            selectRestCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: false));
            selectOffsetCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: true));

            AddPinCommand = new ActionCommand(_ => CanAddPin, _ =>
            {
                InvokeBeginEdit();
                pins = pins.Add(PuppetPin.Create(0, 0));
                InvokeEndEdit();
            });

            RemovePinCommand = new ActionCommand(_ => selectedItem != null && pins.Count > 0, _ =>
            {
                if (selectedItem == null) return;
                var target = selectedItem.Model;
                InvokeBeginEdit();
                pins = pins.Remove(target);
                InvokeEndEdit();
            });

            ResetCommand = new ActionCommand(_ => pins.Count > 0, _ =>
            {
                if (MessageBox.Show(Texts.PuppetPinListResetMessage, Texts.PuppetPinListResetTitle, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    return;
                InvokeBeginEdit();
                foreach (var pin in pins)
                {
                    foreach (var v in pin.OffsetX.Values) v.Value = 0;
                    foreach (var v in pin.OffsetY.Values) v.Value = 0;
                }
                InvokeEndEdit();
            });

            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => InvokeBeginEdit());
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => InvokeEndEdit());

            RebuildItems();
        }

        void HandleSelect(object? arg, bool isOffset)
        {
            if (arg is not PuppetPinItemViewModel vm) return;

            isMutatingSelection = true;
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
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

        void SelectExclusively(PuppetPinItemViewModel target, bool isOffset)
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

        void ExtendRangeSelection(PuppetPinItemViewModel target, bool isOffset)
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

            for (var i = 0; i < allViewModels.Count; i++)
            {
                var inRange = (start <= i && i <= end);
                if (!inRange) continue;
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
            if (e.PropertyName != nameof(PuppetPinEffect.Pins)) return;

            var effect = (PuppetPinEffect)ItemProperties[0].PropertyOwner;
            if (pins == effect.Pins) return;

            pins = effect.Pins ?? ImmutableList<PuppetPin>.Empty;
            RebuildItems();
            OnPropertyChanged(nameof(CanAddPin));
        }

        void RebuildItems()
        {
            var newAllViewModels = new List<PuppetPinItemViewModel>(pins.Count);
            foreach (var pin in pins)
            {
                var vm = allViewModels.FirstOrDefault(x => x.Model == pin)
                         ?? new PuppetPinItemViewModel(pin, selectRestCommand, selectOffsetCommand);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.OffsetChanged -= Item_OffsetChanged;
                oldVm.RestChanged -= Item_RestChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
                newVm.OffsetChanged += Item_OffsetChanged;
                newVm.RestChanged += Item_RestChanged;
            }

            allViewModels = ImmutableList.CreateRange(newAllViewModels);

            var layout = ComputeGridLayout(newAllViewModels);
            Columns = layout.Columns;
            Rows = layout.Rows;

            if (VerticalLines.Length != Columns) VerticalLines = new object[Columns];
            if (HorizontalLines.Length != Rows) HorizontalLines = new object[Rows];

            Items = ImmutableList.CreateRange(layout.Cells);

            EnsureSelectionAfterRebuild();
            UpdateSelection();
        }

        readonly struct GridLayout(int columns, int rows, PuppetPinItemViewModel?[] cells)
        {
            public int Columns { get; } = columns;
            public int Rows { get; } = rows;
            public PuppetPinItemViewModel?[] Cells { get; } = cells;
        }

        static GridLayout ComputeGridLayout(List<PuppetPinItemViewModel> viewModels)
        {
            if (viewModels.Count == 0)
                return new GridLayout(1, 1, new PuppetPinItemViewModel?[1]);

            var xs = viewModels.Select(v => v.Model.RestX.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();
            var ys = viewModels.Select(v => v.Model.RestY.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();

            var bboxW = xs.Max() - xs.Min();
            var bboxH = ys.Max() - ys.Min();
            var tolerance = Math.Max(Math.Max(bboxW, bboxH) * 0.1, 1e-3);

            var colsAssign = ClusterCoordinates(xs, tolerance, out var colCount);
            var rowsAssign = ClusterCoordinates(ys, tolerance, out var rowCount);

            var cells = new PuppetPinItemViewModel?[rowCount * colCount];
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

                var expanded = new PuppetPinItemViewModel?[(rowCount + 1) * colCount];
                Array.Copy(cells, expanded, cells.Length);
                cells = expanded;
                rowCount++;
                expanded[rowCount * colCount - colCount + p.Col] = viewModels[p.Index];
            }

            return new GridLayout(colCount, rowCount, cells);
        }

        static int FindNearestEmptyCell(PuppetPinItemViewModel?[] cells, int row, int col, int rowCount, int colCount)
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

        bool isUpdateScheduled;

        void UpdateSelection()
        {
            if (isMutatingSelection) return;
            if (isUpdateScheduled) return;

            isUpdateScheduled = true;
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                isUpdateScheduled = false;
                if (disposedValue) return;
                selectedItem = allViewModels.FirstOrDefault(x => x.IsRestSelected || x.IsOffsetSelected);
                SelectedTarget = selectedItem?.Model;
            }), System.Windows.Threading.DispatcherPriority.Background);
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
            if (args.PropertyName == nameof(PuppetPinItemViewModel.IsRestSelected)
                || args.PropertyName == nameof(PuppetPinItemViewModel.IsOffsetSelected))
            {
                UpdateSelection();
            }
        }

        void Item_RestChanged(object? sender, EventArgs e)
        {
            RebuildItems();
        }

        void Item_OffsetChanged(object? sender, EventArgs e)
        {
            if (isSyncing || selectedItem == null || sender != selectedItem) return;

            isSyncing = true;
            try
            {
                SyncPointsRealTime();
            }
            finally
            {
                isSyncing = false;
            }
        }

        void SyncPointsRealTime()
        {
            if (selectedItem == null) return;

            var selectedPins = pins.Where(x => x.IsOffsetSelected).ToList();
            if (selectedPins.Count <= 1) return;

            var targetEffect = ItemProperties[0].PropertyOwner as PuppetPinEffect;
            var syncMode = targetEffect?.SyncMode ?? PuppetPinEditorPointsSync.Distance;
            if (syncMode == PuppetPinEditorPointsSync.None) return;

            var currentVector = new Vector2(
                (float)(selectedItem.Model.RestX.Values.FirstOrDefault()?.Value ?? 0),
                (float)(selectedItem.Model.RestY.Values.FirstOrDefault()?.Value ?? 0));

            var maxDistance = 1f;
            if (syncMode == PuppetPinEditorPointsSync.Distance)
            {
                var minX = selectedPins.Min(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var maxX = selectedPins.Max(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var minY = selectedPins.Min(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                var maxY = selectedPins.Max(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                Vector2[] corners = { new(minX, minY), new(maxX, minY), new(minX, maxY), new(maxX, maxY) };
                maxDistance = corners.Max(x => Vector2.Distance(x, currentVector)) + 1f;
            }

            EnsureLastValues(lastPinXValues, p => p.OffsetX);
            EnsureLastValues(lastPinYValues, p => p.OffsetY);

            ApplySync(selectedPins, syncMode, currentVector, maxDistance, lastPinXValues, p => p.OffsetX);
            ApplySync(selectedPins, syncMode, currentVector, maxDistance, lastPinYValues, p => p.OffsetY);
        }

        void EnsureLastValues(Dictionary<PuppetPin, double[]> cache, Func<PuppetPin, Animation> animSelector)
        {
            foreach (var pin in pins)
            {
                if (!cache.ContainsKey(pin))
                    cache[pin] = animSelector(pin).Values.Select(x => x.Value).ToArray();
            }
        }

        void ApplySync(
            List<PuppetPin> selectedPins,
            PuppetPinEditorPointsSync syncMode,
            Vector2 currentVector,
            float maxDistance,
            Dictionary<PuppetPin, double[]> cache,
            Func<PuppetPin, Animation> offsetSelector)
        {
            if (selectedItem == null) return;
            var sourceAnim = offsetSelector(selectedItem.Model);
            if (!cache.TryGetValue(selectedItem.Model, out var sourceLast)) return;

            for (var i = 0; i < sourceAnim.Values.Count; i++)
            {
                if (i >= sourceLast.Length) continue;
                var targetValue = sourceAnim.Values[i].Value;
                var delta = targetValue - sourceLast[i];
                if (delta == 0) continue;

                foreach (var point in selectedPins)
                {
                    if (point == selectedItem.Model) continue;
                    if (!cache.TryGetValue(point, out var pointLast))
                    {
                        pointLast = offsetSelector(point).Values.Select(x => x.Value).ToArray();
                        cache[point] = pointLast;
                    }
                    if (i >= pointLast.Length) continue;

                    var ratio = 1f;
                    if (syncMode == PuppetPinEditorPointsSync.Distance)
                    {
                        var px = (float)(point.RestX.Values.FirstOrDefault()?.Value ?? 0);
                        var py = (float)(point.RestY.Values.FirstOrDefault()?.Value ?? 0);
                        var distance = Vector2.Distance(new Vector2(px, py), currentVector);
                        ratio = Math.Max(0f, 1f - distance / maxDistance);
                    }

                    offsetSelector(point).Values[i].Value += delta * ratio;
                    pointLast[i] = offsetSelector(point).Values[i].Value;
                }

                sourceLast[i] = targetValue;
            }
        }

        void InvokeBeginEdit()
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            lastPinXValues.Clear();
            lastPinYValues.Clear();

            foreach (var pin in pins)
            {
                lastPinXValues[pin] = pin.OffsetX.Values.Select(x => x.Value).ToArray();
                lastPinYValues[pin] = pin.OffsetY.Values.Select(x => x.Value).ToArray();
            }

            if (selectedItem != null)
            {
                oldXAnimationType = selectedItem.Model.OffsetX.AnimationType;
                oldYAnimationType = selectedItem.Model.OffsetY.AnimationType;
                oldXSpan = selectedItem.Model.OffsetX.Span;
                oldYSpan = selectedItem.Model.OffsetY.Span;
            }
        }

        void InvokeEndEdit()
        {
            if (selectedItem != null)
            {
                var changedAnimType = oldXAnimationType != selectedItem.Model.OffsetX.AnimationType
                                      || oldYAnimationType != selectedItem.Model.OffsetY.AnimationType;
                var changedSpan = oldXSpan != selectedItem.Model.OffsetX.Span
                                  || oldYSpan != selectedItem.Model.OffsetY.Span;

                if (changedAnimType)
                {
                    foreach (var item in pins.Where(x => x.IsOffsetSelected))
                    {
                        item.OffsetX.AnimationType = selectedItem.Model.OffsetX.AnimationType;
                        item.OffsetY.AnimationType = selectedItem.Model.OffsetY.AnimationType;
                    }
                }
                else if (changedSpan)
                {
                    foreach (var item in pins.Where(x => x.IsOffsetSelected))
                    {
                        item.OffsetX.Span = selectedItem.Model.OffsetX.Span;
                        item.OffsetY.Span = selectedItem.Model.OffsetY.Span;
                    }
                }
            }

            lastPinXValues.Clear();
            lastPinYValues.Clear();

            var selectionStates = pins.Select(p => (p.IsRestSelected, p.IsOffsetSelected)).ToArray();
            var clonedPins = ImmutableList.CreateRange(pins.Select(p => Json.Json.GetClone(p)!));
            for (var i = 0; i < clonedPins.Count && i < selectionStates.Length; i++)
            {
                clonedPins[i].IsRestSelected = selectionStates[i].IsRestSelected;
                clonedPins[i].IsOffsetSelected = selectionStates[i].IsOffsetSelected;
            }
            pins = clonedPins;
            ItemProperties[0].SetValue(pins);

            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                var effect = (PuppetPinEffect)ItemProperties[0].PropertyOwner;
                effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.RestChanged -= Item_RestChanged;
                    item.OffsetChanged -= Item_OffsetChanged;
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
