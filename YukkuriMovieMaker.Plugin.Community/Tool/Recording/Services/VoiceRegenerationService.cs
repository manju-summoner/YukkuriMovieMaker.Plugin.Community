using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceRegenerationService
    {
        public static void RefreshAndRequest(object target, string text)
        {
            if (target is null)
                return;

            try
            {
                ForceRefreshForVoice(target, text);
                TryRequestVoiceGeneration(target);
            }
            catch
            {
            }
        }

        private static void ForceRefreshForVoice(object target, string text)
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

        private static void TryRequestVoiceGeneration(object target)
        {
            try
            {
                if (TryInvokeVoiceGenerationMethod(target))
                    return;

                _ = TryExecuteVoiceGenerationCommand(target);
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
                    _ = task.ContinueWith(_ => { }, TaskScheduler.Default);
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

        private static void SetBoolMember(object target, string name, bool value)
        {
            _ = SetMemberWithBackingField(target, name, value);
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
    }
}
