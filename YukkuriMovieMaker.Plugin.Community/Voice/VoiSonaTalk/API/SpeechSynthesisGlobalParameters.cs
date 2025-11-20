using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    /// <summary>
    /// 音声合成のグローバルパラメータ
    /// </summary>
    /// <param name="Alp">年齢に似たパラメータ。[0..1]</param>
    /// <param name="Huskiness">ハスキーさのパラメータ。[ -20..20 ]</param>
    /// <param name="Intonation">抑揚のパラメータ。[0..2]</param>
    /// <param name="Pitch">音高のパラメータ。[ -600..600 ]</param>
    /// <param name="Speed">速度のパラメータ。[0.2..5]</param>
    /// <param name="StyleWeights">スタイルの重み付け。</param>
    /// <param name="Volume">音量のパラメータ。[ -8..8 ]</param>
    internal record SpeechSynthesisGlobalParameters(
        [property: JsonProperty("alp")] double Alp,
        [property: JsonProperty("huskiness")] double Huskiness,
        [property: JsonProperty("intonation")] double Intonation,
        [property: JsonProperty("pitch")] double Pitch,
        [property: JsonProperty("speed")] double Speed,
        [property: JsonProperty("style_weights")] double[] StyleWeights,
        [property: JsonProperty("volume")] double Volume
    );
}
