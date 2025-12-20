using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Documents;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class DropFileBehavior : Behavior<FrameworkElement>
    {
        public string Path
        {
            get { return (string)GetValue(PathProperty); }
            set { SetValue(PathProperty, value); }
        }
        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(nameof(Path), typeof(string), typeof(DropFileBehavior), new PropertyMetadata(string.Empty));

        public bool IsEnabled
        {
            get { return (bool)GetValue(IsEnabledProperty); }
            set { SetValue(IsEnabledProperty, value); }
        }
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(DropFileBehavior), new PropertyMetadata(true));

        public bool IsDragOverHighlightingEnabled
        {
            get { return (bool)GetValue(IsDragOverHighlightingEnabledProperty); }
            set { SetValue(IsDragOverHighlightingEnabledProperty, value); }
        }
        public static readonly DependencyProperty IsDragOverHighlightingEnabledProperty =
            DependencyProperty.Register(nameof(IsDragOverHighlightingEnabled), typeof(bool), typeof(DropFileBehavior), new PropertyMetadata(true));

        AdornerLayer? layer;
        DropHighlightAdorner? adorner;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.AllowDrop = true;
            AssociatedObject.DragOver += AssociatedObject_DragOver;
            AssociatedObject.Drop += AssociatedObject_Drop;
            AssociatedObject.DragLeave += AssociatedObject_DragLeave;
            AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.AllowDrop = false;
            AssociatedObject.DragOver -= AssociatedObject_DragOver;
            AssociatedObject.Drop -= AssociatedObject_Drop;
            AssociatedObject.DragLeave -= AssociatedObject_DragLeave;
            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
        }

        private void AssociatedObject_DragOver(object sender, DragEventArgs e)
        {
            if (!IsEnabled)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                EnsureAdorner();
                if (e.AllowedEffects.HasFlag(DragDropEffects.Move))
                    e.Effects = DragDropEffects.Move;
                else
                    e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void AssociatedObject_DragLeave(object sender, DragEventArgs e)
        {
            RemoveAdorner();
        }

        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
        {
            RemoveAdorner();
        }

        private void AssociatedObject_Drop(object sender, DragEventArgs e)
        {
            if(!IsEnabled)
                return;
            RemoveAdorner();
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
                    return;

                // 移動可能なものだけに絞る
                paths = [..paths.Where(path => ShellFileOperation.CanMove(path, Path))];

                // PathがDependencyPropertyでUIスレッドからしか取得できないため、ローカル変数にコピーしておく
                var path = Path;
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (e.Effects.HasFlag(DragDropEffects.Move))
                            ShellFileOperation.MoveFiles(paths, path);
                        else if (e.Effects is DragDropEffects.Copy)
                            ShellFileOperation.CopyFiles(paths, path);
                    }
                    catch (Exception e)
                    {
                        Log.Default.Write("ファイルのドロップ処理中に例外が発生しました。", e);
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        void EnsureAdorner()
        {
            if (adorner != null || !IsDragOverHighlightingEnabled)
                return;
            layer ??= AdornerLayer.GetAdornerLayer(AssociatedObject);
            if (layer is null)
                return;
            adorner = new DropHighlightAdorner(AssociatedObject);
            layer.Add(adorner);
        }
        void RemoveAdorner()
        {
            if (adorner is null || layer is null)
                return;
            layer.Remove(adorner);
            adorner = null;
        }
    }

}
