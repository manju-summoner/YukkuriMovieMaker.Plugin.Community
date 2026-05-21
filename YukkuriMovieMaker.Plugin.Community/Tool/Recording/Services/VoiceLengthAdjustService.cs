using System;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceLengthAdjustService
    {
        public static void UpdateSelectedVoiceItemLength(object selected, RecordingScriptItem item)
        {
            try
            {
                var duration = item.Duration.HasValue && item.Duration.Value > TimeSpan.Zero
                    ? item.Duration.Value
                    : GetAudioDuration(item.AudioFilePath);
                item.Duration = duration;

                var fps = ResolveFpsForSelectedItem(selected);
                var tailPadding = ResolveTailPadding(selected, fps);
                var contentDuration = duration + tailPadding;
                var frames = Math.Max(1, (int)Math.Round(contentDuration.TotalSeconds * fps, MidpointRounding.AwayFromZero));

                _ = SetMemberWithBackingField(selected, "Length", frames);
                _ = SetMemberWithBackingField(selected, "VoiceLength", duration);
                _ = SetMemberWithBackingField(selected, "ContentLength", contentDuration);
                _ = SetMemberWithBackingField(selected, "OriginalContentLength", contentDuration);
            }
            catch
            {
            }
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            using var reader = new NAudio.Wave.WaveFileReader(stream);
            return reader.TotalTime;
        }

        private static TimeSpan ResolveTailPadding(object selected, double fps)
        {
            var defaultPadding = TimeSpan.FromMilliseconds(300);

            try
            {
                var fromCharacter = TryGetCharacterTailPadding(selected, fps);
                if (fromCharacter.HasValue && fromCharacter.Value > TimeSpan.Zero)
                {
                    var clamped = fromCharacter.Value < defaultPadding ? defaultPadding : fromCharacter.Value;
                    return clamped;
                }

                var currentVoiceLength = GetMemberTimeSpan(selected, "VoiceLength")
                    ?? GetMemberTimeSpan(selected, "voiceLength")
                    ?? TimeSpan.Zero;

                var currentContentLength = GetMemberTimeSpan(selected, "ContentLength")
                    ?? GetMemberTimeSpan(selected, "OriginalContentLength")
                    ?? TimeSpan.Zero;

                if (currentContentLength <= TimeSpan.Zero)
                {
                    var currentFrames = GetMemberInt(selected, "Length");
                    if (currentFrames > 0 && fps > 0)
                        currentContentLength = TimeSpan.FromSeconds(currentFrames / fps);
                }

                var existingPadding = currentContentLength - currentVoiceLength;
                if (existingPadding > TimeSpan.Zero && existingPadding <= TimeSpan.FromSeconds(2))
                    return existingPadding;
            }
            catch
            {
            }

            return defaultPadding;
        }

        private static TimeSpan? TryGetCharacterTailPadding(object selected, double fps)
        {
            var character = FindProperty(selected.GetType(), "Character")?.GetValue(selected)
                ?? FindField(selected.GetType(), "character")?.GetValue(selected);
            if (character is null)
                return null;

            string[] candidateTokens = { "Wait", "Pause", "Tail", "Post", "After", "Padding", "Margin", "Blank", "Silence", "Delay" };
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var prop in character.GetType().GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                if (!LooksLikeTailSettingName(prop.Name, candidateTokens))
                    continue;
                if (TryConvertToPadding(prop.GetValue(character), prop.Name, fps, out var padding))
                    return padding;
            }

            foreach (var field in character.GetType().GetFields(flags))
            {
                if (!LooksLikeTailSettingName(field.Name, candidateTokens))
                    continue;
                if (TryConvertToPadding(field.GetValue(character), field.Name, fps, out var padding))
                    return padding;
            }

            return null;
        }

        private static bool LooksLikeTailSettingName(string memberName, string[] tokens)
        {
            if (memberName.Contains("Fade", StringComparison.OrdinalIgnoreCase))
                return false;

            var hitCore = false;
            foreach (var token in tokens)
            {
                if (memberName.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    hitCore = true;
                    break;
                }
            }

            if (!hitCore)
                return false;

            return memberName.Contains("Wait", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Pause", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Padding", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Margin", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Interval", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Blank", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Silence", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Delay", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("After", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Post", StringComparison.OrdinalIgnoreCase)
                || memberName.Contains("Tail", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryConvertToPadding(object? raw, string name, double fps, out TimeSpan padding)
        {
            padding = TimeSpan.Zero;
            if (raw is null)
                return false;

            if (raw is TimeSpan ts)
            {
                if (ts > TimeSpan.Zero && ts <= TimeSpan.FromSeconds(10))
                {
                    padding = ts;
                    return true;
                }
                return false;
            }

            var numeric = ToDouble(raw);
            if (numeric <= 0)
                return false;

            if (name.Contains("Frame", StringComparison.OrdinalIgnoreCase) && fps > 0)
            {
                padding = TimeSpan.FromSeconds(numeric / fps);
                return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
            }
            if (name.Contains("Ms", StringComparison.OrdinalIgnoreCase) || name.Contains("Milli", StringComparison.OrdinalIgnoreCase))
            {
                padding = TimeSpan.FromMilliseconds(numeric);
                return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
            }
            if (name.Contains("Sec", StringComparison.OrdinalIgnoreCase))
            {
                padding = TimeSpan.FromSeconds(numeric);
                return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
            }

            padding = numeric <= 10 ? TimeSpan.FromSeconds(numeric) : TimeSpan.FromMilliseconds(numeric);
            return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
        }

        private static double ResolveFpsForSelectedItem(object selected)
        {
            try
            {
                if (ToolViewModel.TimelineInstance is { } timeline)
                {
                    var fpsFromTimeline = GetTimelineFps(timeline, 0);
                    if (fpsFromTimeline > 0)
                        return fpsFromTimeline;
                }
            }
            catch { }

            var fps = GetMemberDouble(selected, "videoFPS");
            if (fps > 0) return fps;
            fps = GetMemberDouble(selected, "FPS");
            if (fps > 0) return fps;
            fps = GetMemberDouble(selected, "FrameRate");
            if (fps > 0) return fps;
            return 60.0;
        }

        private static double GetTimelineFps(object timeline, double fallbackFps)
        {
            try
            {
                var videoInfoProperty = timeline.GetType().GetProperty("VideoInfo", BindingFlags.Public | BindingFlags.Instance);
                var videoInfo = videoInfoProperty?.GetValue(timeline);
                if (videoInfo is not null)
                {
                    var fpsFromVideoInfo = GetPropertyDouble(videoInfo, "FPS");
                    if (fpsFromVideoInfo > 0)
                        return fpsFromVideoInfo;
                }

                var fps = GetPropertyDouble(timeline, "FPS");
                if (fps > 0)
                    return fps;

                fps = GetPropertyDouble(timeline, "FrameRate");
                if (fps > 0)
                    return fps;
            }
            catch { }
            return fallbackFps;
        }

        private static double GetPropertyDouble(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null) return 0;
            return ToDouble(property.GetValue(instance));
        }

        private static double GetMemberDouble(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                var value = ToDouble(prop.GetValue(target));
                if (value > 0) return value;
            }
            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                var value = ToDouble(field.GetValue(target));
                if (value > 0) return value;
            }
            return 0;
        }

        private static int GetMemberInt(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                var value = ToInt(prop.GetValue(target));
                if (value > 0) return value;
            }
            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                var value = ToInt(field.GetValue(target));
                if (value > 0) return value;
            }
            return 0;
        }

        private static TimeSpan? GetMemberTimeSpan(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null && prop.GetValue(target) is TimeSpan tsProp)
                return tsProp;
            var field = FindField(target.GetType(), name);
            if (field is not null && field.GetValue(target) is TimeSpan tsField)
                return tsField;
            return null;
        }

        private static bool SetMemberWithBackingField(object target, string name, object? value)
        {
            if (SetMember(target, name, value))
                return true;

            var backingField = FindField(target.GetType(), $"<{name}>k__BackingField");
            if (backingField is null)
                return false;

            if (value is null)
            {
                backingField.SetValue(target, null);
                return true;
            }

            if (backingField.FieldType.IsInstanceOfType(value))
            {
                backingField.SetValue(target, value);
                return true;
            }

            try
            {
                var converted = Convert.ChangeType(value, backingField.FieldType);
                backingField.SetValue(target, converted);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SetMember(object target, string name, object? value)
        {
            var type = target.GetType();
            var prop = FindProperty(type, name);
            if (prop is not null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return true;
            }
            var field = FindField(type, name);
            if (field is not null)
            {
                field.SetValue(target, value);
                return true;
            }
            return false;
        }

        private static double ToDouble(object? value)
        {
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }

        private static int ToInt(object? value)
        {
            return value switch
            {
                int i => i,
                long l => l > int.MaxValue ? int.MaxValue : (int)l,
                short s => s,
                byte b => b,
                double d => (int)Math.Round(d, MidpointRounding.AwayFromZero),
                float f => (int)Math.Round(f, MidpointRounding.AwayFromZero),
                decimal m => (int)Math.Round(m, MidpointRounding.AwayFromZero),
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }

        private static PropertyInfo? FindProperty(Type type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var prop = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop is not null)
                    return prop;
            }
            return null;
        }

        private static FieldInfo? FindField(Type type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                    return field;
            }
            return null;
        }
    }
}
