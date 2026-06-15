using UnityEngine;

public class RopeBreakHandler
{
    public void BreakRope(
        PendulumState pendulumState,
        float activeRopeLength,
        float currentRopeTension,
        float maxRopeTension,
        BrokenBucketFallModel brokenBucketFallModel,
        LineRenderer ropeRenderer,
        int ropePointCount,
        Vector3 pivotPosition)
    {
        Vector3 initialVelocity =
            RopeMassModel.CalculateTangentialVelocity(
                pendulumState,
                activeRopeLength
            ) + Vector3.down * 0.5f;

        brokenBucketFallModel.SetInitialVelocity(initialVelocity);

        RopeRendererView.DrawBrokenRope(
            ropeRenderer,
            ropePointCount,
            pivotPosition
        );

        Debug.Log(
            "Rope torn. Tension = " +
            currentRopeTension.ToString("F2") +
            " N, Max = " +
            maxRopeTension.ToString("F2") +
            " N"
        );
    }
}