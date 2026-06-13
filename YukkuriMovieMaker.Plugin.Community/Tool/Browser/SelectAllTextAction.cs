using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class SelectAllTextAction : TriggerAction<TextBox>
    {
        protected override void Invoke(object parameter)
        {
            // GotFocusのタイミングで実行しても全選択されないため、Dispatcher経由で遅延実行する
            Dispatcher.BeginInvoke(() => {
                AssociatedObject.SelectAll();
            });
        }
    }
}
