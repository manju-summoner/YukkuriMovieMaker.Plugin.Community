using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    public partial class VectorFieldPointListEditor : UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public ItemProperty[]? ItemProperties { get; internal set; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        double reservedEditorHeight;

        public VectorFieldPointListEditor()
        {
            InitializeComponent();
            DataContextChanged += VectorFieldPointListEditor_DataContextChanged;
        }

        void VectorFieldPointListEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is VectorFieldPointListEditorViewModel oldViewModel)
            {
                oldViewModel.BeginEdit -= OnBeginEdit;
                oldViewModel.EndEdit -= OnEndEdit;
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                oldViewModel.Dispose();
            }
            if (e.NewValue is VectorFieldPointListEditorViewModel newViewModel)
            {
                newViewModel.BeginEdit += OnBeginEdit;
                newViewModel.EndEdit += OnEndEdit;
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateEditorAreaReservation(newViewModel);
            }
        }

        void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VectorFieldPointListEditorViewModel.SelectedTarget)
                && sender is VectorFieldPointListEditorViewModel viewModel)
            {
                UpdateEditorAreaReservation(viewModel);
            }
        }

        void EditorArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is VectorFieldPointListEditorViewModel viewModel
                && !viewModel.HasNoSelection
                && editorArea.MinHeight == 0
                && e.NewSize.Height > 0)
            {
                reservedEditorHeight = e.NewSize.Height;
            }
        }

        void UpdateEditorAreaReservation(VectorFieldPointListEditorViewModel viewModel)
        {
            editorArea.MinHeight = viewModel.HasNoSelection ? reservedEditorHeight : 0;
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo? info)
        {
            if (DataContext is VectorFieldPointListEditorViewModel viewModel)
                viewModel.SetEditorInfo(info);
        }

        void CanvasResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            VectorFieldWarpEditorSettings.Default.CanvasHeight += e.VerticalChange;
        }

        void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            pointCanvas.ResetView();
        }
    }
}
