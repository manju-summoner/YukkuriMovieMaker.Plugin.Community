using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSVoicePlugin : IVoicePlugin
{
    public IEnumerable<IVoiceSpeaker> Voices => GetSpeakers();

    public bool CanUpdateVoices => false;
    public bool IsVoicesCached => false;
    public string Name => "Irodori-TTS";

    public Task UpdateVoicesAsync() => Task.CompletedTask;

    static IEnumerable<IVoiceSpeaker> GetSpeakers()
    {
        var dir = RefVoiceFile.RefVoicesDirectory;
        if (!Directory.Exists(dir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(dir, "*.ymm4refvoice"))
        {
            RefVoiceFile? meta = null;
            try
            {
                meta = RefVoiceFile.FastLoad(file);
            }
            catch (Exception ex)
            {
                Log.Default.Write($"Irodori-TTS failed to load ref voice: {file}", ex);
                continue;
            }

            yield return new IrodoriTTSVoiceSpeaker(file, meta.Name);
        }
    }
}
