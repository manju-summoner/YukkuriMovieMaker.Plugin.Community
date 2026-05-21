using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class TimelineSelectionApplier
    {
        public static bool TrySetSelection(object item, string[] selectionPropertyNames)
        {
            var timeline = ToolViewModel.TimelineInstance;
            if (timeline is not null && TrySetSelectionOnObject(timeline, item, selectionPropertyNames))
                return true;

            var mainViewModel = Application.Current?.MainWindow?.DataContext;
            if (mainViewModel is not null)
            {
                if (TrySetSelectionOnObject(mainViewModel, item, selectionPropertyNames))
                    return true;

                var activeTimelineViewModel = mainViewModel.GetType()
                    .GetProperty("ActiveTimelineViewModel", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(mainViewModel);
                if (activeTimelineViewModel is not null && TrySetSelectionOnObject(activeTimelineViewModel, item, selectionPropertyNames))
                    return true;
            }
            return false;
        }

        public static bool TryMoveCurrentFrame(object item)
        {
            try
            {
                if (!TimelineMemberReader.TryGetIntProperty(item, "Frame", out var frame))
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineSelectionApplier] TryMoveCurrentFrame failed: {ex}");
                return false;
            }
        }

        private static bool TrySetSelectionOnObject(object source, object item, string[] selectionPropertyNames)
        {
            foreach (var name in selectionPropertyNames)
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
                        return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TimelineSelectionApplier] TrySetSelectionOnObject failed for '{name}': {ex}");
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
    }
}
