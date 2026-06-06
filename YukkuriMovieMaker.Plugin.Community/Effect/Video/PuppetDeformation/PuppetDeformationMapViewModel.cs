using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public sealed class PuppetDeformationMapViewModel : Bindable, IDisposable
    {
        bool disposed;

        ImageSource? currentImage;
        ImmutableList<PuppetDeformationMapPinViewModel> pinViewModels = ImmutableList<PuppetDeformationMapPinViewModel>.Empty;

        double translateX;
        double translateY;
        double scale = 1.0;
        bool transformReady;

        readonly PuppetDeformationEffect effect;
        readonly IEditorInfo editorInfo;
        readonly Dispatcher dispatcher;

        public ImageSource? CurrentImage
        {
            get => currentImage;
            private set
            {
                if (Set(ref currentImage, value))
                    RenderSizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ImmutableList<PuppetDeformationMapPinViewModel> PinViewModels
        {
            get => pinViewModels;
            private set => Set(ref pinViewModels, value);
        }

        public bool IsTransformReady
        {
            get => transformReady;
            private set => Set(ref transformReady, value);
        }

        public string Title => Texts.PuppetDeformationMapWindowTitle;

        public event EventHandler? RenderSizeChanged;

        public PuppetDeformationMapViewModel(PuppetDeformationEffect effect, IEditorInfo editorInfo)
        {
            this.effect = effect;
            this.editorInfo = editorInfo;
            this.dispatcher = Dispatcher.CurrentDispatcher;
            RebuildPinViewModels();
            effect.PropertyChanged += Effect_PropertyChanged;
            effect.FrameCaptured += Effect_FrameCaptured;
            effect.RequestCapture();
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationEffect.Pins))
                dispatcher.InvokeAsync(RebuildPinViewModels);
            effect.RequestCapture();
        }

        void RebuildPinViewModels()
        {
            var pins = effect.Pins;
            var builder = ImmutableList.CreateBuilder<PuppetDeformationMapPinViewModel>();
            foreach (var pin in pins)
            {
                builder.Add(new PuppetDeformationMapPinViewModel(pin, isOffset: false));
                builder.Add(new PuppetDeformationMapPinViewModel(pin, isOffset: true));
            }
            PinViewModels = builder.ToImmutable();
            UpdatePinPositions();
        }

        void Effect_FrameCaptured(object? sender, FrameCapturedEventArgs e)
        {
            var pixels = e.Pixels;
            var width = e.Width;
            var height = e.Height;

            effect.RequestCapture();

            dispatcher.InvokeAsync(() =>
            {
                if (disposed) return;
                var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                wb.Freeze();
                CurrentImage = wb;
                UpdatePinPositions();
            });
        }

        public void UpdateTransform(double tx, double ty, double sc)
        {
            translateX = tx;
            translateY = ty;
            scale = sc;
            IsTransformReady = true;
            UpdatePinPositions();
        }

        void UpdatePinPositions()
        {
            if (currentImage is null || !transformReady) return;

            var imgW = currentImage.Width;
            var imgH = currentImage.Height;
            var centerX = translateX + imgW * scale / 2.0;
            var centerY = translateY + imgH * scale / 2.0;

            var itemFrame = editorInfo.ItemPosition.Frame;
            var itemLength = editorInfo.ItemDuration.Frame;
            var fps = editorInfo.VideoInfo.FPS;
            var safeFps = fps > 0 ? fps : 1;

            var vms = pinViewModels;
            var i = 0;
            while (i < vms.Count)
            {
                var restVm = vms[i];
                var pin = restVm.Model;

                var rx = pin.RestX.GetValue(itemFrame, itemLength, safeFps);
                var ry = pin.RestY.GetValue(itemFrame, itemLength, safeFps);

                restVm.CanvasX = centerX + rx * scale;
                restVm.CanvasY = centerY + ry * scale;
                restVm.IsEnabled = pin.IsEnabled;
                restVm.IsSelected = pin.IsRestSelected;
                i++;

                if (i < vms.Count)
                {
                    var offsetVm = vms[i];
                    var ox = pin.OffsetX.GetValue(itemFrame, itemLength, safeFps);
                    var oy = pin.OffsetY.GetValue(itemFrame, itemLength, safeFps);

                    offsetVm.CanvasX = centerX + (rx + ox) * scale;
                    offsetVm.CanvasY = centerY + (ry + oy) * scale;
                    offsetVm.IsEnabled = pin.IsEnabled;
                    offsetVm.IsSelected = pin.IsOffsetSelected;
                    i++;
                }
            }
        }

        public void SelectPin(PuppetDeformationMapPinViewModel pinVm, ModifierKeys modifiers)
        {
            var pin = pinVm.Model;

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                if (pinVm.IsOffset)
                    pin.IsOffsetSelected = !pin.IsOffsetSelected;
                else
                    pin.IsRestSelected = !pin.IsRestSelected;
            }
            else
            {
                foreach (var p in effect.Pins)
                {
                    p.IsRestSelected = false;
                    p.IsOffsetSelected = false;
                }
                if (pinVm.IsOffset)
                    pin.IsOffsetSelected = true;
                else
                    pin.IsRestSelected = true;
            }

            UpdatePinPositions();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            effect.FrameCaptured -= Effect_FrameCaptured;
            effect.PropertyChanged -= Effect_PropertyChanged;
        }
    }
}
