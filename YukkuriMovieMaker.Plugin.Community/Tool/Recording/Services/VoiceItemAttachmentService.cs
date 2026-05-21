using System;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceItemAttachmentService
    {
        public bool TryAttach(RecordingScriptItem item, TimelineSelectionService selectionService, VoiceTargetResolverService targetResolver)
        {
            return TryAttachToPreferredVoiceItem(item, selectionService, targetResolver)
                || TryAttachToSelectedVoiceItem(item, selectionService, targetResolver)
                || TryAttachToMatchingVoiceItem(item, selectionService, targetResolver);
        }

        private static bool TryAttachToSelectedVoiceItem(RecordingScriptItem item, TimelineSelectionService selectionService, VoiceTargetResolverService targetResolver)
        {
            try
            {
                foreach (var selected in targetResolver.ResolveSelectedCandidates(item, selectionService))
                {
                    var serifProp = selected.GetType().GetProperty("Serif", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!VoiceParameterBindingService.TryBindRecordedParameter(selected, item, out var targetForParameter) || targetForParameter is null)
                        continue;

                    if (serifProp is not null && serifProp.CanWrite && !string.IsNullOrWhiteSpace(item.Text))
                    {
                        var current = serifProp.GetValue(selected) as string;
                        if (string.Equals(current, item.Text, StringComparison.Ordinal))
                            serifProp.SetValue(selected, item.Text + " ");
                        serifProp.SetValue(selected, item.Text);
                    }

                    VoiceRegenerationService.RefreshAndRequest(selected, item.Text);
                    if (!ReferenceEquals(targetForParameter, selected) && targetForParameter is not null)
                        VoiceRegenerationService.RefreshAndRequest(targetForParameter, item.Text);

                    VoiceLengthAdjustService.UpdateSelectedVoiceItemLength(selected, item);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryAttachToMatchingVoiceItem(RecordingScriptItem item, TimelineSelectionService selectionService, VoiceTargetResolverService targetResolver)
        {
            try
            {
                foreach (var candidate in targetResolver.ResolveMatchingCandidates(item, selectionService))
                {
                    if (TryAttachToVoiceItemCore(item, candidate))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryAttachToPreferredVoiceItem(RecordingScriptItem item, TimelineSelectionService selectionService, VoiceTargetResolverService targetResolver)
        {
            try
            {
                if (!targetResolver.TryResolvePreferredCandidate(item, selectionService, out var candidate) || candidate is null)
                    return false;

                return TryAttachToVoiceItemCore(item, candidate);
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

                if (!VoiceParameterBindingService.TryBindRecordedParameter(selected, item, out var targetForParameter) || targetForParameter is null)
                    return false;

                if (serifProp is not null && serifProp.CanWrite && !string.IsNullOrWhiteSpace(item.Text))
                    serifProp.SetValue(selected, item.Text);

                VoiceRegenerationService.RefreshAndRequest(selected, item.Text);
                if (!ReferenceEquals(targetForParameter, selected) && targetForParameter is not null)
                    VoiceRegenerationService.RefreshAndRequest(targetForParameter, item.Text);

                VoiceLengthAdjustService.UpdateSelectedVoiceItemLength(selected, item);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
