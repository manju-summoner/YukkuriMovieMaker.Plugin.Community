using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public partial class PuppetDeformationMapView : UserControl
    {
        bool isDragging;
        Point lastMousePosition;
        bool hasFittedOnce;

        public PuppetDeformationMapView()
        {
            InitializeComponent();
            SizeChanged += PuppetDeformationMapView_SizeChanged;
            DataContextChanged += PuppetDeformationMapView_DataContextChanged;
        }

        void PuppetDeformationMapView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PuppetDeformationMapViewModel oldVm)
                oldVm.RenderSizeChanged -= ViewModel_RenderSizeChanged;
            if (e.NewValue is PuppetDeformationMapViewModel newVm)
            {
                hasFittedOnce = false;
                newVm.RenderSizeChanged += ViewModel_RenderSizeChanged;
                SyncCanvasSize();
                TryFitToView();
            }
        }

        void PuppetDeformationMapView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SyncCanvasSize();
            TryFitToView();
        }

        void ViewModel_RenderSizeChanged(object? sender, EventArgs e)
        {
            SyncCanvasSize();
            TryFitToView();
        }

        void TryFitToView()
        {
            if (hasFittedOnce) return;
            if (DataContext is not PuppetDeformationMapViewModel vm) return;
            if (vm.CurrentImage is null) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            hasFittedOnce = true;
            FitToView();
        }

        void SyncCanvasSize()
        {
            if (DataContext is not PuppetDeformationMapViewModel vm) return;
            if (vm.CurrentImage is null) return;

            PreviewImage.Width = vm.CurrentImage.Width;
            PreviewImage.Height = vm.CurrentImage.Height;

            RootCanvas.Width = ActualWidth;
            RootCanvas.Height = ActualHeight;

            PinCanvas.Width = ActualWidth;
            PinCanvas.Height = ActualHeight;
        }

        void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var position = e.GetPosition(PreviewImage);
            var zoom = e.Delta > 0 ? 1.2 : 1.0 / 1.2;

            var oldScaleX = ImageScale.ScaleX;
            var oldScaleY = ImageScale.ScaleY;
            var newScaleX = oldScaleX * zoom;
            var newScaleY = oldScaleY * zoom;

            if (newScaleX < 0.05 || newScaleX > 100.0) return;

            ImageScale.ScaleX = newScaleX;
            ImageScale.ScaleY = newScaleY;

            ImageTranslate.X -= position.X * (newScaleX - oldScaleX);
            ImageTranslate.Y -= position.Y * (newScaleY - oldScaleY);

            if (DataContext is PuppetDeformationMapViewModel vm)
                vm.UpdateTransform(ImageTranslate.X, ImageTranslate.Y, ImageScale.ScaleX);
        }

        void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is PuppetDeformationMapPinViewModel)
                return;

            isDragging = true;
            lastMousePosition = e.GetPosition(this);
            CaptureMouse();
        }

        void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            ReleaseMouseCapture();
        }

        void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            var position = e.GetPosition(this);
            var deltaX = position.X - lastMousePosition.X;
            var deltaY = position.Y - lastMousePosition.Y;

            ImageTranslate.X += deltaX;
            ImageTranslate.Y += deltaY;

            lastMousePosition = position;

            if (DataContext is PuppetDeformationMapViewModel vm)
                vm.UpdateTransform(ImageTranslate.X, ImageTranslate.Y, ImageScale.ScaleX);
        }

        void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                ReleaseMouseCapture();
            }
        }

        void Pin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PuppetDeformationMapPinViewModel pinVm)
            {
                if (DataContext is PuppetDeformationMapViewModel vm)
                    vm.SelectPin(pinVm, Keyboard.Modifiers);
                e.Handled = true;
            }
        }

        public void FitToView()
        {
            if (DataContext is not PuppetDeformationMapViewModel vm) return;
            if (vm.CurrentImage is null) return;

            var imgW = vm.CurrentImage.Width;
            var imgH = vm.CurrentImage.Height;
            var viewW = ActualWidth;
            var viewH = ActualHeight;

            if (viewW <= 0 || viewH <= 0 || imgW <= 0 || imgH <= 0) return;

            var scale = Math.Min(viewW / imgW, viewH / imgH) * 0.9;

            ImageScale.ScaleX = scale;
            ImageScale.ScaleY = scale;
            ImageTranslate.X = (viewW - imgW * scale) / 2.0;
            ImageTranslate.Y = (viewH - imgH * scale) / 2.0;

            vm.UpdateTransform(ImageTranslate.X, ImageTranslate.Y, scale);
        }
    }
}
