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
                oldViewModel.Dispose();
            }
            if (e.NewValue is VectorFieldPointListEditorViewModel newViewModel)
            {
                newViewModel.BeginEdit += OnBeginEdit;
                newViewModel.EndEdit += OnEndEdit;
            }
        }

        void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
        void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

        public void SetEditorInfo(IEditorInfo info)
        {
            if (DataContext is VectorFieldPointListEditorViewModel viewModel)
                viewModel.SetEditorInfo(info);
        }
    }
}
