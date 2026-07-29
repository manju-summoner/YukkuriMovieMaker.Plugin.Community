namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

internal static class BoundingBoxUtility
{
    public static Model3DPart[] CalculatePartCenters(Model3DData model)
    {
        var vertices = model.Vertices;
        var indices = model.Indices;
        var parts = model.Parts.ToArray();

        for (int i = 0; i < parts.Length; i++)
        {
            var box = new CullingBox();
            int end = parts[i].IndexOffset + parts[i].IndexCount;

            for (int j = parts[i].IndexOffset; j < end && j < indices.Length; j++)
            {
                int index = indices[j];
                if ((uint)index < (uint)vertices.Length)
                    box.Expand(vertices[index].Position);
            }

            if (box.IsEmpty) continue;

            parts[i].Center = (box.Min + box.Max) * 0.5f;
        }

        return parts;
    }
}
