using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public partial class ToolView : UserControl
    {
        const string PreferredDropEffectFormat = "Preferred DropEffect";
        bool isDragStarted;

        public ToolView()
        {
            InitializeComponent();
        }

        void RecordList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragStarted = FindListViewItem(e.OriginalSource as DependencyObject) is not null;
        }

        void RecordList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragStarted = false;
        }

        void RecordList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragStarted || e.LeftButton != MouseButtonState.Pressed)
                return;

            if (DataContext is not ToolViewModel vm)
                return;

            var paths = vm.GetSelectedRecordPathsForDragDrop();
            if (paths.Length == 0)
                return;

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                    return;
            }

            isDragStarted = false;

            var dataObject = new DataObject(DataFormats.FileDrop, paths);
            var moveBytes = BitConverter.GetBytes((int)DragDropEffects.Move);
            dataObject.SetData(PreferredDropEffectFormat, new MemoryStream(moveBytes));
            DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
        }

        static ListViewItem? FindListViewItem(DependencyObject? current)
        {
            while (current is not null)
            {
                if (current is ListViewItem item)
                    return item;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}

