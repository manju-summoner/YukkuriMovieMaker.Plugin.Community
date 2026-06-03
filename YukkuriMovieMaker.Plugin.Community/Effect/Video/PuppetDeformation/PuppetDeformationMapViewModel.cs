using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public sealed class PuppetDeformationMapViewModel : Bindable, IDisposable
    {
        bool disposed;
        ITimelineSourceAndDevices? source;
        Guid currentSceneId;
        int isUpdating;

        ImageSource? currentImage;
        ImmutableList<PuppetDeformationMapPinViewModel> pinViewModels = ImmutableList<PuppetDeformationMapPinViewModel>.Empty;

        double translateX;
        double translateY;
        double scale = 1.0;
        bool transformReady;

        readonly PuppetDeformationEffect effect;

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

        public PuppetDeformationMapViewModel(PuppetDeformationEffect effect)
        {
            this.effect = effect;
            RebuildPinViewModels();
            effect.PropertyChanged += Effect_PropertyChanged;
            PuppetDeformationFrameService.FrameUpdated += FrameService_FrameUpdated;
            ScheduleUpdateFrame();
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationEffect.Pins))
                Application.Current?.Dispatcher.InvokeAsync(RebuildPinViewModels);
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

        void FrameService_FrameUpdated(object? sender, EventArgs e)
        {
            ScheduleUpdateFrame();
        }

        void ScheduleUpdateFrame()
        {
            if (Interlocked.CompareExchange(ref isUpdating, 1, 0) == 0)
                Application.Current?.Dispatcher.InvokeAsync(UpdateFrame);
        }

        void UpdateFrame()
        {
            try
            {
                if (disposed || PuppetDeformationFrameService.IsInternalRendering) return;

                var scene = PuppetDeformationFrameService.CurrentScene;
                if (scene is null) return;

                if (currentSceneId != scene.ID || source is null)
                {
                    source?.Dispose();
                    source = null;
                    currentSceneId = scene.ID;
                    if (scene.TryCreateVideoSource(out var newSource))
                        source = newSource;
                }

                if (source is null) return;

                PuppetDeformationFrameService.IsInternalRendering = true;
                try
                {
                    int frame = PuppetDeformationFrameService.CurrentFrame;
                    int fps = PuppetDeformationFrameService.CurrentFPS;
                    if (fps > 0)
                    {
                        var time = TimeSpan.FromTicks((long)frame * 10000000 / fps);
                        source.Update(time, TimelineSourceUsage.Paused);
                        var bmp = source.RenderBitmapSource();
                        bmp.Freeze();
                        CurrentImage = bmp;
                        UpdatePinPositions();
                    }
                }
                finally
                {
                    PuppetDeformationFrameService.IsInternalRendering = false;
                }
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref isUpdating, 0);
            }
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

            var frame = PuppetDeformationFrameService.CurrentFrame;
            var fps = PuppetDeformationFrameService.CurrentFPS;
            var length = 1;
            var safeFps = fps > 0 ? fps : 1;

            var vms = pinViewModels;
            var i = 0;
            while (i < vms.Count)
            {
                var restVm = vms[i];
                var pin = restVm.Model;

                var rx = pin.RestX.GetValue(frame, length, safeFps);
                var ry = pin.RestY.GetValue(frame, length, safeFps);

                restVm.CanvasX = centerX + rx * scale;
                restVm.CanvasY = centerY + ry * scale;
                restVm.IsEnabled = pin.IsEnabled;
                restVm.IsSelected = pin.IsRestSelected;
                i++;

                if (i < vms.Count)
                {
                    var offsetVm = vms[i];
                    var ox = pin.OffsetX.GetValue(frame, length, safeFps);
                    var oy = pin.OffsetY.GetValue(frame, length, safeFps);

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
            PuppetDeformationFrameService.FrameUpdated -= FrameService_FrameUpdated;
            effect.PropertyChanged -= Effect_PropertyChanged;
            source?.Dispose();
            source = null;
        }
    }
}
