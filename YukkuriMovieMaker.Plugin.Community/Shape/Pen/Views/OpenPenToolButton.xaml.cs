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
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
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

            var owner = ItemProperties[0].PropertyOwner;
            var layers = owner.GetType().GetProperty(nameof(PenShapeParameter.Layers))?.GetValue(owner) as ImmutableList<PenLayer> ?? [];
            var strokes = ItemProperties[0].GetValue<ImmutableList<SerializableStroke>>() ?? [];
            using var vm = new PenEditorViewModel(editorInfo, layers, strokes);
            var window = new PenEditorWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = vm,
            };
            window.ShowDialog();

            var editedLayers = vm.Layers;
            var mirror = vm.CreateStrokeMirror();
            foreach (var property in ItemProperties)
            {
                var layersProperty = property.PropertyOwner.GetType().GetProperty(nameof(PenShapeParameter.Layers));
                if (layersProperty is not null)
                    property.SetValue(layersProperty, CloneLayers(editedLayers));
                property.SetValue(mirror);
            }

            foreach (var property in ItemProperties)
                property.PropertyOwner.GetType().GetProperty("IsEditing")?.SetValue(property.PropertyOwner, false);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
        public void SetEditorInfo(IEditorInfo? info)
        {
            editorInfo = info;
        }

        static ImmutableList<PenLayer> CloneLayers(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            foreach (var layer in layers)
                builder.Add(layer.Clone(layer.Id));
            return builder.ToImmutable();
        }
    }
}
