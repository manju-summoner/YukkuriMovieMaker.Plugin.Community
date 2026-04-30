using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal static class ShellImageLoader
    {
        record struct WorkItem(Func<ImageSource?> Action, TaskCompletionSource<ImageSource?> Tcs, CancellationToken Token);

        static readonly BlockingCollection<WorkItem> queue = new(new ConcurrentStack<WorkItem>());

        static ShellImageLoader()
        {
            int threadCount = Math.Max(2, Environment.ProcessorCount / 2);
            for (int i = 0; i < threadCount; i++)
            {
                var thread = new Thread(Work) { IsBackground = true };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        static void Work()
        {
            foreach (var item in queue.GetConsumingEnumerable())
            {
                if (item.Token.IsCancellationRequested)
                {
                    item.Tcs.TrySetCanceled(item.Token);
                    continue;
                }

                try
                {
                    var result = item.Action();
                    result?.Freeze();
                    item.Tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    item.Tcs.TrySetException(ex);
                }
            }
        }

        public static async Task<ImageSource?> LoadAsync(Func<ImageSource?> func, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var registration = token.Register(() => tcs.TrySetCanceled(token));

            queue.Add(new WorkItem(func, tcs, token));

            return await tcs.Task;
        }
    }
}
