using System.Collections.Concurrent;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3EditorProbe
    {
        static readonly ConcurrentDictionary<string, Task<bool>> cache = new(StringComparer.OrdinalIgnoreCase);

        public static Task<bool> HasEditorAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult(false);
            return cache.GetOrAdd(path, p => Task.Run(() => Probe(p)));
        }

        static bool Probe(string path)
        {
            try
            {
                var hasView = false;
                Vst3HostThread.Invoke(() =>
                {
                    using var session = new Vst3EditorSession(path, null, null);
                    hasView = session.TryCreateView();
                });
                return hasView;
            }
            catch
            {
                return true;
            }
        }
    }
}
