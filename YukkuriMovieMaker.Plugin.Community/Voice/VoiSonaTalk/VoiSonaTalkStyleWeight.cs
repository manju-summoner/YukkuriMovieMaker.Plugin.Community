using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkStyleWeight : UndoRedoable
    {
        string? name;
        double weight;
        public string? Name { get=> name; set => Set(ref name, value); }

        [VoiSonaTalkStyleWeightDisplay]
        [TextBoxSlider("F2", "", 0, 1)]
        [Range(0d, 1d)]
        [DefaultValue(0.0d)]
        public double Weight { get => weight; set => Set(ref weight, value); }
    }
}
