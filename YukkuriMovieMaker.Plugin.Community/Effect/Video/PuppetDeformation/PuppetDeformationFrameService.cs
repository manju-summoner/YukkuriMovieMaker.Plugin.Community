using System;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public static class PuppetDeformationFrameService
    {
        public sealed record FrameSnapshot(ISceneInfo Scene, int Frame, int FPS, int ItemFrame, int ItemLength);

        static FrameSnapshot? _snapshot;

        public static FrameSnapshot? Snapshot => Volatile.Read(ref _snapshot);

        public static volatile bool IsInternalRendering;

        public static event EventHandler? FrameUpdated;

        public static void Publish(ISceneInfo scene, int frame, int fps, int itemFrame, int itemLength)
        {
            Volatile.Write(ref _snapshot, new FrameSnapshot(scene, frame, fps, itemFrame, itemLength));
            FrameUpdated?.Invoke(null, EventArgs.Empty);
        }
    }
}
