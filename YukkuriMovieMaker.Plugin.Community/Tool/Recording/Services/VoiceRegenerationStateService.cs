using System;
using System.Diagnostics;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal sealed class VoiceRegenerationStateService
    {
        public void RefreshState(object target, string text)
        {
            if (TryGetStringMember(target, "Hatsuon", out var hatsuon) && string.Equals(hatsuon, text, StringComparison.Ordinal))
                SetMemberWithBackingField(target, "Hatsuon", text + " ");

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

        private static void SetBoolMember(object target, string name, bool value)
        {
            _ = SetMemberWithBackingField(target, name, value);
        }

        private static bool SetMemberWithBackingField(object target, string name, object? value)
        {
            if (SetMember(target, name, value))
                return true;

            var backingField = VoiceReflectionHelper.FindField(target.GetType(), $"<{name}>k__BackingField");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[VoiceRegenerationStateService.SetMemberWithBackingField] convert failed for '{name}': {ex}");
                return false;
            }
        }

        private static bool SetMember(object target, string name, object? value)
        {
            var prop = VoiceReflectionHelper.FindProperty(target.GetType(), name);
            if (prop is not null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return true;
            }

            var field = VoiceReflectionHelper.FindField(target.GetType(), name);
            if (field is not null)
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static bool TryGetStringMember(object target, string name, out string? value)
        {
            value = null;
            var prop = VoiceReflectionHelper.FindProperty(target.GetType(), name);
            if (prop is not null)
            {
                value = prop.GetValue(target) as string;
                return true;
            }

            var field = VoiceReflectionHelper.FindField(target.GetType(), name);
            if (field is not null)
            {
                value = field.GetValue(target) as string;
                return true;
            }

            return false;
        }
    }
}
