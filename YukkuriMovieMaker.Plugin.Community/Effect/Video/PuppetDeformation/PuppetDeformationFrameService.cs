using System;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public static class PuppetDeformationFrameService
    {
        public sealed record FrameSnapshot(ISceneInfo Scene, int Frame, int FPS);

        static FrameSnapshot? _snapshot;

        public static FrameSnapshot? Snapshot => Volatile.Read(ref _snapshot);

        public static volatile bool IsInternalRendering;

        public static event EventHandler? FrameUpdated;

        public static void Publish(ISceneInfo scene, int frame, int fps)
        {
            Volatile.Write(ref _snapshot, new FrameSnapshot(scene, frame, fps));
            FrameUpdated?.Invoke(null, EventArgs.Empty);
        }
    }
}
