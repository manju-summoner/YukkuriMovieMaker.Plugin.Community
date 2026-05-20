using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models
{
    public class RecordingDataEventArgs : EventArgs
    {
        public RecordingDataEventArgs(double volume)
        {
            Volume = volume;
        }

        public double Volume { get; }
    }
}



