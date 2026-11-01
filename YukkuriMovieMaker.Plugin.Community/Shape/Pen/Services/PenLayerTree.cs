using System.Collections.Immutable;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services
{
    static class PenLayerTree
    {
        public static PenLayer? Find(ImmutableList<PenLayer> layers, Guid id)
        {
            foreach (var layer in layers)
            {
                if (layer.Id == id)
                    return layer;
            }
            return null;
        }

        public static PenLayer? FindSibling(ImmutableList<PenLayer> layers, PenLayer layer, int delta)
        {
            var index = layers.IndexOf(layer);
            if (index < 0)
                return null;

            var step = delta > 0 ? 1 : -1;
            for (var i = index + step; i >= 0 && i < layers.Count; i += step)
            {
                if (layers[i].ParentId == layer.ParentId)
                    return layers[i];
            }
            return null;
        }

        public static PenLayer? FindTargetFolder(ImmutableList<PenLayer> layers, PenLayer? layer)
        {
            if (layer is null)
                return null;

            var index = layers.IndexOf(layer);
            if (index < 0)
                return null;

            for (var i = index + 1; i < layers.Count; i++)
            {
                var candidate = layers[i];
                if (candidate.ParentId != layer.ParentId)
                    continue;
                return candidate.IsFolder ? candidate : null;
            }
            return null;
        }

        public static PenLayer? FindMergeTarget(ImmutableList<PenLayer> layers, PenLayer? layer)
        {
            if (layer is null || !IsMergeable(layers, layer))
                return null;

            var lower = FindSibling(layers, layer, -1);
            if (lower is null || !IsMergeable(layers, lower))
                return null;

            return HasFill(layer) && HasStroke(lower) ? null : lower;
        }

        static bool HasFill(PenLayer layer)
        {
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.FillFigures is not null)
                    return true;
            }
            return false;
        }

        static bool HasStroke(PenLayer layer)
        {
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.FillFigures is null)
                    return true;
            }
            return false;
        }

        static bool IsMergeable(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            if (layer is not { IsFolder: false, IsVisible: true, IsClipping: false, IsRangeOverridden: false } || IsLocked(layers, layer))
                return false;
            if (layer.BlendMode is not ProjectBlend.Normal)
                return false;
            if (!layer.VideoEffects.IsEmpty)
                return false;

            var values = layer.Opacity.Values;
            return values.Count == 1 && values[0].Value == 100;
        }

        public static bool IsLocked(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            if (layer.IsLocked)
                return true;

            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = Find(layers, parentId);
                if (parent is null)
                    return false;
                if (parent.IsLocked)
                    return true;
                parentId = parent.ParentId;
            }
            return false;
        }

        public static bool IsCollapsed(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = Find(layers, parentId);
                if (parent is null)
                    return false;
                if (!parent.IsExpanded)
                    return true;
                parentId = parent.ParentId;
            }
            return false;
        }

        public static void ExpandAncestors(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = Find(layers, parentId);
                if (parent is null)
                    return;

                parent.IsExpanded = true;
                parentId = parent.ParentId;
            }
        }

        public static ImmutableList<PenLayer> CreateDisplayLayers(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            for (var i = layers.Count - 1; i >= 0; i--)
            {
                if (!IsCollapsed(layers, layers[i]))
                    builder.Add(layers[i]);
            }
            return builder.ToImmutable();
        }

        public static PenLayer CloneSubtree(ImmutableList<PenLayer> layers, PenLayer layer, Guid parentId, ImmutableList<PenLayer>.Builder builder)
        {
            var copy = layer.Clone(Guid.NewGuid());
            copy.ParentId = parentId;
            if (layer.IsFolder)
            {
                foreach (var child in layers)
                {
                    if (child.ParentId == layer.Id)
                        CloneSubtree(layers, child, copy.Id, builder);
                }
            }
            builder.Add(copy);
            return copy;
        }

        public static void RemoveSubtree(ImmutableList<PenLayer>.Builder builder, PenLayer layer)
        {
            if (layer.IsFolder)
            {
                for (var i = builder.Count - 1; i >= 0; i--)
                {
                    if (builder[i].ParentId == layer.Id)
                        RemoveSubtree(builder, builder[i]);
                }
            }
            builder.Remove(layer);
        }

        public static int CountSubtree(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var count = 1;
            if (!layer.IsFolder)
                return count;

            foreach (var candidate in layers)
            {
                if (candidate.ParentId == layer.Id)
                    count += CountSubtree(layers, candidate);
            }
            return count;
        }

        public static ImmutableList<PenLayer> Normalize(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            var emitted = new HashSet<Guid>(layers.Count);
            EmitLayers(layers, Guid.Empty, 0, builder, emitted);

            foreach (var layer in layers)
            {
                if (!emitted.Add(layer.Id))
                    continue;
                layer.ParentId = Guid.Empty;
                layer.Depth = 0;
                layer.HasLowerLayer = builder.Count > 0;
                builder.Add(layer);
            }
            return builder.ToImmutable();
        }

        static void EmitLayers(ImmutableList<PenLayer> layers, Guid parentId, int depth, ImmutableList<PenLayer>.Builder builder, HashSet<Guid> emitted)
        {
            var hasBase = false;
            foreach (var layer in layers)
            {
                if (layer.ParentId != parentId || !emitted.Add(layer.Id))
                    continue;

                layer.Depth = depth;
                layer.HasLowerLayer = hasBase;
                hasBase = true;
                if (layer.IsFolder)
                    EmitLayers(layers, layer.Id, depth + 1, builder, emitted);
                builder.Add(layer);
            }
        }
    }
}
