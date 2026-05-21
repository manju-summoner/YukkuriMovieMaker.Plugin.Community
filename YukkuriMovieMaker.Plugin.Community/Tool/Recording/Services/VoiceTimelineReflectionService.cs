using System;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceTimelineReflectionService
    {
        private readonly TimelineAudioMetricsService timelineAudioMetricsService = new();

        public object? GetActiveTimeline(object mainViewModel)
        {
            var mainViewModelType = mainViewModel.GetType();

            var activeTimelineViewModel = mainViewModelType
                .GetProperty("ActiveTimelineViewModel", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(mainViewModel);

            if (activeTimelineViewModel is not null)
            {
                var timelineField = activeTimelineViewModel.GetType()
                    .GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic);

                if (timelineField?.GetValue(activeTimelineViewModel) is { } timeline)
                    return timeline;
            }

            var modelField = mainViewModelType.GetField("model", BindingFlags.Instance | BindingFlags.NonPublic);
            var model = modelField?.GetValue(mainViewModel);
            return model?.GetType()
                .GetProperty("Timeline", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(model);
        }

        public int GetCurrentFrame(object timeline)
        {
            var frame = TimelineMemberReader.GetCurrentFrame(timeline);
            if (frame == int.MinValue)
                throw new InvalidOperationException(Texts.CurrentFrameUnavailable);
            return frame;
        }

        public int GetLengthFrames(object timeline, string filePath)
        {
            return timelineAudioMetricsService.GetLengthFrames(timeline, filePath);
        }

        public object CreateVoiceItemViaReflection(RecordingScriptItem item, int frame, int length, int layer)
        {
            var voiceItemType = Type.GetType("YukkuriMovieMaker.Project.Items.VoiceItem, YukkuriMovieMaker")
                ?? throw new InvalidOperationException(Texts.VoiceItemUnavailable);

            var parameter = new RecordedVoiceParameter
            {
                Text = item.Text,
                AudioFilePath = item.AudioFilePath,
                Duration = item.Duration,
                CreatedAt = item.CreatedAt
            };

            var characterType = Type.GetType("YukkuriMovieMaker.Project.Character, YukkuriMovieMaker")
                ?? throw new InvalidOperationException(Texts.CharacterUnavailable);
            var character = Activator.CreateInstance(characterType)
                ?? throw new InvalidOperationException(Texts.CharacterCreateFailed);

            var voiceDescriptionType = Type.GetType("YukkuriMovieMaker.Plugin.Voice.VoiceDescription, YukkuriMovieMaker.Plugin")
                ?? throw new InvalidOperationException(Texts.VoiceDescriptionUnavailable);
            var speaker = RecordedVoiceSpeaker.Instance;
            var voiceDescription = Activator.CreateInstance(voiceDescriptionType, speaker)
                ?? throw new InvalidOperationException(Texts.VoiceDescriptionCreateFailed);
            var apiProp = voiceDescriptionType.GetProperty("API");
            if (apiProp?.CanWrite == true)
                apiProp.SetValue(voiceDescription, RecordedVoiceSpeaker.ApiName);
            var argProp = voiceDescriptionType.GetProperty("Arg");
            if (argProp?.CanWrite == true)
                argProp.SetValue(voiceDescription, RecordedVoiceSpeaker.SpeakerId);

            characterType.GetProperty("Voice")?.SetValue(character, voiceDescription);
            characterType.GetProperty("VoiceParameter")?.SetValue(character, parameter.Clone());

            var voiceItem = Activator.CreateInstance(voiceItemType, character)
                ?? throw new InvalidOperationException(Texts.VoiceItemUnavailable);

            voiceItemType.GetProperty("Serif")?.SetValue(voiceItem, item.Text);
            voiceItemType.GetProperty("VoiceParameter")?.SetValue(voiceItem, parameter);
            voiceItemType.GetProperty("Frame")?.SetValue(voiceItem, frame);
            voiceItemType.GetProperty("Layer")?.SetValue(voiceItem, layer);
            voiceItemType.GetProperty("Length")?.SetValue(voiceItem, length);
            var voiceLengthProp = voiceItemType.GetProperty("VoiceLength");
            if (voiceLengthProp?.CanWrite == true)
                voiceLengthProp.SetValue(voiceItem, item.Duration ?? timelineAudioMetricsService.GetAudioDuration(item.AudioFilePath));

            return voiceItem;
        }

        public void TryAddItem(object timeline, object voiceItem, int frame, int layer)
        {
            var timelineType = timeline.GetType();
            var itemInterfaceType = timelineType.Assembly.GetType("YukkuriMovieMaker.Project.Items.IItem")
                ?? throw new InvalidOperationException(Texts.IItemUnavailable);

            var itemArray = Array.CreateInstance(itemInterfaceType, 1);
            itemArray.SetValue(voiceItem, 0);

            var tryAddItems = timelineType.GetMethod(
                "TryAddItems",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { itemArray.GetType(), typeof(int), typeof(int) },
                modifiers: null);

            if (tryAddItems is not null)
            {
                var added = (bool)tryAddItems.Invoke(timeline, new object[] { itemArray, frame, layer })!;
                if (!added)
                    throw new InvalidOperationException(Texts.TimelineAddFailedMessage);
                return;
            }

            var addItems = timelineType.GetMethod("AddItems", BindingFlags.Instance | BindingFlags.Public);
            if (addItems is null)
                throw new InvalidOperationException(Texts.TimelineAddMethodUnavailable);

            addItems.Invoke(timeline, new object[] { itemArray });
        }
    }
}
