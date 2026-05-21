using System;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceTimelineDirectInsertService
    {
        private readonly TimelineAudioMetricsService timelineAudioMetricsService = new();

        public void Insert(Timeline timeline, RecordingScriptItem item, int? selectedFrame, int? selectedLayer)
        {
            var frame = selectedFrame ?? timeline.CurrentFrame;
            var layer = selectedLayer ?? 0;
            var length = timelineAudioMetricsService.GetLengthFrames(timeline, item.AudioFilePath);
            var voiceItem = CreateVoiceItem(item, frame, length, layer);

            var added = timeline.TryAddItems(new IItem[] { voiceItem }, voiceItem.Frame, voiceItem.Layer);
            if (!added)
                throw new InvalidOperationException(Texts.TimelineAddFailedMessage);
        }

        private static VoiceItem CreateVoiceItem(RecordingScriptItem item, int frame, int length, int layer)
        {
            var parameter = new RecordedVoiceParameter
            {
                Text = item.Text,
                AudioFilePath = item.AudioFilePath,
                Duration = item.Duration,
                CreatedAt = item.CreatedAt
            };

            var character = new Character
            {
                Voice = RecordedVoiceSpeaker.Description,
                VoiceParameter = parameter.Clone()
            };

            return new VoiceItem(character)
            {
                Serif = item.Text,
                VoiceParameter = parameter,
                Frame = frame,
                Layer = layer,
                Length = length
            };
        }
    }
}
