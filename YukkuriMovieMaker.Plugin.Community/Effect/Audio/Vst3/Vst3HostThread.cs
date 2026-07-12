using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3HostThread
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        static readonly object gate = new();
        static Dispatcher? fallbackDispatcher;

        public static bool CheckAccess()
        {
            var dispatcher = Application.Current?.Dispatcher ?? GetFallbackDispatcher();
            return dispatcher.CheckAccess();
        }

        public static void Post(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher ?? GetFallbackDispatcher();
            dispatcher.BeginInvoke(priority, action);
        }

        public static void Invoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher ?? GetFallbackDispatcher();
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            var operation = dispatcher.InvokeAsync(action);
            bool completed;
            try
            {
                completed = operation.Task.Wait(Timeout);
            }
            catch (AggregateException e) when (e.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
            if (completed)
                return;

            if (operation.Abort())
                throw new TimeoutException("VST3 plugin did not respond.");

            try
            {
                operation.Task.GetAwaiter().GetResult();
            }
            catch (AggregateException e) when (e.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        static Dispatcher GetFallbackDispatcher()
        {
            lock (gate)
            {
                if (fallbackDispatcher is not null)
                    return fallbackDispatcher;

                using var ready = new ManualResetEventSlim();
                Dispatcher? created = null;
                var thread = new Thread(() =>
                {
                    created = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "VST3 Host",
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();
                fallbackDispatcher = created!;
                return fallbackDispatcher;
            }
        }
    }
}
