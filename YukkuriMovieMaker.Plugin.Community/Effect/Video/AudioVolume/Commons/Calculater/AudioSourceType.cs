using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public enum AudioSourceType
    {
        [Display(Name = nameof(Texts.AudioSourceTypeFile), Description = nameof(Texts.AudioSourceTypeFile), ResourceType = typeof(Texts))]
        File,
        [Display(Name = nameof(Texts.AudioSourceTypeScene), Description = nameof(Texts.AudioSourceTypeScene), ResourceType = typeof(Texts))]
        Scene,
        [Display(Name = nameof(Texts.AudioSourceTypeTimeline), Description = nameof(Texts.AudioSourceTypeTimeline), ResourceType = typeof(Texts))]
        Timeline,
    }
}
