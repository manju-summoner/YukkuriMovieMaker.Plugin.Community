using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal sealed class VoiceGenerationRequestService
    {
        public bool TryRequest(object target)
        {
            if (TryInvokeVoiceGenerationMethod(target))
                return true;

            return TryExecuteVoiceGenerationCommand(target);
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
                    _ = task.ContinueWith(_ => { }, TaskScheduler.Default);

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
                var prop = VoiceReflectionHelper.FindProperty(target.GetType(), name);
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
    }
}
