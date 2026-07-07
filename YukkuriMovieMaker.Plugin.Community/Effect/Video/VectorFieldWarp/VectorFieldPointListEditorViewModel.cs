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
        readonly ICommand selectCommand;

        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;
        int columns = 1;
        int rows = 1;
        object[] verticalLines = [];
        object[] horizontalLines = [];

        bool isMutatingSelection;
        bool disposedValue;

        public void SetEditorInfo(IEditorInfo info) { }

        public int Columns { get => columns; private set => Set(ref columns, value); }
        public int Rows { get => rows; private set => Set(ref rows, value); }
        public object[] VerticalLines { get => verticalLines; private set => Set(ref verticalLines, value); }
        public object[] HorizontalLines { get => horizontalLines; private set => Set(ref horizontalLines, value); }

        public ImmutableList<VectorFieldPointItemViewModel?> Items { get => items; private set => Set(ref items, value); }
        ImmutableList<VectorFieldPointItemViewModel?> items = ImmutableList<VectorFieldPointItemViewModel?>.Empty;

        public object? SelectedTarget { get => selectedTarget; private set => Set(ref selectedTarget, value); }

        public bool CanAddPoint => Effect.Points.Count < VectorFieldWarpCustomEffect.MaxPoints;

        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
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

            selectCommand = new ActionCommand(_ => true, arg => HandleSelect(arg));

            AddPointCommand = new ActionCommand(_ => CanAddPoint, _ => AddPoint());
            RemovePointCommand = new ActionCommand(_ => selectedItem != null, _ => RemovePoint());
            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => BeginEdit?.Invoke(this, EventArgs.Empty));
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => EndEdit?.Invoke(this, EventArgs.Empty));

            RebuildViewModels();
        }

        void AddPoint()
        {
            var index = Effect.Points.Count;
            var x = (index % 5 - 2) * 100d;
            var y = (index / 5) * 100d;
            var points = Effect.Points.Add(VectorFieldPoint.Create(x, y));
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(points, points.Count - 1);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        void RemovePoint()
        {
            if (selectedItem is null)
                return;
            var index = allViewModels.IndexOf(selectedItem);
            var points = Effect.Points.Remove(selectedItem.Model);
            var selectedIndex = points.Count == 0 ? -1 : Math.Min(index, points.Count - 1);
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(points, selectedIndex);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        void CommitStructuralChange(ImmutableList<VectorFieldPoint> points, int selectedIndex)
        {
            var clones = points.Select(Clone).ToImmutableList();
            for (var index = 0; index < clones.Count; index++)
                clones[index].IsSelected = index == selectedIndex;
            ItemProperties[0].SetValue(clones);
        }

        static VectorFieldPoint Clone(VectorFieldPoint point)
        {
            return JsonConvert.DeserializeObject<VectorFieldPoint>(JsonConvert.SerializeObject(point))
                ?? VectorFieldPoint.Create(0, 0);
        }

        void HandleSelect(object? arg)
        {
            if (arg is not VectorFieldPointItemViewModel vm)
                return;

            isMutatingSelection = true;
            try
            {
                foreach (var item in allViewModels)
                    item.IsSelected = ReferenceEquals(item, vm);
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
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
            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                var vm = existingByModel.TryGetValue(point, out var existing)
                    ? existing
                    : new VectorFieldPointItemViewModel(point, index, selectCommand);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.PositionChanged -= Item_PositionChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
                newVm.PositionChanged += Item_PositionChanged;
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

        readonly struct GridLayout(int columns, int rows, VectorFieldPointItemViewModel?[] cells)
        {
            public int Columns { get; } = columns;
            public int Rows { get; } = rows;
            public VectorFieldPointItemViewModel?[] Cells { get; } = cells;
        }

        static GridLayout ComputeGridLayout(ImmutableList<VectorFieldPointItemViewModel> viewModels)
        {
            if (viewModels.Count == 0)
                return new GridLayout(1, 1, new VectorFieldPointItemViewModel?[1]);

            var xs = viewModels.Select(v => v.Model.X.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();
            var ys = viewModels.Select(v => v.Model.Y.Values.FirstOrDefault()?.Value ?? 0.0).ToArray();

            var bboxW = xs.Max() - xs.Min();
            var bboxH = ys.Max() - ys.Min();
            var tolerance = Math.Max(Math.Max(bboxW, bboxH) * 0.1, 1e-3);

            var colsAssign = ClusterCoordinates(xs, tolerance, out var colCount);
            var rowsAssign = ClusterCoordinates(ys, tolerance, out var rowCount);

            var cells = new VectorFieldPointItemViewModel?[rowCount * colCount];
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

                var expanded = new VectorFieldPointItemViewModel?[(rowCount + 1) * colCount];
                Array.Copy(cells, expanded, cells.Length);
                rowCount++;
                cells = expanded;
                var newSlot = FindNearestEmptyCell(cells, p.Row, p.Col, rowCount, colCount);
                if (newSlot >= 0)
                    cells[newSlot] = viewModels[p.Index];
            }

            return new GridLayout(colCount, rowCount, cells);
        }

        static int FindNearestEmptyCell(VectorFieldPointItemViewModel?[] cells, int row, int col, int rowCount, int colCount)
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

        void Item_PositionChanged(object? sender, EventArgs e)
        {
            RefreshGridLayout();
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
                    item.PositionChanged -= Item_PositionChanged;
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
