using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class TimelineSelectionService
    {
        private static readonly object preferredVoiceTargetLock = new();
        private static PreferredVoiceTarget? preferredVoiceTarget;

        private static readonly string[] SelectionPropertyNames =
        {
            "SelectedItems",
            "SelectedItem",
            "SelectedTimelineItems",
            "SelectedItemViewModels",
            "SelectedElements",
            "SelectedClips"
        };

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
                var serif = TimelineMemberReader.GetStringProperty(item, "Serif")
                            ?? TimelineMemberReader.GetStringProperty(item, "Text");
                if (!string.IsNullOrWhiteSpace(serif))
                {
                    return serif;
                }
            }
            return null;
        }

        public bool TryGetSelectedPlacement(out int frame, out int layer)
        {
            frame = 0;
            layer = 0;

            var items = GetSelectedItemsSnapshot();
            foreach (var item in items)
            {
                if (TimelineMemberReader.TryGetIntProperty(item, "Frame", out var foundFrame))
                {
                    frame = foundFrame;
                    if (!TimelineMemberReader.TryGetIntProperty(item, "Layer", out layer))
                        layer = 0;
                    return true;
                }
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

                var selected = GetSelectedItemsSnapshot()
                    .Where(VoiceTimelineNavigator.IsVoiceLikeItem)
                    .Distinct()
                    .FirstOrDefault();
                if (selected is null)
                {
                    return false;
                }

                var voiceItems = VoiceTimelineNavigator.GetSortedVoiceItems(timeline);
                if (voiceItems.Count == 0)
                {
                    return false;
                }

                var currentFrame = GetCurrentFrame(timeline);
                var currentIndex = VoiceTimelineNavigator.FindCurrentIndex(voiceItems, selected, currentSerif, currentFrame);
                if (currentIndex < 0 || currentIndex >= voiceItems.Count - 1)
                {
                    ClearPreferredVoiceTarget();
                    return false;
                }

                var next = voiceItems[currentIndex + 1];
                var serif = TimelineMemberReader.GetStringProperty(next, "Serif") ?? TimelineMemberReader.GetStringProperty(next, "Text");
                if (string.IsNullOrWhiteSpace(serif))
                {
                    return false;
                }

                SetPreferredVoiceTarget(next, serif);
                nextSerif = serif;
                var selectionMoved = false;
                try
                {
                    selectionMoved = TimelineSelectionApplier.TrySetSelection(next, SelectionPropertyNames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TimelineSelectionService] TryMoveToNextSerif selection apply failed: {ex}");
                }

                var frameMoved = TimelineSelectionApplier.TryMoveCurrentFrame(next);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] TryMoveToNextSerif failed: {ex}");
                return false;
            }
        }

        private static void SetPreferredVoiceTarget(object item, string serif)
        {
            var frame = TimelineMemberReader.GetIntMember(item, "Frame");
            var layer = TimelineMemberReader.GetIntMember(item, "Layer");
            if (frame < 0 || layer < 0)
                return;

            lock (preferredVoiceTargetLock)
            {
                preferredVoiceTarget = new PreferredVoiceTarget(frame, layer, serif);
            }
        }

        public IReadOnlyList<object> GetSelectedItemsSnapshot()
        {
            var result = new List<object>();

            try
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    CollectFromObject(timeline, result);
                }
                else
                {
                    var mainViewModel = Application.Current?.MainWindow?.DataContext;
                    if (mainViewModel is not null)
                    {
                        CollectFromObject(mainViewModel, result);
                        var activeTimelineViewModel = mainViewModel.GetType()
                            .GetProperty("ActiveTimelineViewModel", BindingFlags.Instance | BindingFlags.Public)
                            ?.GetValue(mainViewModel);
                        if (activeTimelineViewModel is not null)
                        {
                            CollectFromObject(activeTimelineViewModel, result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] GetSelectedItemsSnapshot failed: {ex}");
            }
            return result;
        }

        public IReadOnlyList<object> GetVoiceItemsSnapshot()
        {
            try
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is null)
                    return Array.Empty<object>();

                return VoiceTimelineNavigator.GetSortedVoiceItems(timeline);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] GetVoiceItemsSnapshot failed: {ex}");
                return Array.Empty<object>();
            }
        }

        private static void CollectFromObject(object source, List<object> result)
        {
            foreach (var name in SelectionPropertyNames)
            {
                var property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is null)
                    continue;
                if (property.GetIndexParameters().Length > 0)
                    continue;

                var value = property.GetValue(source);
                if (value is null)
                    continue;

                if (value is IEnumerable enumerable && value is not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is null)
                            continue;

                        result.Add(UnwrapItem(item));
                    }
                }
                else
                {
                    result.Add(UnwrapItem(value));
                }
            }
        }

        private static int GetCurrentFrame(object timeline)
        {
            var frame = TimelineMemberReader.GetCurrentFrame(timeline);
            return frame == int.MinValue ? 0 : frame;
        }

        private static object UnwrapItem(object item)
        {
            try
            {
                var itemProperty = item.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (itemProperty is not null && itemProperty.GetIndexParameters().Length == 0)
                {
                    if (itemProperty.GetValue(item) is object inner)
                        return inner;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] UnwrapItem by Item failed: {ex}");
            }

            try
            {
                var modelProperty = item.GetType().GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (modelProperty is not null && modelProperty.GetIndexParameters().Length == 0)
                {
                    if (modelProperty.GetValue(item) is object model)
                        return model;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionService] UnwrapItem by Model failed: {ex}");
            }

            return item;
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





