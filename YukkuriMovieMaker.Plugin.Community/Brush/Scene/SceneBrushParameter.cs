using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Scene
{
    internal class SceneBrushParameter : ScalableDrawingBrushParameterBase
    {
        [Display(Name = nameof(Texts.Scene), ResourceType = typeof(Texts))]
        [SceneComboBox]
        public Guid SceneId { get => sceneId; set => Set(ref sceneId, value); }
        Guid sceneId;

        [Display(Name = nameof(Texts.PlaybackRate), Description = nameof(Texts.PlaybackRate), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", -400, 400)]
        [DefaultValue(100)]
        [Range(int.MinValue, int.MaxValue)]
        public double PlaybackRate { get => playbackRate; set => Set(ref playbackRate, value); }
        double playbackRate = 100;

        [Display(Name = nameof(Texts.ContentOffset), Description = nameof(Texts.ContentOffset), ResourceType = typeof(Texts))]
        [TimeSpanRange]
        [TimeSpanDefaultValue]
        [TimeSpanEditor]
        public TimeSpan ContentOffset { get => contentOffset; set=>Set(ref contentOffset,value); }
        TimeSpan contentOffset = TimeSpan.Zero;

        [Display(Name = nameof(Texts.ExtendModeX), Description = nameof(Texts.ExtendModeX), Order = 450, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ExtendMode ExtendModeX { get => extendMode; set => Set(ref extendMode, value); }
        ExtendMode extendMode = ExtendMode.Wrap;

        [Display(Name = nameof(Texts.ExtendModeY), Description = nameof(Texts.ExtendModeY), Order = 450, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ExtendMode ExtendModeY { get => extendModeY; set => Set(ref extendModeY, value); }
        ExtendMode extendModeY = ExtendMode.Wrap;

        [Display(Name = nameof(Texts.RemoveBoundary), Description = nameof(Texts.RemoveBoundary), Order = 600, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsRemoveBoundaryEnabled { get => isRemoveBoundaryEnabled; set => Set(ref isRemoveBoundaryEnabled, value); }
        bool isRemoveBoundaryEnabled = true;

        [Display(Name = nameof(Texts.FixSize), Description = nameof(Texts.FixSize), Order = 600, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsFixSizeEnabled { get => isFixSizeEnabled; set => Set(ref isFixSizeEnabled, value); }
        bool isFixSizeEnabled = false;


        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new SceneBrushSource(devices, this);
        }
    }
}