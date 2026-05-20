using System;
using System.Collections;
using System.Collections.Generic;
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
                var serif = GetStringProperty(item, "Serif")
                            ?? GetStringProperty(item, "Text");
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
                if (TryGetIntProperty(item, "Frame", out var foundFrame))
                {
                    frame = foundFrame;
                    if (!TryGetIntProperty(item, "Layer", out layer))
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
                    .Where(IsVoiceLikeItem)
                    .Distinct()
                    .FirstOrDefault();
                if (selected is null)
                {
                    return false;
                }

                var voiceItems = CollectVoiceItems(timeline)
                    .OrderBy(i => GetIntMember(i, "Frame"))
                    .ThenBy(i => GetIntMember(i, "Layer"))
                    .ToList();
                if (voiceItems.Count == 0)
                {
                    return false;
                }

                var currentFrame = GetCurrentFrame(timeline);
                var currentIndex = FindCurrentIndex(voiceItems, selected, currentSerif, currentFrame);
                if (currentIndex < 0 || currentIndex >= voiceItems.Count - 1)
                {
                    ClearPreferredVoiceTarget();
                    return false;
                }

                var next = voiceItems[currentIndex + 1];
                var serif = GetStringProperty(next, "Serif") ?? GetStringProperty(next, "Text");
                if (string.IsNullOrWhiteSpace(serif))
                {
                    return false;
                }

                SetPreferredVoiceTarget(next, serif);
                nextSerif = serif;
                var selectionMoved = false;
                try
                {
                    selectionMoved = TrySetSelection(next);
                }
                catch
                {
                }

                var frameMoved = TryMoveCurrentFrame(next);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetPreferredVoiceTarget(object item, string serif)
        {
            var frame = GetIntMember(item, "Frame");
            var layer = GetIntMember(item, "Layer");
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
            catch
            {
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

                return CollectVoiceItems(timeline)
                    .OrderBy(i => GetIntMember(i, "Frame"))
                    .ThenBy(i => GetIntMember(i, "Layer"))
                    .ToList();
            }
            catch
            {
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

        private static bool TrySetSelection(object item)
        {
            var timeline = ToolViewModel.TimelineInstance;
            if (timeline is not null && TrySetSelectionOnObject(timeline, item))
                return true;

            var mainViewModel = Application.Current?.MainWindow?.DataContext;
            if (mainViewModel is not null)
            {
                if (TrySetSelectionOnObject(mainViewModel, item))
                    return true;

                var activeTimelineViewModel = mainViewModel.GetType()
                    .GetProperty("ActiveTimelineViewModel", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(mainViewModel);
                if (activeTimelineViewModel is not null && TrySetSelectionOnObject(activeTimelineViewModel, item))
                    return true;
            }
            return false;
        }

        private static bool TryMoveCurrentFrame(object item)
        {
            try
            {
                if (!TryGetIntProperty(item, "Frame", out var frame))
                    return false;

                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is null)
                    return false;

                var frameProp = timeline.GetType().GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (frameProp?.CanWrite != true)
                    return false;

                frameProp.SetValue(timeline, frame);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetSelectionOnObject(object source, object item)
        {
            foreach (var name in SelectionPropertyNames)
            {
                var property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is null)
                    continue;
                if (property.GetIndexParameters().Length > 0)
                    continue;

                try
                {
                    if (name == "SelectedItem" && property.CanWrite)
                    {
                        if (property.PropertyType.IsInstanceOfType(item) || property.PropertyType == typeof(object))
                        {
                            property.SetValue(source, item);
                            return true;
                        }
                    }

                    var current = property.GetValue(source);
                    if (current is null)
                        continue;

                    if (TryReplaceInCollection(current, item))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TryReplaceInCollection(object collection, object item)
        {
            if (collection is IList list)
            {
                list.Clear();
                list.Add(item);
                return true;
            }

            var clear = collection.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
            var add = collection.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            if (clear is null || add is null)
                return false;

            var parameters = add.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(item))
                return false;

            clear.Invoke(collection, null);
            add.Invoke(collection, new[] { item });
            return true;
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
                catch
                {
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

        private static bool IsVoiceLikeItem(object item)
        {
            var type = item.GetType();
            if (type.Name.Contains("Voice", StringComparison.OrdinalIgnoreCase))
                return true;

            var serif = GetStringProperty(item, "Serif");
            var frame = GetIntMember(item, "Frame");
            var layer = GetIntMember(item, "Layer");
            return serif is not null && (frame != int.MinValue || layer != int.MinValue);
        }

        private static int FindCurrentIndex(IReadOnlyList<object> items, object selected)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], selected))
                    return i;
            }

            var selectedFrame = GetIntMember(selected, "Frame");
            var selectedLayer = GetIntMember(selected, "Layer");
            var selectedSerif = GetStringProperty(selected, "Serif");
            for (var i = 0; i < items.Count; i++)
            {
                if (GetIntMember(items[i], "Frame") == selectedFrame
                    && GetIntMember(items[i], "Layer") == selectedLayer
                    && string.Equals(GetStringProperty(items[i], "Serif"), selectedSerif, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindCurrentIndex(IReadOnlyList<object> items, object selected, string? currentSerif, int currentFrame)
        {
            // 1) Prefer currently selected item if it matches.
            var bySelected = FindCurrentIndex(items, selected);
            if (bySelected >= 0)
            {
                var selectedSerif = GetStringProperty(items[bySelected], "Serif");
                if (string.IsNullOrWhiteSpace(currentSerif) || string.Equals(selectedSerif, currentSerif, StringComparison.Ordinal))
                    return bySelected;
            }

            // 2) Prefer item at current frame + same serif (frame cursor survives when selection move fails).
            if (!string.IsNullOrWhiteSpace(currentSerif))
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (GetIntMember(items[i], "Frame") == currentFrame
                        && string.Equals(GetStringProperty(items[i], "Serif"), currentSerif, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            // 3) Fallback to same serif with nearest frame <= current frame.
            if (!string.IsNullOrWhiteSpace(currentSerif))
            {
                var candidate = -1;
                var bestFrame = int.MinValue;
                for (var i = 0; i < items.Count; i++)
                {
                    if (!string.Equals(GetStringProperty(items[i], "Serif"), currentSerif, StringComparison.Ordinal))
                        continue;

                    var frame = GetIntMember(items[i], "Frame");
                    if (frame <= currentFrame && frame >= bestFrame)
                    {
                        bestFrame = frame;
                        candidate = i;
                    }
                }

                if (candidate >= 0)
                    return candidate;
            }

            // 4) Last resort: original selected-based logic.
            return FindCurrentIndex(items, selected);
        }

        private static int GetIntMember(object instance, string name)
        {
            if (TryGetIntProperty(instance, name, out var value))
                return value;
            return int.MinValue;
        }

        private static int GetCurrentFrame(object timeline)
        {
            try
            {
                var prop = timeline.GetType().GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop is not null && prop.GetIndexParameters().Length == 0)
                {
                    var value = prop.GetValue(timeline);
                    if (TryConvertToInt(value, out var frame))
                        return frame;
                }
            }
            catch
            {
            }

            return 0;
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
            catch
            {
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
            catch
            {
            }

            return item;
        }

        private static string? GetStringProperty(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null && property.GetIndexParameters().Length > 0)
                return null;
            if (property?.GetValue(instance) is string value)
                return value;

            return null;
        }

        private static bool TryGetIntProperty(object instance, string propertyName, out int value)
        {
            value = 0;
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null)
            {
                if (property.GetIndexParameters().Length > 0)
                    return false;
                var raw = property.GetValue(instance);
                if (TryConvertToInt(raw, out value))
                    return true;
            }

            var field = instance.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
            {
                var raw = field.GetValue(instance);
                if (TryConvertToInt(raw, out value))
                    return true;
            }

            return false;
        }

        private static bool TryConvertToInt(object? raw, out int value)
        {
            value = 0;
            if (raw is null)
                return false;

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = (int)l;
                    return true;
                case short s:
                    value = s;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case string str when int.TryParse(str, out var parsed):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private static void DumpObjectShape(string prefix, object instance)
        {
            var type = instance.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                var valueSummary = TryFormatValue(prop, instance);
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var valueSummary = TryFormatValue(field, instance);
            }
        }

        private static string TryFormatValue(PropertyInfo prop, object instance)
        {
            try
            {
                var value = prop.GetValue(instance);
                return SummarizeValue(value);
            }
            catch (Exception ex)
            {
                return $"<error {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private static string TryFormatValue(FieldInfo field, object instance)
        {
            try
            {
                var value = field.GetValue(instance);
                return SummarizeValue(value);
            }
            catch (Exception ex)
            {
                return $"<error {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private static string SummarizeValue(object? value)
        {
            if (value is null)
                return "null";

            if (value is string s)
                return $"\"{s}\" (len={s.Length})";

            if (value is System.Collections.IEnumerable && value is not string)
                return $"[{value.GetType().FullName}]";

            return value.ToString() ?? value.GetType().FullName ?? "<unknown>";
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




