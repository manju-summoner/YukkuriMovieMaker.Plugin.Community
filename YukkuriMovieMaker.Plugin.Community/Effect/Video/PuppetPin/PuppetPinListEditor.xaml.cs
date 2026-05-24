using System;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    public partial class PuppetPinListEditor : UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public ItemProperty[]? ItemProperties { get; internal set; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public PuppetPinListEditor()
        {
            InitializeComponent();
            DataContextChanged += PuppetPinListEditor_DataContextChanged;
        }

        void PuppetPinListEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PuppetPinListEditorViewModel oldVm)
            {
                oldVm.BeginEdit -= OnBeginEdit;
                oldVm.EndEdit -= OnEndEdit;
                oldVm.Dispose();
            }
            if (e.NewValue is PuppetPinListEditorViewModel newVm)
            {
                newVm.BeginEdit += OnBeginEdit;
                newVm.EndEdit += OnEndEdit;
            }
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo frame)
        {
            if (DataContext is PuppetPinListEditorViewModel vm)
            {
                vm.SetEditorInfo(frame);
            }
        }
    }
}
