using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public enum PuppetDeformationAlgorithm
    {
        [Display(Name = nameof(Texts.PuppetDeformationAlgorithmMlsName), Description = nameof(Texts.PuppetDeformationAlgorithmMlsDesc), ResourceType = typeof(Texts))]
        Mls,
        [Display(Name = nameof(Texts.PuppetDeformationAlgorithmArapName), Description = nameof(Texts.PuppetDeformationAlgorithmArapDesc), ResourceType = typeof(Texts))]
        Arap
    }
}
