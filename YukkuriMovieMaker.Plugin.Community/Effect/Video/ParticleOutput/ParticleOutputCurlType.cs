using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    public enum ParticleOutputCurlType
    {
        [Display(Name = nameof(Texts.CurlTypeDisplacement), ResourceType = typeof(Texts))]
        Displacement = 0,

        [Display(Name = nameof(Texts.CurlTypeAdvection), ResourceType = typeof(Texts))]
        Advection = 1,
    }
}
