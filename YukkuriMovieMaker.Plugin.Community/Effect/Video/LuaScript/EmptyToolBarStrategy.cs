using System.Collections.Generic;
using YukkuriMovieMaker.Controls.AvalonEdit.ToolBarStrategy;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class EmptyToolBarStrategy : IToolBarStrategy
    {
        public IEnumerable<ToolBarGroup> GetToolBarGroups() => [];
    }
}
