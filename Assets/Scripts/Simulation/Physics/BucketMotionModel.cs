using UnityEngine;

public static class BucketMotionModel
{
    private const float MaximumAttachedTiltDegrees = 10.0f;

    public static Vector3 CalculatePendulumPosition(
        PendulumState state,
        Vector3 pivotPosition,
        float ropeLength)
    {
        float activeLength =
            Mathf.Max(0.001f, ropeLength);

        float sinTheta =
            Mathf.Sin(state.theta);

        float x =
            activeLength *
            sinTheta *
            Mathf.Cos(state.phi);

        float y =
            -activeLength *
            Mathf.Cos(state.theta);

        float z =
            activeLength *
            sinTheta *
            Mathf.Sin(state.phi);

        return pivotPosition + new Vector3(x, y, z);
    }

    public static void ApplyPendulumPosition(
        Transform bucket,
        Vector3 worldPosition)
    {
        if (bucket == null)
        {
            return;
        }

        bucket.position =
            worldPosition;
    }

    public static void ApplyStableAttachedRotationWithSelfSpin(
        Transform bucket,
        Vector3 pivotPosition,
        Vector3 ropeAttachmentPosition,
        Quaternion stableBaseRotation,
        bool enableSelfSpin,
        float spinSpeedDegreesPerSecond,
        ref float currentSpinAngle,
        float deltaTime)
    {
        if (bucket == null)
        {
            return;
        }

        if (enableSelfSpin)
        {
            currentSpinAngle +=
                spinSpeedDegreesPerSecond *
                deltaTime;
        }

        Quaternion spinRotation =
            Quaternion.AngleAxis(
                currentSpinAngle,
                Vector3.up
            );

        Quaternion uprightSpinRotation =
            spinRotation *
            stableBaseRotation;

        Vector3 ropeDirection =
            pivotPosition - ropeAttachmentPosition;

        Vector3 horizontalRopeDirection =
            Vector3.ProjectOnPlane(
                ropeDirection,
                Vector3.up
            );

        if (horizontalRopeDirection.sqrMagnitude <= 0.0001f)
        {
            bucket.rotation =
                uprightSpinRotation;

            return;
        }

        horizontalRopeDirection.Normalize();

        Vector3 tiltAxis =
            Vector3.Cross(
                Vector3.up,
                horizontalRopeDirection
            );

        if (tiltAxis.sqrMagnitude <= 0.0001f)
        {
            bucket.rotation =
                uprightSpinRotation;

            return;
        }

        tiltAxis.Normalize();

        Quaternion smallTilt =
            Quaternion.AngleAxis(
                MaximumAttachedTiltDegrees,
                tiltAxis
            );

        bucket.rotation =
            smallTilt *
            uprightSpinRotation;
    }
}