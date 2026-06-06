using System;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public partial class PuppetPinListEditor : UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public ItemProperty[]? ItemProperties { get; internal set; }

        IEditorInfo? editorInfo;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

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
                oldVm.Dispose();
            }
            if (e.NewValue is PuppetDeformationListEditorViewModel newVm)
            {
                newVm.BeginEdit += OnBeginEdit;
                newVm.EndEdit += OnEndEdit;
                if (editorInfo is not null)
                    newVm.SetEditorInfo(editorInfo);
            }
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo frame)
        {
            editorInfo = frame;
            if (DataContext is PuppetDeformationListEditorViewModel vm)
                vm.SetEditorInfo(frame);
        }
    }
}
