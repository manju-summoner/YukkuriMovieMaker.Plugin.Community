using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class ExplorerAudioPreviewViewModel(string path, TimeSpan windowLength) : Bindable
    {
        readonly string path = path;
        CancellationTokenSource? loadCts;
        AudioPreview? preview;
        TimeSpan position;
        TimeSpan windowLength = windowLength;

        public Geometry? Waveform
        {
            get
            {
                StartLoad();
                return preview?.Waveform;
            }
        }

        public string? DurationText
        {
            get
            {
                StartLoad();
                return preview?.DurationText;
            }
        }

        public TimeSpan Position
        {
            get => position;
            set
            {
                if (position == value)
                    return;
                var previousWindowStart = WindowStart;
                position = value;
                if (WindowStart == previousWindowStart)
                    return;
                Cancel();
                StartLoad();
            }
        }

        public double? GetProgress()
        {
            if (preview is null || preview.Length <= TimeSpan.Zero)
                return null;
            return Math.Clamp((position - preview.Start) / preview.Length, 0.0, 1.0);
        }

        public void SetWindowLength(TimeSpan length)
        {
            if (windowLength == length || length <= TimeSpan.Zero)
                return;
            windowLength = length;
            Clear();
        }

        public void Clear()
        {
            Cancel();
            preview = null;
            OnPropertyChanged(nameof(Waveform));
            OnPropertyChanged(nameof(DurationText));
        }

        public void Cancel()
        {
            var cts = loadCts;
            loadCts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        TimeSpan WindowStart => AudioPreviewService.GetWindowStart(position, windowLength);

        void StartLoad()
        {
            if (loadCts != null)
                return;

            var capturedStart = WindowStart;
            if (preview != null && preview.Start == capturedStart)
                return;

            var capturedLength = windowLength;
            loadCts = new CancellationTokenSource();
            _ = LoadAsync(capturedStart, capturedLength, loadCts.Token);
        }

        async Task LoadAsync(TimeSpan capturedStart, TimeSpan capturedLength, CancellationToken token)
        {
            var myCts = loadCts;
            try
            {
                var result = await AudioPreviewService.LoadAsync(path, capturedStart, capturedLength, token);

                if (result != null && !token.IsCancellationRequested)
                {
                    preview = result;
                    OnPropertyChanged(nameof(Waveform));
                    OnPropertyChanged(nameof(DurationText));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerAudioPreviewViewModel.Load", e);
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(loadCts, myCts))
                    {
                        loadCts = null;
                        myCts?.Dispose();
                    }
                });
            }
        }
    }
}
