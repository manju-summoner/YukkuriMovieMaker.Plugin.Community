using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジットエフェクトのセットアップレベル(黒レベル)。
    /// NTSC-J(日本)は0 IRE、NTSC-M(北米等)は7.5 IRE。
    /// </summary>
    internal enum NtscSetupLevel
    {
        [Display(Name = nameof(Texts.NtscSetupLevelIre0Name), Description = nameof(Texts.NtscSetupLevelIre0Name), ResourceType = typeof(Texts))]
        Ire0 = 0,
        [Display(Name = nameof(Texts.NtscSetupLevelIre75Name), Description = nameof(Texts.NtscSetupLevelIre75Name), ResourceType = typeof(Texts))]
        Ire75 = 1,
    }
}
