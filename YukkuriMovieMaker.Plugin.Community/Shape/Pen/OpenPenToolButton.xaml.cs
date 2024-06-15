using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    /// <summary>
    /// OpenPenToolButton.xaml の相互作用ロジック
    /// </summary>
    public partial class OpenPenToolButton : UserControl, IPropertyEditorControl2
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
        IEditorInfo? editorInfo;

        public ItemProperty[]? ItemProperties { get; set; }

        public OpenPenToolButton()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(editorInfo is null)
                throw new InvalidOperationException("EditorInfo is not set.");
            if(ItemProperties is null)
                throw new InvalidOperationException("ItemProperties is not set.");
            BeginEdit?.Invoke(this, EventArgs.Empty);

            //編集中は画像を非表示にするために編集中フラグを立てる。
            //IsEditing決め打ち。他で使う予定もないのでとりあえずこれで。
            foreach(var property in ItemProperties)
                property.PropertyOwner.GetType().GetProperty("IsEditing")?.SetValue(property.PropertyOwner, true);

            var strokes = ItemProperties[0].GetValue<ImmutableList<SerializableStroke>>() ?? [];
            using var vm = new PenToolViewModel(editorInfo, strokes.Select(x=>x.ToStroke()));
            var window = new PenToolView
            {
                Owner = Window.GetWindow(this),
                DataContext = vm,
            };
            window.ShowDialog();

            foreach (var property in ItemProperties)
            {
                var clones = 
                    vm.Strokes
                    .Select(x => x.Clone())
                    .Select(x => new SerializableStroke(x))
                    .ToImmutableList();
                property.SetValue(clones);

            }

            foreach (var property in ItemProperties)
                property.PropertyOwner.GetType().GetProperty("IsEditing")?.SetValue(property.PropertyOwner, false);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
        }
    }
}
