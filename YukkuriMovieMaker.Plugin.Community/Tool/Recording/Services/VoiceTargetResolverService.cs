using System;
using System.Collections.Generic;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceTargetResolverService
    {
        public bool TryResolvePreferredCandidate(RecordingScriptItem item, TimelineSelectionService selectionService, out object? candidate)
        {
            candidate = null;
            if (!TimelineSelectionService.TryGetPreferredVoiceTarget(out var frame, out var layer, out var serif))
                return false;

            var timeline = ToolViewModel.TimelineInstance;
            if (timeline is not null)
            {
                var currentFrame = ToInt(FindProperty(timeline.GetType(), "CurrentFrame")?.GetValue(timeline));
                if (Math.Abs(currentFrame - frame) > 2)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(item.Text)
                && !string.IsNullOrWhiteSpace(serif)
                && !string.Equals(item.Text, serif, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Text) && !string.IsNullOrWhiteSpace(serif))
                item.Text = serif;

            foreach (var voiceItem in selectionService.GetVoiceItemsSnapshot())
            {
                var candidateFrame = ToInt(FindProperty(voiceItem.GetType(), "Frame")?.GetValue(voiceItem));
                var candidateLayer = ToInt(FindProperty(voiceItem.GetType(), "Layer")?.GetValue(voiceItem));
                if (candidateFrame != frame || candidateLayer != layer)
                    continue;

                if (!string.IsNullOrWhiteSpace(serif))
                {
                    var candidateSerif = FindProperty(voiceItem.GetType(), "Serif")?.GetValue(voiceItem) as string;
                    if (!string.Equals(candidateSerif, serif, StringComparison.Ordinal))
                        continue;
                }

                candidate = voiceItem;
                return true;
            }

            return false;
        }

        public IEnumerable<object> ResolveSelectedCandidates(RecordingScriptItem item, TimelineSelectionService selectionService)
        {
            foreach (var selected in selectionService.GetSelectedItemsSnapshot())
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
                    continue;
                }

                yield return selected;
            }
        }

        public IEnumerable<object> ResolveMatchingCandidates(RecordingScriptItem item, TimelineSelectionService selectionService)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                yield break;

            foreach (var voiceItem in selectionService.GetVoiceItemsSnapshot())
            {
                var serif = FindProperty(voiceItem.GetType(), "Serif")?.GetValue(voiceItem) as string;
                if (!string.Equals(serif, item.Text, StringComparison.Ordinal))
                    continue;

                var voiceParameter = FindProperty(voiceItem.GetType(), "VoiceParameter")?.GetValue(voiceItem);
                if (voiceParameter is RecordedVoiceParameter rp)
                {
                    var audio = rp.AudioFilePath ?? string.Empty;
                    if (!(string.IsNullOrWhiteSpace(audio) || audio.EndsWith("Silent_5s.wav", StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                yield return voiceItem;
            }
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
    }
}
