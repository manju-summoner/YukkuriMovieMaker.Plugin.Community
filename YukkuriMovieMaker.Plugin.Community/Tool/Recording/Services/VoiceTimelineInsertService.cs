using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class VoiceTimelineInsertService
    {
        private readonly VoiceTimelineReflectionService reflectionService = new();
        private readonly VoiceItemAttachmentService voiceItemAttachmentService = new();
        private readonly VoiceTimelineDirectInsertService directInsertService = new();
        private readonly TimelineSelectionService selectionService;
        private readonly VoiceTargetResolverService targetResolver;

        public VoiceTimelineInsertService()
            : this(new TimelineSelectionService(), new VoiceTargetResolverService())
        {
        }

        internal VoiceTimelineInsertService(TimelineSelectionService selectionService, VoiceTargetResolverService targetResolver)
        {
            this.selectionService = selectionService;
            this.targetResolver = targetResolver;
        }

        public Task InsertAsync(RecordingScriptItem item)
        {
            if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (string.IsNullOrWhiteSpace(item.AudioFilePath) || !File.Exists(item.AudioFilePath))
                throw new FileNotFoundException("録音済み wav が見つかりません。", item.AudioFilePath);

            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("UI Dispatcher を取得できません。");

            return dispatcher.InvokeAsync(() =>
            {
                int? selectedFrame = null;
                int? selectedLayer = null;
                if (selectionService.TryGetSelectedPlacement(out var frame, out var placementLayer))
                {
                    selectedFrame = frame;
                    selectedLayer = placementLayer;
                }

                if (voiceItemAttachmentService.TryAttach(item, selectionService, targetResolver))
                    return;

                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    directInsertService.Insert(timeline, item, selectedFrame, selectedLayer);
                    return;
                }

                var mainViewModel = Application.Current?.MainWindow?.DataContext
                    ?? throw new InvalidOperationException("MainViewModel を取得できません。");

                var fallbackTimeline = reflectionService.GetActiveTimeline(mainViewModel)
                    ?? throw new InvalidOperationException("タイムラインを取得できません。");

                var currentFrame = selectedFrame ?? reflectionService.GetCurrentFrame(fallbackTimeline);
                var length = reflectionService.GetLengthFrames(fallbackTimeline, item.AudioFilePath);
                var targetLayer = selectedLayer ?? 0;
                var voiceItem = reflectionService.CreateVoiceItemViaReflection(item, currentFrame, length, targetLayer);
                reflectionService.TryAddItem(fallbackTimeline, voiceItem, currentFrame, targetLayer);
            }).Task;
        }
    }
}
