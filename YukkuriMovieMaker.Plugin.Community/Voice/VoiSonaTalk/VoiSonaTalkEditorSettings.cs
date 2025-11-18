using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkEditorSettings : Bindable
    {
        double width = 600;
        public double Width { get => width; set => Set(ref width, value); }
    }
}
