using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    // サンプリング密度(片側サンプル数の上限)。半径が小さいうちは半径で決まるが、
    // 半径が大きいときに品質と処理負荷のトレードオフを決める。
    public enum AnisotropicKuwaharaQuality
    {
        [Display(Name = nameof(Texts.AnisotropicKuwaharaQualityLow), Description = nameof(Texts.AnisotropicKuwaharaQualityLowDesc), ResourceType = typeof(Texts))]
        Low,

        [Display(Name = nameof(Texts.AnisotropicKuwaharaQualityMedium), Description = nameof(Texts.AnisotropicKuwaharaQualityMediumDesc), ResourceType = typeof(Texts))]
        Medium,

        [Display(Name = nameof(Texts.AnisotropicKuwaharaQualityHigh), Description = nameof(Texts.AnisotropicKuwaharaQualityHighDesc), ResourceType = typeof(Texts))]
        High,

        [Display(Name = nameof(Texts.AnisotropicKuwaharaQualityUltra), Description = nameof(Texts.AnisotropicKuwaharaQualityUltraDesc), ResourceType = typeof(Texts))]
        Ultra,
    }
}
