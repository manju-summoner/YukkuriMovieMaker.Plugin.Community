using System;
using System.Diagnostics;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class RecordingLifecycleService
    {
        public static void DetachRecordingEvents(
            RecordingService recordingService,
            EventHandler<Models.RecordingDataEventArgs> onRecordingDataAvailable,
            EventHandler onRecordingStateChanged)
        {
            recordingService.DataAvailable -= onRecordingDataAvailable;
            recordingService.RecordingStateChanged -= onRecordingStateChanged;
        }

        public static void AttachRecordingEvents(
            RecordingService recordingService,
            EventHandler<Models.RecordingDataEventArgs> onRecordingDataAvailable,
            EventHandler onRecordingStateChanged)
        {
            recordingService.DataAvailable += onRecordingDataAvailable;
            recordingService.RecordingStateChanged += onRecordingStateChanged;
        }

        public static void TryStopRecording(RecordingService recordingService)
        {
            try
            {
                if (recordingService.IsRecording)
                    recordingService.StopRecording();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RecordingLifecycleService] TryStopRecording failed: {ex}");
            }
        }
    }
}
