namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal struct SortableVertex : IComparable<SortableVertex>
{
    public int V;
    public int Vt;
    public int Vn;
    public int OriginalIndex;

    public SortableVertex(int v, int vt, int vn, int originalIndex)
    {
        V = v;
        Vt = vt;
        Vn = vn;
        OriginalIndex = originalIndex;
    }

    public int CompareTo(SortableVertex other)
    {
        int c = V - other.V;
        if (c != 0) return c;
        c = Vt - other.Vt;
        if (c != 0) return c;
        return Vn - other.Vn;
    }
}
