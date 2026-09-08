namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    /// <summary>
    /// 参照シーンを描画している間だけ立つ入れ子の印。
    /// 参照シーンの描画は本体側でアイテムごとに並列化されるため、[ThreadStatic] では描画スレッドに印が届かない。
    /// AsyncLocal は ExecutionContext と一緒に作業スレッドへ流れる。
    /// </summary>
    internal static class ColorTransferReferenceScope
    {
        private static readonly AsyncLocal<State?> _current = new();

        public static int Depth => _current.Value?.Depth ?? 0;

        public static bool IsOwner(ColorTransferEffect item)
            => ReferenceEquals(_current.Value?.Owner, item);

        public static Handle Enter(ColorTransferEffect owner)
        {
            var previous = _current.Value;
            _current.Value = new State(owner, Depth + 1);
            return new Handle(previous);
        }

        internal readonly struct Handle(State? previous) : IDisposable
        {
            public void Dispose() => _current.Value = previous;
        }

        internal sealed record State(ColorTransferEffect Owner, int Depth);
    }
}
