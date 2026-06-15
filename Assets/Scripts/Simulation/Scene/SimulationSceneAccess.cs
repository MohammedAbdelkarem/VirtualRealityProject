using UnityEngine;

public class SimulationSceneAccess
{
    private Transform pivotPoint;
    private Transform bucket;
    private Transform bucketRopeAttachment;

    public void Configure(
        Transform pivotPoint,
        Transform bucket,
        Transform bucketRopeAttachment)
    {
        this.pivotPoint = pivotPoint;
        this.bucket = bucket;
        this.bucketRopeAttachment = bucketRopeAttachment;
    }

    public Vector3 GetPivotPosition()
    {
        if (pivotPoint == null)
        {
            return Vector3.zero;
        }

        return pivotPoint.position;
    }

    public Vector3 GetBucketRopeAttachPosition(Vector3 fallbackPosition)
    {
        if (bucketRopeAttachment != null)
        {
            return bucketRopeAttachment.position;
        }

        if (bucket != null)
        {
            return bucket.position;
        }

        return fallbackPosition;
    }
}