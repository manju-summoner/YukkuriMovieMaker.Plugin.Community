using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        //ジョイント/ピン選択中のプロパティエディタの高さ。選択解除時に同じ高さを確保する。
        //ピンとボーンでは編集項目の数が違い高さが一致しないため、モードごとに分けて覚える
        readonly Dictionary<PuppetDeformationEditMode, double> reservedEditorHeights = [];

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
            //モードだけ切り替わって選択の有無が変わらない（両モードとも未選択）ときはSelectedTargetが通知されないため、EditModeでも更新する
            if ((e.PropertyName == nameof(PuppetDeformationListEditorViewModel.SelectedTarget)
                || e.PropertyName == nameof(PuppetDeformationListEditorViewModel.EditMode))
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
                reservedEditorHeights[vm.EditMode] = e.NewSize.Height;
            }
        }

        void UpdateEditorAreaReservation(PuppetDeformationListEditorViewModel vm)
        {
            //未選択時は同じモードで直前に選択していたときと同じ高さを確保し、外側ScrollViewerのスクロール位置ズレを防ぐ
            editorArea.MinHeight = vm.HasNoSelection ? GetReservedEditorHeight(vm.EditMode) : 0;
        }

        double GetReservedEditorHeight(PuppetDeformationEditMode mode)
        {
            if (reservedEditorHeights.TryGetValue(mode, out var height))
                return height;
            //そのモードでまだ一度も選択していないときは、他のモードの高さで代用する
            return reservedEditorHeights.Values.DefaultIfEmpty(0).Max();
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo? frame)
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
