using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models
{
    public class RecordingDataEventArgs(double volume) : EventArgs
    {
        public double Volume { get; } = volume;
    }
}



