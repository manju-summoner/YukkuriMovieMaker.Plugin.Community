using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class SizeChangedTrigger : TriggerBase<FrameworkElement>
    {

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SizeChanged += OnSizeChanged;
        }
        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            InvokeActions(e);
        }
    }
}
