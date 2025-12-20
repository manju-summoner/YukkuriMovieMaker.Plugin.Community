using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerLayout : Bindable
    {
        public ExplorerViewMode Mode { get; set => Set(ref field, value, nameof(Mode), nameof(MaxTextHeight)); } = ExplorerViewMode.List;
        public int IconSize { get; set => Set(ref field, value); } = 16;

        public double MaxTextHeight => SystemFonts.MessageFontSize * (Mode switch
        {
            ExplorerViewMode.List => 1,
            ExplorerViewMode.WrapList => 1,
            ExplorerViewMode.Tiles => 3,
            _ => throw new NotImplementedException(),
        });

        public ExplorerLayout Clone() => new()
        {
            Mode = Mode,
            IconSize = IconSize,
        };
        public bool CanDecreaseLayoutSize()
        {
            var currentSpec = ExplorerLayoutSpec.All.First(spec => spec.Mode == Mode);
            var sizes = ExplorerLayoutSpec.SupportedIconSizes
                .Where(size => size >= currentSpec.MinimumIconSize && size <= currentSpec.MaximumIconSize)
                .OrderBy(size => size)
                .ToArray();
            var currentIndex = Array.IndexOf(sizes, IconSize);
            if (currentIndex > 0)
                return true;
            else
            {
                var currentSpecIndex = Array.IndexOf(ExplorerLayoutSpec.All, currentSpec);
                return currentSpecIndex > 0;
            }
        }
        public void DecreaseLayoutSize()
        {
            var currentSpec = ExplorerLayoutSpec.All.First(spec => spec.Mode == Mode);
            var sizes = ExplorerLayoutSpec.SupportedIconSizes
                .Where(size => size >= currentSpec.MinimumIconSize && size <= currentSpec.MaximumIconSize)
                .OrderBy(size => size)
                .ToArray();
            var currentIndex = Array.IndexOf(sizes, IconSize);
            if (currentIndex > 0)
                IconSize = sizes[currentIndex - 1];
            else
            {
                var currentSpecIndex = Array.IndexOf(ExplorerLayoutSpec.All, currentSpec);
                if (currentSpecIndex > 0)
                {
                    var newSpec = ExplorerLayoutSpec.All[currentSpecIndex - 1];
                    Mode = newSpec.Mode;
                    IconSize = newSpec.MaximumIconSize;
                }
            }
        }
        public bool CanIncreaseLayoutSize()
        {
            var currentSpec = ExplorerLayoutSpec.All.First(spec => spec.Mode == Mode);
            var sizes = ExplorerLayoutSpec.SupportedIconSizes
                .Where(size => size >= currentSpec.MinimumIconSize && size <= currentSpec.MaximumIconSize)
                .OrderBy(size => size)
                .ToArray();
            var currentIndex = Array.IndexOf(sizes, IconSize);
            if (currentIndex < sizes.Length - 1)
                return true;
            else
            {
                var currentSpecIndex = Array.IndexOf(ExplorerLayoutSpec.All, currentSpec);
                return currentIndex < ExplorerLayoutSpec.All.Length;
            }
        }
        public void IncreaseLayoutSize()
        {
            var currentSpec = ExplorerLayoutSpec.All.First(spec => spec.Mode == Mode);
            var sizes = ExplorerLayoutSpec.SupportedIconSizes
                .Where(size => size >= currentSpec.MinimumIconSize && size <= currentSpec.MaximumIconSize)
                .OrderBy(size => size)
                .ToArray();
            var currentIndex = Array.IndexOf(sizes, IconSize);
            if (currentIndex < sizes.Length - 1)
                IconSize = sizes[currentIndex + 1];
            else
            {
                var currentSpecIndex = Array.IndexOf(ExplorerLayoutSpec.All, currentSpec);
                if (currentSpecIndex < ExplorerLayoutSpec.All.Length - 1)
                {
                    var newSpec = ExplorerLayoutSpec.All[currentSpecIndex + 1];
                    Mode = newSpec.Mode;
                    IconSize = newSpec.MinimumIconSize;
                }
            }
        }
    }
}
