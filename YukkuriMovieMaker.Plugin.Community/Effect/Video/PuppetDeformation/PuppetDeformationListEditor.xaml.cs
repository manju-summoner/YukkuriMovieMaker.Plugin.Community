using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public partial class PuppetPinListEditor : UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public ItemProperty[]? ItemProperties { get; internal set; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        //ジョイント/ピン選択中のプロパティエディタの高さ。選択解除時に同じ高さを確保する
        double reservedEditorHeight;

        public PuppetPinListEditor()
        {
            InitializeComponent();
            DataContextChanged += PuppetPinListEditor_DataContextChanged;
        }

        void PuppetPinListEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PuppetDeformationListEditorViewModel oldVm)
            {
                oldVm.BeginEdit -= OnBeginEdit;
                oldVm.EndEdit -= OnEndEdit;
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                oldVm.Dispose();
            }
            if (e.NewValue is PuppetDeformationListEditorViewModel newVm)
            {
                newVm.BeginEdit += OnBeginEdit;
                newVm.EndEdit += OnEndEdit;
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                UpdateEditorAreaReservation(newVm);
            }
        }

        void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.SelectedTarget)
                && sender is PuppetDeformationListEditorViewModel vm)
            {
                UpdateEditorAreaReservation(vm);
            }
        }

        void EditorArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //選択中のみ自然な高さを記録する。MinHeight適用中(未選択)の高さは記録対象外
            if (DataContext is PuppetDeformationListEditorViewModel vm
                && !vm.HasNoSelection
                && editorArea.MinHeight == 0
                && e.NewSize.Height > 0)
            {
                reservedEditorHeight = e.NewSize.Height;
            }
        }

        void UpdateEditorAreaReservation(PuppetDeformationListEditorViewModel vm)
        {
            //未選択時は直前の選択時と同じ高さを確保し、外側ScrollViewerのスクロール位置ズレを防ぐ
            editorArea.MinHeight = vm.HasNoSelection ? reservedEditorHeight : 0;
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo frame)
        {
            if (DataContext is PuppetDeformationListEditorViewModel vm)
            {
                vm.SetEditorInfo(frame);
            }
        }

        void CanvasResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            PuppetDeformationEditorSettings.Default.CanvasHeight += e.VerticalChange;
        }

        void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            //ズームと表示位置を全体表示に戻す
            pinCanvas.ResetView();
        }
    }
}
