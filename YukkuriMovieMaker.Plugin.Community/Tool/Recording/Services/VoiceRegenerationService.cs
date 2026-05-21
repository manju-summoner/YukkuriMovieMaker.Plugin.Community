using System;
using System.Diagnostics;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceRegenerationService
    {
        private static readonly VoiceRegenerationStateService stateService = new();
        private static readonly VoiceGenerationRequestService requestService = new();

        public static void RefreshAndRequest(object target, string text)
        {
            if (target is null)
                return;

            try
            {
                stateService.RefreshState(target, text);
                _ = requestService.TryRequest(target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VoiceRegenerationService.RefreshAndRequest] failed: {ex}");
            }
        }
    }
}
