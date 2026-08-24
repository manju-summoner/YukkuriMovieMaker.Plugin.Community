using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジットエフェクトのY/C分離方式。
    /// ノッチ: fsc近傍除去+LPF。クロスカラーが多いが縦方向の混色は無い。
    /// コム: 前後ラインとの加減算(2ラインコム)。クロスカラーが減る代わりに縦方向の色混じりが出る。
    /// </summary>
    internal enum NtscYCSeparationMode
    {
        [Display(Name = nameof(Texts.NtscYCSeparationNotchName), Description = nameof(Texts.NtscYCSeparationNotchName), ResourceType = typeof(Texts))]
        Notch = 0,
        [Display(Name = nameof(Texts.NtscYCSeparationCombName), Description = nameof(Texts.NtscYCSeparationCombName), ResourceType = typeof(Texts))]
        Comb = 1,
    }
}
