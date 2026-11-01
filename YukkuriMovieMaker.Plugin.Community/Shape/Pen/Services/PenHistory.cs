using System.Collections.Immutable;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services
{
    sealed class PenHistory
    {
        const int Capacity = 100;

        readonly List<ImmutableList<PenLayer>> undo = [];
        readonly List<ImmutableList<PenLayer>> redo = [];
        ImmutableList<PenLayer> current = [];
        int editDepth;
        bool isDirty;

        public bool IsEditing => editDepth > 0;

        public bool CanUndo => editDepth == 0 && undo.Count > 0;

        public bool CanRedo => editDepth == 0 && redo.Count > 0;

        public void Reset(ImmutableList<PenLayer> layers)
        {
            undo.Clear();
            redo.Clear();
            current = Capture(layers);
            isDirty = false;
        }

        public void MarkDirty() => isDirty = true;

        public void ClearDirty() => isDirty = false;

        public bool BeginEdit()
        {
            editDepth++;
            return editDepth == 1;
        }

        public bool EndEdit()
        {
            if (editDepth > 0)
                editDepth--;
            return editDepth == 0;
        }

        public bool Commit(ImmutableList<PenLayer> layers)
        {
            if (!isDirty)
                return false;

            isDirty = false;
            undo.Add(current);
            if (undo.Count > Capacity)
                undo.RemoveAt(0);
            redo.Clear();
            current = Capture(layers);
            return true;
        }

        public ImmutableList<PenLayer>? Undo()
        {
            if (undo.Count == 0)
                return null;

            var snapshot = undo[^1];
            undo.RemoveAt(undo.Count - 1);
            redo.Add(current);
            current = snapshot;
            return snapshot;
        }

        public ImmutableList<PenLayer>? Redo()
        {
            if (redo.Count == 0)
                return null;

            var snapshot = redo[^1];
            redo.RemoveAt(redo.Count - 1);
            undo.Add(current);
            current = snapshot;
            return snapshot;
        }

        public static ImmutableList<PenLayer> Capture(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            foreach (var layer in layers)
                builder.Add(layer.Clone(layer.Id));
            return builder.ToImmutable();
        }
    }
}
