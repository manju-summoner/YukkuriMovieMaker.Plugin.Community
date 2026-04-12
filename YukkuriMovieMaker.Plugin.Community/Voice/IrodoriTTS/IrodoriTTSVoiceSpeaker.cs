using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSVoiceSpeaker(string refVoiceFilePath, string speakerName) : IVoiceSpeaker
{
    static readonly SemaphoreSlim semaphore = new(1);

    public string EngineName => "Irodori-TTS";
    public string SpeakerName => speakerName;
    public string API => "IrodoriTTS";
    public string ID => Path.GetFileNameWithoutExtension(refVoiceFilePath);
    public bool IsVoiceDataCachingRequired => false;
    public SupportedTextFormat Format => SupportedTextFormat.Text;
    public IVoiceLicense? License => null;
    public IVoiceResource? Resource => null;

    public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter param) => Task.FromResult(text);

    public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
    {
        if (parameter is not IrodoriTTSVoiceParameter irodoriParam)
            return null;

        await semaphore.WaitAsync();
        try
        {
            var url = await IrodoriTTSGradioServer.EnsureTTSServerAsync();

            using var refVoice = RefVoiceFile.Load(refVoiceFilePath);
            if (!refVoice.HasRefFilePath)
                return null;

            await IrodoriTTSAPI.SynthesizeAsync(
                url,
                text,
                refVoice.RefFilePath!,
                irodoriParam.NumSteps,
                irodoriParam.CfgScaleText,
                irodoriParam.CfgScaleSpeaker,
                filePath,
                irodoriParam.Checkpoint);
        }
        finally
        {
            semaphore.Release();
        }

        return null;
    }

    public IVoiceParameter CreateVoiceParameter() => new IrodoriTTSVoiceParameter();

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
    {
        return currentParameter is IrodoriTTSVoiceParameter
            ? currentParameter
            : new IrodoriTTSVoiceParameter();
    }

    public bool IsMatch(string api, string id) => api == API && id == ID;
}
