using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class TimelineSelectionService
    {
        private static readonly object preferredVoiceTargetLock = new();
        private static PreferredVoiceTarget? preferredVoiceTarget;

        public static bool TryGetPreferredVoiceTarget(out int frame, out int layer, out string? serif)
        {
            frame = 0;
            layer = 0;
            serif = null;

            lock (preferredVoiceTargetLock)
            {
                var target = preferredVoiceTarget;
                if (target is null)
                    return false;

                frame = target.Frame;
                layer = target.Layer;
                serif = target.Serif;
                return true;
            }
        }

        public static void ClearPreferredVoiceTarget()
        {
            lock (preferredVoiceTargetLock)
            {
                preferredVoiceTarget = null;
            }
        }

        public string? TryGetSelectedSerif()
        {
            var items = GetSelectedItemsSnapshot();
            foreach (var item in items)
            {
                if (item is VoiceItem voiceItem && !string.IsNullOrWhiteSpace(voiceItem.Serif))
                {
                    return voiceItem.Serif;
                }
                if (item is TextItem textItem && !string.IsNullOrWhiteSpace(textItem.Text))
                {
                    return textItem.Text;
                }
            }
            return null;
        }

        public bool TryGetSelectedPlacement(out int frame, out int layer)
        {
            frame = 0;
            layer = 0;

            var items = GetSelectedItemsSnapshot();
            foreach (var item in items.OfType<IItem>())
            {
                frame = item.Frame;
                layer = item.Layer;
                return true;
            }
            return false;
        }

        public bool TryMoveToNextSerif(string? currentSerif, out string? nextSerif)
        {
            nextSerif = null;
            try
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is null)
                {
                    return false;
                }

                var selected = GetSelectedItemsSnapshot().OfType<VoiceItem>().FirstOrDefault();
                if (selected is null)
                {
                    return false;
                }

                var voiceItems = timeline.Items.OfType<VoiceItem>().OrderBy(x => x.Frame).ToList();
                if (voiceItems.Count == 0)
                {
                    return false;
                }

                var currentIndex = voiceItems.IndexOf(selected);
                if (currentIndex < 0 || currentIndex >= voiceItems.Count - 1)
                {
                    ClearPreferredVoiceTarget();
                    return false;
                }

                var next = voiceItems[currentIndex + 1];
                var serif = next.Serif;
                if (string.IsNullOrWhiteSpace(serif))
                {
                    return false;
                }

                SetPreferredVoiceTarget(next, serif);
                nextSerif = serif;

                timeline.SelectedItems = System.Collections.Immutable.ImmutableList.Create<IItem>(next);
                timeline.CurrentFrame = next.Frame;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] TryMoveToNextSerif failed: {ex}");
                return false;
            }
        }

        private static void SetPreferredVoiceTarget(IItem item, string serif)
        {
            if (item.Frame < 0 || item.Layer < 0)
                return;

            lock (preferredVoiceTargetLock)
            {
                preferredVoiceTarget = new PreferredVoiceTarget(item.Frame, item.Layer, serif);
            }
        }

        public IReadOnlyList<object> GetSelectedItemsSnapshot()
        {
            try
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    return timeline.SelectedItems.Cast<object>().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] GetSelectedItemsSnapshot failed: {ex}");
            }
            return Array.Empty<object>();
        }

        public IReadOnlyList<object> GetVoiceItemsSnapshot()
        {
            try
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is null)
                    return Array.Empty<object>();

                return timeline.Items.OfType<VoiceItem>().OrderBy(x => x.Frame).Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] GetVoiceItemsSnapshot failed: {ex}");
                return Array.Empty<object>();
            }
        }

        private sealed class PreferredVoiceTarget
        {
            public PreferredVoiceTarget(int frame, int layer, string? serif)
            {
                Frame = frame;
                Layer = layer;
                Serif = serif;
            }

            public int Frame { get; }
            public int Layer { get; }
            public string? Serif { get; }
        }
    }
}
