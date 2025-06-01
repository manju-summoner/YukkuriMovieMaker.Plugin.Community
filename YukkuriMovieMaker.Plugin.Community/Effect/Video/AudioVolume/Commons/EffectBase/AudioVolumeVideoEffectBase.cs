using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase
{
    public abstract class AudioVolumeVideoEffectBase : VideoEffectBase, IFileItem
    {
        [Display(GroupName = nameof(Texts.AudioSourceGroup), AutoGenerateField = true, ResourceType = typeof(Texts))]
        public AudioVolumeCalculater Calculater { get; } = new AudioVolumeCalculater();

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Calculater];

        public override IEnumerable<string> GetFiles()
        {
            foreach (var file in base.GetFiles())
                yield return file;
            foreach (var file in Calculater.GetFiles())
                yield return file;
        }

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            Calculater.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach (var resource in base.GetResources())
                yield return resource;
            foreach (var resource in Calculater.GetResources())
                yield return resource;
        }
    }
}
