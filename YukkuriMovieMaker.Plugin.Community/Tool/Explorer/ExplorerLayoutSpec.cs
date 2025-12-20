using System;
using System.Collections.Generic;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal record ExplorerLayoutSpec(ExplorerViewMode Mode, int MinimumIconSize, int MaximumIconSize)
    {
        public static int[] SupportedIconSizes { get; } = [ 16, 20, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512 ];

        public static ExplorerLayoutSpec List { get; } = new ExplorerLayoutSpec(ExplorerViewMode.List, 16, 20);
        public static ExplorerLayoutSpec WrapList { get; } = new ExplorerLayoutSpec(ExplorerViewMode.WrapList, 20, 32);
        public static ExplorerLayoutSpec Tiles { get; } = new ExplorerLayoutSpec(ExplorerViewMode.Tiles, 32, 512);

        public static ExplorerLayoutSpec[] All { get; } = [ List, WrapList, Tiles ];
    }
}
