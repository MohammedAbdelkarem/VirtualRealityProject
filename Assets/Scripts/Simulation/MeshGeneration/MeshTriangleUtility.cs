using System.Collections.Generic;

public static class MeshTriangleUtility
{
    public static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);

        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    public static void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
    {
        AddQuad(triangles, a, b, c, d);
        AddQuad(triangles, d, c, b, a);
    }

    public static void AddTopAnnulusQuad(
        List<int> triangles,
        int outerA,
        int outerB,
        int holeA,
        int holeB)
    {
        triangles.Add(outerA);
        triangles.Add(holeA);
        triangles.Add(holeB);

        triangles.Add(outerA);
        triangles.Add(holeB);
        triangles.Add(outerB);
    }

    public static void AddBottomAnnulusQuad(
        List<int> triangles,
        int outerA,
        int outerB,
        int holeA,
        int holeB)
    {
        triangles.Add(outerA);
        triangles.Add(outerB);
        triangles.Add(holeB);

        triangles.Add(outerA);
        triangles.Add(holeB);
        triangles.Add(holeA);
    }
}