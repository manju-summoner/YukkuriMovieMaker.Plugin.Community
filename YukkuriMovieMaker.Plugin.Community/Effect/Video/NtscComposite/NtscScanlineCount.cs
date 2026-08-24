using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジットエフェクトの走査線数(有効走査線)。
    /// 240はノンインターレース(240p)の往年のゲーム機系の見た目になる。
    /// </summary>
    internal enum NtscScanlineCount
    {
        [Display(Name = nameof(Texts.NtscScanlineCountLines480Name), Description = nameof(Texts.NtscScanlineCountLines480Name), ResourceType = typeof(Texts))]
        Lines480 = 480,
        [Display(Name = nameof(Texts.NtscScanlineCountLines240Name), Description = nameof(Texts.NtscScanlineCountLines240Name), ResourceType = typeof(Texts))]
        Lines240 = 240,
    }
}
