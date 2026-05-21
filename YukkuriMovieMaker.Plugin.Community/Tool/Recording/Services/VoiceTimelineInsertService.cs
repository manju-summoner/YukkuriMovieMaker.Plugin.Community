using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class VoiceTimelineInsertService
    {
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
                var selectionService = new TimelineSelectionService();
                if (selectionService.TryGetSelectedPlacement(out var frame, out var placementLayer))
                {
                    selectedFrame = frame;
                    selectedLayer = placementLayer;
                }

                if (TryAttachToPreferredVoiceItem(item, selectionService))
                {
                    return;
                }

                if (TryAttachToSelectedVoiceItem(item))
                {
                    return;
                }

                if (TryAttachToMatchingVoiceItem(item, selectionService))
                {
                    return;
                }

                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    InsertWithTimeline(timeline, item, selectedFrame, selectedLayer);
                    return;
                }

                var mainViewModel = Application.Current?.MainWindow?.DataContext
                    ?? throw new InvalidOperationException("MainViewModel を取得できません。");

                var fallbackTimeline = GetActiveTimeline(mainViewModel)
                    ?? throw new InvalidOperationException("タイムラインを取得できません。");

                var currentFrame = selectedFrame ?? GetCurrentFrame(fallbackTimeline);
                var length = GetLengthFrames(fallbackTimeline, item.AudioFilePath);
                var targetLayer = selectedLayer ?? 0;
                var voiceItem = CreateVoiceItemViaReflection(item, currentFrame, length, targetLayer);
                TryAddItem(fallbackTimeline, voiceItem, currentFrame, length, targetLayer);
            }).Task;
        }

        private static bool TryAttachToSelectedVoiceItem(RecordingScriptItem item)
        {
            try
            {
                var selectionService = new TimelineSelectionService();
                var selectedItems = selectionService.GetSelectedItemsSnapshot();
                foreach (var selected in selectedItems)
                {

                    if (!selected.GetType().Name.Contains("Voice", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var serifProp = selected.GetType().GetProperty("Serif", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var currentSerif = serifProp?.GetValue(selected) as string;
                    if (string.IsNullOrWhiteSpace(item.Text) && !string.IsNullOrWhiteSpace(currentSerif))
                    {
                        item.Text = currentSerif;
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Text)
                             && !string.IsNullOrWhiteSpace(currentSerif)
                             && !string.Equals(item.Text, currentSerif, StringComparison.Ordinal))
                    {
                        // Prevent overwriting another serif when selection is stale.
                        continue;
                    }

                    var voiceParameterProp = selected.GetType().GetProperty("VoiceParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    object? targetForParameter = selected;

                    if (voiceParameterProp is null)
                    {
                        var characterProp = selected.GetType().GetProperty("Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var character = characterProp?.GetValue(selected);
                        if (character is not null)
                        {
                            voiceParameterProp = character.GetType().GetProperty("VoiceParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            targetForParameter = character;
                        }
                    }

                    FieldInfo? voiceParameterField = null;
                    if (voiceParameterProp is null)
                    {
                        voiceParameterField = selected.GetType().GetField("voiceParameter", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (voiceParameterField is not null)
                        {
                            targetForParameter = selected;
                        }
                    }

                    if ((voiceParameterProp is null && voiceParameterField is null) || targetForParameter is null)
                    {
                        continue;
                    }

                    var existing = voiceParameterProp is not null
                        ? voiceParameterProp.GetValue(targetForParameter)
                        : voiceParameterField?.GetValue(targetForParameter);
                    var recorded = existing as RecordedVoiceParameter ?? new RecordedVoiceParameter();
                    recorded.Text = item.Text;
                    recorded.AudioFilePath = item.AudioFilePath;
                    recorded.Duration = item.Duration;
                    recorded.CreatedAt = item.CreatedAt;

                    if (voiceParameterProp is not null)
                    {
                        if (voiceParameterProp.CanWrite)
                        {
                            voiceParameterProp.SetValue(targetForParameter, recorded);
                        }
                        else
                        {
                        }
                    }

                    if (voiceParameterField is not null)
                    {
                        voiceParameterField.SetValue(targetForParameter, recorded);
                    }

                    if (serifProp is not null && serifProp.CanWrite && !string.IsNullOrWhiteSpace(item.Text))
                    {
                        var current = serifProp.GetValue(selected) as string;
                        if (string.Equals(current, item.Text, StringComparison.Ordinal))
                        {
                            // Brute-force refresh: toggle once so YMM detects a text change.
                            serifProp.SetValue(selected, item.Text + " ");
                        }
                        serifProp.SetValue(selected, item.Text);
                    }

                    // Force YMM to rebuild voice even when text has not changed.
                    ForceRefreshForVoice(selected, item.Text);
                    if (!ReferenceEquals(targetForParameter, selected) && targetForParameter is not null)
                        ForceRefreshForVoice(targetForParameter, item.Text);

                    UpdateSelectedVoiceItemLength(selected, item);
                    TryRequestVoiceGeneration(selected);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void SetBoolMember(object target, string name, bool value)
        {
            if (SetMemberWithBackingField(target, name, value))
                return;
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

        private static bool TryAttachToMatchingVoiceItem(RecordingScriptItem item, TimelineSelectionService selectionService)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                return false;

            try
            {
                var voiceItems = selectionService.GetVoiceItemsSnapshot();
                foreach (var candidate in voiceItems)
                {
                    var serif = FindProperty(candidate.GetType(), "Serif")?.GetValue(candidate) as string;
                    if (!string.Equals(serif, item.Text, StringComparison.Ordinal))
                        continue;

                    var voiceParameter = FindProperty(candidate.GetType(), "VoiceParameter")?.GetValue(candidate);
                    if (voiceParameter is RecordedVoiceParameter rp)
                    {
                        var audio = rp.AudioFilePath ?? string.Empty;
                        if (!(string.IsNullOrWhiteSpace(audio) || audio.EndsWith("Silent_5s.wav", StringComparison.OrdinalIgnoreCase)))
                            continue;
                    }

                    if (TryAttachToVoiceItemCore(item, candidate))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryAttachToPreferredVoiceItem(RecordingScriptItem item, TimelineSelectionService selectionService)
        {
            if (!TimelineSelectionService.TryGetPreferredVoiceTarget(out var frame, out var layer, out var serif))
                return false;

            // If timeline cursor is far away from preferred target, treat it as stale context.
            // This avoids accidental overwrite after user manually navigated elsewhere.
            var timeline = ToolViewModel.TimelineInstance;
            if (timeline is not null)
            {
                var currentFrame = ToInt(FindProperty(timeline.GetType(), "CurrentFrame")?.GetValue(timeline));
                if (Math.Abs(currentFrame - frame) > 2)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(item.Text)
                && !string.IsNullOrWhiteSpace(serif)
                && !string.Equals(item.Text, serif, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Text) && !string.IsNullOrWhiteSpace(serif))
                item.Text = serif;

            try
            {
                var voiceItems = selectionService.GetVoiceItemsSnapshot();
                foreach (var candidate in voiceItems)
                {
                    var candidateFrame = ToInt(FindProperty(candidate.GetType(), "Frame")?.GetValue(candidate));
                    var candidateLayer = ToInt(FindProperty(candidate.GetType(), "Layer")?.GetValue(candidate));
                    if (candidateFrame != frame || candidateLayer != layer)
                        continue;

                    if (!string.IsNullOrWhiteSpace(serif))
                    {
                        var candidateSerif = FindProperty(candidate.GetType(), "Serif")?.GetValue(candidate) as string;
                        if (!string.Equals(candidateSerif, serif, StringComparison.Ordinal))
                            continue;
                    }

                    if (TryAttachToVoiceItemCore(item, candidate))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryAttachToVoiceItemCore(RecordingScriptItem item, object selected)
        {
            try
            {
                var serifProp = selected.GetType().GetProperty("Serif", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                var voiceParameterProp = selected.GetType().GetProperty("VoiceParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object? targetForParameter = selected;

                if (voiceParameterProp is null)
                {
                    var characterProp = selected.GetType().GetProperty("Character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var character = characterProp?.GetValue(selected);
                    if (character is not null)
                    {
                        voiceParameterProp = character.GetType().GetProperty("VoiceParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        targetForParameter = character;
                    }
                }

                FieldInfo? voiceParameterField = null;
                if (voiceParameterProp is null)
                {
                    voiceParameterField = selected.GetType().GetField("voiceParameter", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (voiceParameterField is not null)
                        targetForParameter = selected;
                }

                if ((voiceParameterProp is null && voiceParameterField is null) || targetForParameter is null)
                    return false;

                var existing = voiceParameterProp is not null
                    ? voiceParameterProp.GetValue(targetForParameter)
                    : voiceParameterField?.GetValue(targetForParameter);
                var recorded = existing as RecordedVoiceParameter ?? new RecordedVoiceParameter();
                recorded.Text = item.Text;
                recorded.AudioFilePath = item.AudioFilePath;
                recorded.Duration = item.Duration;
                recorded.CreatedAt = item.CreatedAt;

                if (voiceParameterProp is not null && voiceParameterProp.CanWrite)
                    voiceParameterProp.SetValue(targetForParameter, recorded);
                if (voiceParameterField is not null)
                    voiceParameterField.SetValue(targetForParameter, recorded);

                if (serifProp is not null && serifProp.CanWrite && !string.IsNullOrWhiteSpace(item.Text))
                    serifProp.SetValue(selected, item.Text);

                ForceRefreshForVoice(selected, item.Text);
                if (!ReferenceEquals(targetForParameter, selected) && targetForParameter is not null)
                    ForceRefreshForVoice(targetForParameter, item.Text);

                UpdateSelectedVoiceItemLength(selected, item);
                TryRequestVoiceGeneration(selected);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ForceRefreshForVoice(object target, string text)
        {
            if (TryGetStringMember(target, "Hatsuon", out var hatsuon) && string.Equals(hatsuon, text, StringComparison.Ordinal))
            {
                SetMemberWithBackingField(target, "Hatsuon", text + " ");
            }

            SetBoolMember(target, "IsVoiceChanged", true);
            SetBoolMember(target, "IsHatsuonChanged", true);
            SetBoolMember(target, "isCharacterChanged", true);
            SetBoolMember(target, "isHatsuonChanged", true);
            SetBoolMember(target, "IsVoiceChanged", false);
            SetBoolMember(target, "IsVoiceChanged", true);
            SetMemberWithBackingField(target, "Hatsuon", text);
            SetMemberWithBackingField(target, "VoiceCache", null);
            SetMemberWithBackingField(target, "voiceCache", null);
            SetMemberWithBackingField(target, "FilePath", null);
            SetMemberWithBackingField(target, "customVoiceFilePath", null);
            SetMemberWithBackingField(target, "voiceFile", null);
            SetMemberWithBackingField(target, "VoiceLength", TimeSpan.Zero);
            SetMemberWithBackingField(target, "voiceLength", TimeSpan.Zero);
        }

        private static void UpdateSelectedVoiceItemLength(object selected, RecordingScriptItem item)
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

                var updatedLength = SetMemberWithBackingField(selected, "Length", frames);
                var updatedVoiceLength = SetMemberWithBackingField(selected, "VoiceLength", duration);
                var updatedContentLength = SetMemberWithBackingField(selected, "ContentLength", contentDuration);
                var updatedOriginalContentLength = SetMemberWithBackingField(selected, "OriginalContentLength", contentDuration);
            }
            catch
            {
            }
        }

        private static TimeSpan ResolveTailPadding(object selected, double fps)
        {
            // Default to a small tail so the subtitle does not cut immediately at voice end.
            var defaultPadding = TimeSpan.FromMilliseconds(300);

            try
            {
                var fromCharacter = TryGetCharacterTailPadding(selected, fps, out var characterSource);
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

        private static TimeSpan? TryGetCharacterTailPadding(object selected, double fps, out string source)
        {
            source = string.Empty;
            var character = FindProperty(selected.GetType(), "Character")?.GetValue(selected)
                ?? FindField(selected.GetType(), "character")?.GetValue(selected);
            if (character is null)
                return null;

            string[] candidateTokens =
            {
                "Wait", "Pause", "Tail", "Post", "After", "Padding", "Margin", "Blank", "Silence", "Delay"
            };

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var prop in character.GetType().GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                if (!LooksLikeTailSettingName(prop.Name, candidateTokens))
                    continue;

                if (TryConvertToPadding(prop.GetValue(character), prop.Name, fps, out var padding))
                {
                    source = $"Property:{prop.Name}";
                    return padding;
                }
            }

            foreach (var field in character.GetType().GetFields(flags))
            {
                if (!LooksLikeTailSettingName(field.Name, candidateTokens))
                    continue;

                if (TryConvertToPadding(field.GetValue(character), field.Name, fps, out var padding))
                {
                    source = $"Field:{field.Name}";
                    return padding;
                }
            }

            return null;
        }

        private static bool LooksLikeTailSettingName(string memberName, string[] tokens)
        {
            // Avoid matching clearly unrelated timing names.
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

            if (name.Contains("Ms", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Milli", StringComparison.OrdinalIgnoreCase))
            {
                padding = TimeSpan.FromMilliseconds(numeric);
                return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
            }

            if (name.Contains("Sec", StringComparison.OrdinalIgnoreCase))
            {
                padding = TimeSpan.FromSeconds(numeric);
                return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
            }

            // Heuristic fallback:
            // - <= 10 : seconds
            // - > 10  : milliseconds
            padding = numeric <= 10
                ? TimeSpan.FromSeconds(numeric)
                : TimeSpan.FromMilliseconds(numeric);

            return padding > TimeSpan.Zero && padding <= TimeSpan.FromSeconds(10);
        }

        private static double ResolveFpsForSelectedItem(object selected)
        {
            try
            {
                if (ToolViewModel.TimelineInstance is { } timeline)
                {
                    var fpsFromTimeline = GetTimelineFps(timeline, fallbackFps: 0);
                    if (fpsFromTimeline > 0)
                        return fpsFromTimeline;
                }
            }
            catch
            {
            }

            var fps = GetMemberDouble(selected, "videoFPS");
            if (fps > 0)
                return fps;

            fps = GetMemberDouble(selected, "FPS");
            if (fps > 0)
                return fps;

            fps = GetMemberDouble(selected, "FrameRate");
            if (fps > 0)
                return fps;

            return 60.0;
        }

        private static double GetMemberDouble(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                var value = ToDouble(prop.GetValue(target));
                if (value > 0)
                    return value;
            }

            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                var value = ToDouble(field.GetValue(target));
                if (value > 0)
                    return value;
            }

            return 0;
        }

        private static int GetMemberInt(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                var value = ToInt(prop.GetValue(target));
                if (value > 0)
                    return value;
            }

            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                var value = ToInt(field.GetValue(target));
                if (value > 0)
                    return value;
            }

            return 0;
        }

        private static TimeSpan? GetMemberTimeSpan(object target, string name)
        {
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                var value = prop.GetValue(target);
                if (value is TimeSpan ts)
                    return ts;
            }

            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                var value = field.GetValue(target);
                if (value is TimeSpan ts)
                    return ts;
            }

            return null;
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

        private static void TryRequestVoiceGeneration(object selected)
        {
            try
            {
                if (TryInvokeVoiceGenerationMethod(selected))
                    return;

                if (TryExecuteVoiceGenerationCommand(selected))
                    return;
            }
            catch
            {
            }
        }

        private static bool TryInvokeVoiceGenerationMethod(object target)
        {
            string[] candidateNames =
            {
                "CreateVoiceFileAsync",
                "CreateVoiceAsync",
                "UpdateVoiceAsync",
                "RegenerateVoiceAsync",
                "RefreshVoiceAsync"
            };

            foreach (var name in candidateNames)
            {
                var method = FindInvokableMethod(target.GetType(), name);
                if (method is null)
                    continue;

                var args = BuildArguments(method.GetParameters());
                if (args is null)
                    continue;

                var result = method.Invoke(target, args);
                if (result is Task task)
                {
                    _ = task.ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted)
                            {
                                var ex = t.Exception?.GetBaseException()
                                         ?? (Exception?)t.Exception
                                         ?? new Exception("Unknown task exception");
                            }
                        },
                        TaskScheduler.Default);
                }
                return true;
            }

            return false;
        }

        private static bool TryExecuteVoiceGenerationCommand(object target)
        {
            string[] candidateNames =
            {
                "CreateVoiceCommand",
                "RegenerateVoiceCommand",
                "RefreshVoiceCommand"
            };

            foreach (var name in candidateNames)
            {
                var prop = FindProperty(target.GetType(), name);
                if (prop?.GetValue(target) is not ICommand command)
                    continue;

                if (!command.CanExecute(null))
                    continue;

                command.Execute(null);
                return true;
            }

            return false;
        }

        private static MethodInfo? FindInvokableMethod(Type type, string methodName)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in methods)
                {
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;
                    if (method.ReturnType != typeof(void) && !typeof(Task).IsAssignableFrom(method.ReturnType))
                        continue;
                    return method;
                }
            }

            return null;
        }

        private static object?[]? BuildArguments(ParameterInfo[] parameters)
        {
            if (parameters.Length == 0)
                return Array.Empty<object?>();

            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.IsOptional)
                {
                    args[i] = p.DefaultValue;
                    continue;
                }

                if (p.ParameterType == typeof(CancellationToken))
                {
                    args[i] = CancellationToken.None;
                    continue;
                }

                if (p.ParameterType == typeof(bool))
                {
                    args[i] = p.Name?.Contains("force", StringComparison.OrdinalIgnoreCase) == true;
                    continue;
                }

                return null;
            }

            return args;
        }

        private static bool TryGetStringMember(object target, string name, out string? value)
        {
            value = null;
            var prop = FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                value = prop.GetValue(target) as string;
                return true;
            }

            var field = FindField(target.GetType(), name);
            if (field is not null)
            {
                value = field.GetValue(target) as string;
                return true;
            }

            return false;
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

        private static void InsertWithTimeline(Timeline timeline, RecordingScriptItem item, int? selectedFrame, int? selectedLayer)
        {
            var frame = selectedFrame ?? timeline.CurrentFrame;
            var layer = selectedLayer ?? 0;
            var length = GetLengthFrames(timeline, item.AudioFilePath);
            var voiceItem = CreateVoiceItem(item, frame, length, layer);

            var added = timeline.TryAddItems(new IItem[] { voiceItem }, voiceItem.Frame, voiceItem.Layer);
            if (!added)
                throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
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

            var voiceItem = new VoiceItem(character)
            {
                Serif = item.Text,
                VoiceParameter = parameter,
                Frame = frame,
                Layer = layer,
                Length = length
            };

            return voiceItem;
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            return reader.TotalTime;
        }

        private static int GetLengthFrames(object timeline, string filePath)
        {
            var fps = GetTimelineFps(timeline, fallbackFps: 60.0);
            var durationSeconds = GetAudioDuration(filePath).TotalSeconds;
            var frames = (int)Math.Round(durationSeconds * fps, MidpointRounding.AwayFromZero);
            return Math.Max(1, frames);
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
            catch
            {
            }

            return fallbackFps;
        }

        private static double GetPropertyDouble(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
                return 0;

            var value = property.GetValue(instance);
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

        private static object? GetActiveTimeline(object mainViewModel)
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

        private static int GetCurrentFrame(object timeline)
        {
            return (int)(timeline.GetType()
                .GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(timeline)
                ?? throw new InvalidOperationException("現在フレームを取得できません。"));
        }

        private static object CreateVoiceItemViaReflection(RecordingScriptItem item, int frame, int length, int layer)
        {
            var voiceItemType = Type.GetType("YukkuriMovieMaker.Project.Items.VoiceItem, YukkuriMovieMaker")
                ?? throw new InvalidOperationException("VoiceItem を取得できません。");

            var parameter = new RecordedVoiceParameter
            {
                Text = item.Text,
                AudioFilePath = item.AudioFilePath,
                Duration = item.Duration,
                CreatedAt = item.CreatedAt
            };

            var characterType = Type.GetType("YukkuriMovieMaker.Project.Character, YukkuriMovieMaker")
                ?? throw new InvalidOperationException("Character を取得できません。");
            var character = Activator.CreateInstance(characterType)
                ?? throw new InvalidOperationException("Character を設定できません。");

            var voiceDescriptionType = Type.GetType("YukkuriMovieMaker.Plugin.Voice.VoiceDescription, YukkuriMovieMaker.Plugin")
                ?? throw new InvalidOperationException("VoiceDescription を取得できません。");
            var speaker = RecordedVoiceSpeaker.Instance;
            var voiceDescription = Activator.CreateInstance(voiceDescriptionType, speaker)
                ?? throw new InvalidOperationException("VoiceDescription を設定できません。");
            var apiProp = voiceDescriptionType.GetProperty("API");
            if (apiProp?.CanWrite == true)
                apiProp.SetValue(voiceDescription, RecordedVoiceSpeaker.ApiName);
            var argProp = voiceDescriptionType.GetProperty("Arg");
            if (argProp?.CanWrite == true)
                argProp.SetValue(voiceDescription, RecordedVoiceSpeaker.SpeakerId);

            characterType.GetProperty("Voice")?.SetValue(character, voiceDescription);
            characterType.GetProperty("VoiceParameter")?.SetValue(character, parameter.Clone());

            var voiceItem = Activator.CreateInstance(voiceItemType, character)
                ?? throw new InvalidOperationException("VoiceItem を設定できません。");

            voiceItemType.GetProperty("Serif")?.SetValue(voiceItem, item.Text);
            voiceItemType.GetProperty("VoiceParameter")?.SetValue(voiceItem, parameter);
            voiceItemType.GetProperty("Frame")?.SetValue(voiceItem, frame);
            voiceItemType.GetProperty("Layer")?.SetValue(voiceItem, layer);
            voiceItemType.GetProperty("Length")?.SetValue(voiceItem, length);
            var voiceLengthProp = voiceItemType.GetProperty("VoiceLength");
            if (voiceLengthProp?.CanWrite == true)
                voiceLengthProp.SetValue(voiceItem, item.Duration ?? GetAudioDuration(item.AudioFilePath));

            return voiceItem;
        }

        private static void TryAddItem(object timeline, object voiceItem, int frame, int length, int layer)
        {
            var timelineType = timeline.GetType();
            var itemInterfaceType = timelineType.Assembly.GetType("YukkuriMovieMaker.Project.Items.IItem")
                ?? throw new InvalidOperationException("IItem を取得できません。");

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
                    throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
                return;
            }

            var addItems = timelineType.GetMethod("AddItems", BindingFlags.Instance | BindingFlags.Public);
            if (addItems is null)
                throw new InvalidOperationException("タイムライン追加メソッドを取得できません。");

            addItems.Invoke(timeline, new object[] { itemArray });
        }
    }
}




