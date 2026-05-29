using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperSpeakerEntryBuilder
{
    public static IEnumerable<PiperSpeakerEntry> Build(PiperModelInfo model)
    {
        foreach (var (speakerId, speakerName) in EnumerateSpeakers(model))
        {
            yield return new PiperSpeakerEntry
            {
                ModelPath = model.ModelPath,
                ConfigPath = model.ConfigPath,
                ModelName = model.ModelName,
                ModelDisplayName = model.ModelName,
                SpeakerId = speakerId,
                SpeakerName = speakerName,
                IsMultiSpeaker = model.IsMultiSpeaker,
                LanguageArgument = model.LanguageArgument,
            };
        }
    }

    static IEnumerable<(int Id, string Name)> EnumerateSpeakers(PiperModelInfo model)
    {
        if (!model.IsMultiSpeaker)
        {
            yield return (0, model.ModelName);
            yield break;
        }

        if (model.SpeakerIdMap.Count > 0)
        {
            foreach (var (name, id) in model.SpeakerIdMap.OrderBy(kv => kv.Value))
                yield return (id, name);
        }
        else
        {
            for (var i = 0; i < model.NumSpeakers; i++)
                yield return (i, i.ToString());
        }
    }
}
