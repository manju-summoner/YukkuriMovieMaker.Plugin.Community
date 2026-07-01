using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public sealed class PuppetDeformationMapPinViewModel : Bindable
    {
        double canvasX;
        double canvasY;
        bool isSelected;
        bool isEnabled;

        public PuppetDeformation Model { get; }
        public bool IsOffset { get; }

        public double CanvasX
        {
            get => canvasX;
            set => Set(ref canvasX, value);
        }

        public double CanvasY
        {
            get => canvasY;
            set => Set(ref canvasY, value);
        }

        public bool IsSelected
        {
            get => isSelected;
            set => Set(ref isSelected, value);
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set => Set(ref isEnabled, value);
        }

        public PuppetDeformationMapPinViewModel(PuppetDeformation model, bool isOffset)
        {
            Model = model;
            IsOffset = isOffset;
            isEnabled = model.IsEnabled;
            isSelected = isOffset ? model.IsOffsetSelected : model.IsRestSelected;
        }
    }
}
