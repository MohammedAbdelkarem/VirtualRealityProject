using UnityEngine;

public static class BucketMotionModel
{
    public static Vector3 CalculatePendulumPosition(
        PendulumState state,
        Vector3 pivotPosition,
        float ropeLength)
    {
        float safeRopeLength = Mathf.Max(0.001f, ropeLength);

        float x =
            safeRopeLength *
            Mathf.Sin(state.theta) *
            Mathf.Cos(state.phi);

        float y =
            -safeRopeLength *
            Mathf.Cos(state.theta);

        float z =
            safeRopeLength *
            Mathf.Sin(state.theta) *
            Mathf.Sin(state.phi);

        return pivotPosition + new Vector3(x, y, z);
    }

    public static void ApplyPendulumPosition(
        Transform bucket,
        Vector3 bucketWorldPosition)
    {
        if (bucket == null)
        {
            return;
        }

        bucket.position = bucketWorldPosition;
    }

    public static void ApplyRopeAlignedRotation(
        Transform bucket,
        Vector3 pivotPosition,
        Vector3 bucketRopeAttachmentPosition,
        bool alignBucketWithRope,
        bool enableBucketSpin,
        float spinSpeedDegreesPerSecond,
        ref float currentSpinAngle,
        float deltaTime)
    {
        if (bucket == null)
        {
            return;
        }

        if (enableBucketSpin)
        {
            currentSpinAngle += spinSpeedDegreesPerSecond * deltaTime;
        }

        if (!alignBucketWithRope)
        {
            return;
        }

        Vector3 ropeDirection =
            bucketRopeAttachmentPosition -
            pivotPosition;

        if (ropeDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 bucketUpDirection = -ropeDirection.normalized;

        Quaternion alignRotation =
            Quaternion.FromToRotation(
                Vector3.up,
                bucketUpDirection
            );

        Quaternion spinRotation = enableBucketSpin
            ? Quaternion.AngleAxis(currentSpinAngle, Vector3.up)
            : Quaternion.identity;

        bucket.rotation = alignRotation * spinRotation;
    }
}