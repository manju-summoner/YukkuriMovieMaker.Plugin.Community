using System;
using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceParameterBindingService
    {
        public static bool TryBindRecordedParameter(object selected, RecordingScriptItem item, out object? targetForParameter)
        {
            targetForParameter = null;

            var voiceParameterProp = selected.GetType().GetProperty("VoiceParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            targetForParameter = selected;

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

            return true;
        }
    }
}
