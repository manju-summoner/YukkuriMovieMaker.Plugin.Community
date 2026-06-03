using System;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public static class PuppetDeformationFrameService
    {
        public static ISceneInfo? CurrentScene { get; set; }
        public static int CurrentFrame { get; set; }
        public static int CurrentFPS { get; set; }
        public static volatile bool IsInternalRendering;

        public static event EventHandler? FrameUpdated;

        public static void NotifyFrameUpdated()
        {
            FrameUpdated?.Invoke(null, EventArgs.Empty);
        }
    }
}
