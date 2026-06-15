using UnityEngine;

public class RopeLoadRuntimeModel
{
    private float dynamicRopeLength;
    private float currentRopeTension;
    private float currentRopeStretch;
    private bool ropeIsBroken;

    public float DynamicRopeLength => dynamicRopeLength;
    public float CurrentRopeTension => currentRopeTension;
    public float CurrentRopeStretch => currentRopeStretch;
    public bool RopeIsBroken => ropeIsBroken;

    public void Reset(float baseRopeLength)
    {
        dynamicRopeLength = Mathf.Max(0.001f, baseRopeLength);
        currentRopeTension = 0.0f;
        currentRopeStretch = 0.0f;
        ropeIsBroken = false;
    }

    public float GetActiveRopeLength(float baseRopeLength)
    {
        if (dynamicRopeLength > 0.0f)
        {
            return dynamicRopeLength;
        }

        return Mathf.Max(0.001f, baseRopeLength);
    }

    public void UpdateStretch(
        PendulumState pendulumState,
        bool useMassEffects,
        float baseRopeLength,
        float bucketMass,
        float gravity,
        float ropeStretchStiffness,
        float maxRopeStretch)
    {
        if (!useMassEffects)
        {
            dynamicRopeLength = Mathf.Max(0.001f, baseRopeLength);
            currentRopeTension = 0.0f;
            currentRopeStretch = 0.0f;
            return;
        }

        float activeLengthBeforeStretch =
            GetActiveRopeLength(baseRopeLength);

        currentRopeTension = RopeMassModel.CalculateTension(
            pendulumState,
            bucketMass,
            gravity,
            activeLengthBeforeStretch
        );

        currentRopeStretch = RopeMassModel.CalculateStretch(
            currentRopeTension,
            ropeStretchStiffness,
            maxRopeStretch
        );

        dynamicRopeLength =
            Mathf.Max(0.001f, baseRopeLength) +
            currentRopeStretch;
    }

    public bool ShouldBreak(
        bool useMassEffects,
        bool ropeCanTear,
        float maxRopeTension)
    {
        if (!useMassEffects || !ropeCanTear || ropeIsBroken)
        {
            return false;
        }

        return currentRopeTension > maxRopeTension;
    }

    public void MarkBroken()
    {
        ropeIsBroken = true;
    }
}