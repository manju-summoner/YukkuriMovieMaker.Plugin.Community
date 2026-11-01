using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    sealed class PenComposeChain
    {
        public ID2D1Effect? Effect;
        public ID2D1Image? Image;
        public ID2D1Image? GroupImage;
        public ID2D1Image? GroupMask;
        public PenLayerPlan GroupPlan;

        public void Reset()
        {
            Effect = null;
            Image = null;
            GroupImage = null;
            GroupMask = null;
            GroupPlan = default;
        }
    }
}
