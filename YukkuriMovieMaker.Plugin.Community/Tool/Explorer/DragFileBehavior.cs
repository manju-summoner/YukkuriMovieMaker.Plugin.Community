using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class DragFileBehavior : Behavior<ListBoxItem>
    {
        const string PreferredDropEffectFormat = "Preferred DropEffect";

        public IEnumerable<string> Paths
        {
            get { return (IEnumerable<string>)GetValue(PathsProperty); }
            set { SetValue(PathsProperty, value); }
        }
        public static readonly DependencyProperty PathsProperty =
            DependencyProperty.Register(nameof(Paths), typeof(IEnumerable<string>), typeof(DragFileBehavior), new PropertyMetadata(Enumerable.Empty<string>()));

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += AssociatedObject_PreviewMouseDown;
            AssociatedObject.PreviewMouseMove += AssociatedObject_PreviewMouseMove;
            AssociatedObject.PreviewMouseUp += AssociatedObject_PreviewMouseUp;
            AssociatedObject.LostMouseCapture += AssociatedObject_LostMouseCapture;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseDown -= AssociatedObject_PreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= AssociatedObject_PreviewMouseMove;
            AssociatedObject.PreviewMouseUp -= AssociatedObject_PreviewMouseUp;
            AssociatedObject.LostMouseCapture -= AssociatedObject_LostMouseCapture;
        }

        bool isDragStarted = false;
        private void AssociatedObject_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isDragStarted = true;

            // ドラッグ処理用に、複数アイテムを選択している状態でのマウスダウンイベントを握りつぶす
            // これが無いと、複数選択後、ドラッグを開始したタイミングでアイテムの選択状態が解除される
            e.Handled = ItemsControl.ItemsControlFromItemContainer(AssociatedObject) is ListBox listBox
                && e.ChangedButton is MouseButton.Left
                && AssociatedObject.IsSelected
                && listBox.SelectedItems.Count > 1
                && Keyboard.Modifiers is ModifierKeys.None;
        }

        private void AssociatedObject_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton is not MouseButtonState.Pressed || !isDragStarted)
                return;

            // PreviewMouseDownではなくここでCaptureする。
            // PreviewMouseDownでCapture＆Handled=trueにすると、ListBoxItemの選択処理が機能しなくなるため。
            // このタイミングでCaptureすることで、ListBoxItem外へのドラッグを検出できるようになる。
            if (!AssociatedObject.IsMouseCaptured)
                AssociatedObject.CaptureMouse();

            var position = e.GetPosition(AssociatedObject);
            var bounds = new Rect(0, 0, AssociatedObject.ActualWidth, AssociatedObject.ActualHeight);
            if (bounds.Contains(position))
                return;

            try
            {
                var dataObject = new DataObject(DataFormats.FileDrop, Paths.ToArray());
                var moveBytes = BitConverter.GetBytes((int)DragDropEffects.Move);
                dataObject.SetData(PreferredDropEffectFormat, new MemoryStream(moveBytes));
                DragDrop.DoDragDrop(AssociatedObject, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            }
            finally
            {
                AssociatedObject.ReleaseMouseCapture();
            }
        }

        private void AssociatedObject_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton is not MouseButton.Left)
                return;
            AssociatedObject.ReleaseMouseCapture();
        }

        private void AssociatedObject_LostMouseCapture(object sender, MouseEventArgs e)
        {
            isDragStarted = false;
        }
    }

}
