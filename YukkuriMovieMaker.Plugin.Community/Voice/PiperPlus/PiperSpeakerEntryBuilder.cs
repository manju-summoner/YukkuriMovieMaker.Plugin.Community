using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperSpeakerEntryBuilder
{
    public static IEnumerable<PiperSpeakerEntry> Build(PiperModelInfo model)
    {
        if (!model.IsMultiSpeaker)
        {
            yield return new PiperSpeakerEntry
            {
                ModelPath = model.ModelPath,
                ConfigPath = model.ConfigPath,
                ModelName = model.ModelName,
                SpeakerId = 0,
                SpeakerName = model.ModelName,
                IsMultiSpeaker = false,
            };
            yield break;
        }

        if (model.SpeakerIdMap.Count > 0)
        {
            foreach (var (name, id) in model.SpeakerIdMap.OrderBy(kv => kv.Value))
            {
                yield return new PiperSpeakerEntry
                {
                    ModelPath = model.ModelPath,
                    ConfigPath = model.ConfigPath,
                    ModelName = model.ModelName,
                    SpeakerId = id,
                    SpeakerName = name,
                    IsMultiSpeaker = true,
                };
            }
        }
        else
        {
            for (var i = 0; i < model.NumSpeakers; i++)
            {
                yield return new PiperSpeakerEntry
                {
                    ModelPath = model.ModelPath,
                    ConfigPath = model.ConfigPath,
                    ModelName = model.ModelName,
                    SpeakerId = i,
                    SpeakerName = i.ToString(),
                    IsMultiSpeaker = true,
                };
            }
        }
    }
}
