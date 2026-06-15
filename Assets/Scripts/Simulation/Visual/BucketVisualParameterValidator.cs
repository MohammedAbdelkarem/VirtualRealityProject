using UnityEngine;

public static class BucketVisualParameterValidator
{
    public static void Clamp(
        ref float topRadius,
        ref float bottomRadius,
        ref float height,
        ref float wallThickness,
        ref int segments,
        ref float handleHeight,
        ref float handleAttachWidthFactor,
        ref float handleWidth,
        ref float drainHoleRadius,
        ref float drainOutletOuterRadius,
        ref float drainOutletLength)
    {
        topRadius = Mathf.Max(0.05f, topRadius);
        bottomRadius = Mathf.Max(0.03f, bottomRadius);
        height = Mathf.Max(0.10f, height);

        wallThickness = Mathf.Clamp(
            wallThickness,
            0.005f,
            Mathf.Min(topRadius, bottomRadius) * 0.45f
        );

        segments = Mathf.Clamp(segments, 12, 128);

        handleHeight = Mathf.Max(0.05f, handleHeight);
        handleAttachWidthFactor = Mathf.Clamp(handleAttachWidthFactor, 0.2f, 1.0f);
        handleWidth = Mathf.Clamp(handleWidth, 0.005f, 0.08f);

        float innerBottomRadius = Mathf.Max(0.01f, bottomRadius - wallThickness);

        drainHoleRadius = Mathf.Clamp(
            drainHoleRadius,
            0.01f,
            innerBottomRadius * 0.65f
        );

        drainOutletOuterRadius = Mathf.Max(
            drainOutletOuterRadius,
            drainHoleRadius + 0.01f
        );

        drainOutletLength = Mathf.Clamp(drainOutletLength, 0.02f, 0.40f);
    }
}