using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceTimelineNavigator
    {
        public static IReadOnlyList<object> GetSortedVoiceItems(object timeline)
        {
            return CollectVoiceItems(timeline)
                .OrderBy(i => TimelineMemberReader.GetIntMember(i, "Frame"))
                .ThenBy(i => TimelineMemberReader.GetIntMember(i, "Layer"))
                .ToList();
        }

        public static bool IsVoiceLikeItem(object item)
        {
            var type = item.GetType();
            if (type.Name.Contains("Voice", StringComparison.OrdinalIgnoreCase))
                return true;

            var serif = TimelineMemberReader.GetStringProperty(item, "Serif");
            var frame = TimelineMemberReader.GetIntMember(item, "Frame");
            var layer = TimelineMemberReader.GetIntMember(item, "Layer");
            return serif is not null && (frame != int.MinValue || layer != int.MinValue);
        }

        public static int FindCurrentIndex(IReadOnlyList<object> items, object selected, string? currentSerif, int currentFrame)
        {
            var bySelected = FindCurrentIndex(items, selected);
            if (bySelected >= 0)
            {
                var selectedSerif = TimelineMemberReader.GetStringProperty(items[bySelected], "Serif");
                if (string.IsNullOrWhiteSpace(currentSerif) || string.Equals(selectedSerif, currentSerif, StringComparison.Ordinal))
                    return bySelected;
            }

            if (!string.IsNullOrWhiteSpace(currentSerif))
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (TimelineMemberReader.GetIntMember(items[i], "Frame") == currentFrame
                        && string.Equals(TimelineMemberReader.GetStringProperty(items[i], "Serif"), currentSerif, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentSerif))
            {
                var candidate = -1;
                var bestFrame = int.MinValue;
                for (var i = 0; i < items.Count; i++)
                {
                    if (!string.Equals(TimelineMemberReader.GetStringProperty(items[i], "Serif"), currentSerif, StringComparison.Ordinal))
                        continue;

                    var frame = TimelineMemberReader.GetIntMember(items[i], "Frame");
                    if (frame <= currentFrame && frame >= bestFrame)
                    {
                        bestFrame = frame;
                        candidate = i;
                    }
                }

                if (candidate >= 0)
                    return candidate;
            }

            return FindCurrentIndex(items, selected);
        }

        private static int FindCurrentIndex(IReadOnlyList<object> items, object selected)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], selected))
                    return i;
            }

            var selectedFrame = TimelineMemberReader.GetIntMember(selected, "Frame");
            var selectedLayer = TimelineMemberReader.GetIntMember(selected, "Layer");
            var selectedSerif = TimelineMemberReader.GetStringProperty(selected, "Serif");
            for (var i = 0; i < items.Count; i++)
            {
                if (TimelineMemberReader.GetIntMember(items[i], "Frame") == selectedFrame
                    && TimelineMemberReader.GetIntMember(items[i], "Layer") == selectedLayer
                    && string.Equals(TimelineMemberReader.GetStringProperty(items[i], "Serif"), selectedSerif, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static IEnumerable<object> CollectVoiceItems(object root)
        {
            var result = new List<object>();
            var queue = new Queue<(object obj, int depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth > 3)
                    continue;

                foreach (var child in GetChildObjects(current))
                {
                    if (child is null)
                        continue;

                    var unwrapped = UnwrapItem(child);
                    if (IsVoiceLikeItem(unwrapped))
                    {
                        if (!result.Contains(unwrapped))
                            result.Add(unwrapped);
                        continue;
                    }

                    if (depth < 3 && ShouldTraverse(unwrapped))
                        queue.Enqueue((unwrapped, depth + 1));
                }
            }

            return result;
        }

        private static IEnumerable<object?> GetChildObjects(object source)
        {
            foreach (var prop in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                object? value;
                try
                {
                    value = prop.GetValue(source);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VoiceTimelineNavigator] GetChildObjects failed: {ex}");
                    continue;
                }

                if (value is null || value is string)
                    continue;

                if (value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                        yield return item;
                }
                else
                {
                    yield return value;
                }
            }
        }

        private static bool ShouldTraverse(object obj)
        {
            var t = obj.GetType();
            if (t.IsPrimitive || t.IsEnum)
                return false;
            if (obj is string)
                return false;
            var ns = t.Namespace ?? string.Empty;
            if (ns.StartsWith("System", StringComparison.Ordinal))
                return false;
            return true;
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
                Debug.WriteLine($"[VoiceTimelineNavigator] UnwrapItem by Item failed: {ex}");
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
                Debug.WriteLine($"[VoiceTimelineNavigator] UnwrapItem by Model failed: {ex}");
            }

            return item;
        }
    }
}
