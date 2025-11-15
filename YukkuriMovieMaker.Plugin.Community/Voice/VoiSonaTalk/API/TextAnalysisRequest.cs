using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ForceEnqueue">既存の音声合成リクエストおよび／またはテキスト分析リクエストを削除して、このリクエストをキューに入れるかどうかを示すフラグです。trueの場合、「キュー済み」または「実行中」状態にあるリクエストを除き、最も古いリクエストが自動的に削除され、このリクエストがキューに入れられます。システムのリクエストキューがまだいっぱいの場合、このAPIは409（競合）エラーで失敗します。</param>
    /// <param name="Language">言語（例："ja_JP"）</param>
    /// <param name="Text">分析するテキスト（1～500文字）</param>
    internal record TextAnalysisRequest(
        [property: JsonProperty("force_enqueue")] bool ForceEnqueue, 
        [property: JsonProperty("language")] string Language,
        [property: JsonProperty("text")] string Text);

}
