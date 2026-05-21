using System;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal sealed class VoiceTimelineFallbackInsertService
    {
        private readonly VoiceTimelineReflectionService reflectionService = new();

        public void Insert(object mainViewModel, RecordingScriptItem item, int? selectedFrame, int? selectedLayer)
        {
            var timeline = reflectionService.GetActiveTimeline(mainViewModel)
                ?? throw new InvalidOperationException(Texts.TimelineUnavailable);

            var currentFrame = selectedFrame ?? reflectionService.GetCurrentFrame(timeline);
            var length = reflectionService.GetLengthFrames(timeline, item.AudioFilePath);
            var targetLayer = selectedLayer ?? 0;
            var voiceItem = reflectionService.CreateVoiceItemViaReflection(item, currentFrame, length, targetLayer);
            reflectionService.TryAddItem(timeline, voiceItem, currentFrame, targetLayer);
        }
    }
}
