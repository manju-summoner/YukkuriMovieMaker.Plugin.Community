using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class InkCanvasEraser
    {
        public static double GetSize(DependencyObject obj)
        {
            return (double)obj.GetValue(SizeProperty);
        }

        public static void SetSize(DependencyObject obj, double value)
        {
            obj.SetValue(SizeProperty, value);
        }

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.RegisterAttached("Size", typeof(double), typeof(InkCanvasEraser), new PropertyMetadata(0d, 
                (s, a) => 
                {
                    if (s is not InkCanvas inkCanvas) 
                        return;
                    inkCanvas.EraserShape = new EllipseStylusShape((double)a.NewValue, (double)a.NewValue);
                }));


    }
}
