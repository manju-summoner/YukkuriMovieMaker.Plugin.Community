using System.Collections.Immutable;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services
{
    sealed class PenLayerVisibility
    {
        readonly List<bool> visibility = [];
        readonly Dictionary<Guid, bool> folderVisibility = [];
        readonly Dictionary<Guid, bool> clipBaseVisibility = [];

        public bool this[int index] => visibility[index];

        public void Update(ImmutableList<PenLayer> layers)
        {
            visibility.Clear();
            folderVisibility.Clear();
            clipBaseVisibility.Clear();

            for (var i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.IsFolder)
                    folderVisibility[layer.Id] = layer.IsVisible && IsParentVisible(layer.ParentId);
            }

            foreach (var layer in layers)
            {
                var isVisible = layer.IsVisible && IsParentVisible(layer.ParentId);
                if (isVisible && layer.IsClipping && clipBaseVisibility.TryGetValue(layer.ParentId, out var baseVisible) && !baseVisible)
                    isVisible = false;
                if (!layer.IsClipping)
                    clipBaseVisibility[layer.ParentId] = isVisible;
                visibility.Add(isVisible);
            }
        }

        bool IsParentVisible(Guid parentId)
            => parentId == Guid.Empty || (folderVisibility.TryGetValue(parentId, out var visible) && visible);
    }
}
