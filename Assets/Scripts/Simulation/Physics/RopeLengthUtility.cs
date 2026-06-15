using UnityEngine;

public static class RopeLengthUtility
{
    public static float CalculateSegmentLength(float ropeLength, int ropePointCount)
    {
        float safeRopeLength = Mathf.Max(0.001f, ropeLength);
        int safePointCount = Mathf.Max(2, ropePointCount);

        return safeRopeLength / (safePointCount - 1);
    }
}