using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class MediaPositionMarkerBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty MediaProperty =
            DependencyProperty.Register(nameof(Media), typeof(MediaElement), typeof(MediaPositionMarkerBehavior));

        bool isRendering;

        public MediaElement? Media
        {
            get => (MediaElement?)GetValue(MediaProperty);
            set => SetValue(MediaProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Visibility = Visibility.Hidden;
            AssociatedObject.Loaded += AssociatedObject_Loaded;
            AssociatedObject.Unloaded += AssociatedObject_Unloaded;
            if (AssociatedObject.IsLoaded)
                StartRendering();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
            StopRendering();
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e) => StartRendering();

        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e) => StopRendering();

        void StartRendering()
        {
            if (isRendering)
                return;
            isRendering = true;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        void StopRendering()
        {
            if (!isRendering)
                return;
            isRendering = false;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            var marker = AssociatedObject;

            if (Media?.Clock?.CurrentTime is not TimeSpan position ||
                marker.DataContext is not IExplorerItemViewModel item ||
                VisualTreeHelper.GetParent(marker) is not FrameworkElement container ||
                container.ActualWidth <= 0)
            {
                marker.Visibility = Visibility.Hidden;
                return;
            }

            item.PreviewPosition = position;

            if (item.AudioPreview is not AudioPreview preview || preview.Length <= TimeSpan.Zero)
            {
                marker.Visibility = Visibility.Hidden;
                return;
            }

            var progress = Math.Clamp((position - preview.Start) / preview.Length, 0.0, 1.0);
            if (marker.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                marker.RenderTransform = transform;
            }
            transform.X = progress * Math.Max(0, container.ActualWidth - marker.ActualWidth);
            marker.Visibility = Visibility.Visible;
        }
    }
}
