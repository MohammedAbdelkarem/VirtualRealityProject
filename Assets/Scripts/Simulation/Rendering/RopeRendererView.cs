using UnityEngine;

public static class RopeRendererView
{
    public static void Setup(
        LineRenderer ropeRenderer,
        int pointCount,
        float ropeWidth,
        Color ropeColor,
        int cornerSmoothness,
        int capSmoothness)
    {
        if (ropeRenderer == null)
        {
            Debug.LogWarning("Rope Renderer is not assigned.");
            return;
        }

        ropeRenderer.positionCount = Mathf.Max(2, pointCount);
        ropeRenderer.useWorldSpace = true;

        ropeRenderer.startWidth = ropeWidth;
        ropeRenderer.endWidth = ropeWidth;

        ropeRenderer.numCornerVertices = Mathf.Max(0, cornerSmoothness);
        ropeRenderer.numCapVertices = Mathf.Max(0, capSmoothness);

        ropeRenderer.startColor = ropeColor;
        ropeRenderer.endColor = ropeColor;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material ropeMaterial = new Material(shader);
        ropeMaterial.color = ropeColor;

        ropeRenderer.material = ropeMaterial;
    }

    public static void Draw(
        LineRenderer ropeRenderer,
        Vector3[] ropePoints,
        Vector3 finalAttachmentPoint)
    {
        if (ropeRenderer == null || ropePoints == null || ropePoints.Length < 2)
        {
            return;
        }

        if (ropeRenderer.positionCount != ropePoints.Length)
        {
            ropeRenderer.positionCount = ropePoints.Length;
        }

        for (int i = 0; i < ropePoints.Length; i++)
        {
            ropeRenderer.SetPosition(i, ropePoints[i]);
        }

        ropeRenderer.SetPosition(ropePoints.Length - 1, finalAttachmentPoint);
    }

    public static void DrawBrokenRope(
        LineRenderer ropeRenderer,
        int originalPointCount,
        Vector3 pivotPoint)
    {
        if (ropeRenderer == null)
        {
            return;
        }

        int remainingPoints = Mathf.Max(2, originalPointCount / 3);
        ropeRenderer.positionCount = remainingPoints;

        for (int i = 0; i < remainingPoints; i++)
        {
            float t = i / (float)(remainingPoints - 1);
            ropeRenderer.SetPosition(i, pivotPoint + Vector3.down * t * 0.8f);
        }
    }
}