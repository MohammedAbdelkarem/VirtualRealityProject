using UnityEngine;

public static class SimulationReferenceResolver
{
    private const string GeneratedAttachmentName = "Generated_RopeAttachmentPoint";

    public static Transform ResolveBucketRopeAttachment(
        Transform bucket,
        Transform currentAttachment)
    {
        if (bucket == null)
        {
            return null;
        }

        if (IsValidBucketAttachment(bucket, currentAttachment))
        {
            return currentAttachment;
        }

        Transform generatedAttachment =
            bucket.Find(GeneratedAttachmentName);

        if (generatedAttachment != null)
        {
            return generatedAttachment;
        }

        RealisticBucketVisualBuilder visualBuilder =
            bucket.GetComponent<RealisticBucketVisualBuilder>();

        if (visualBuilder != null &&
            visualBuilder.RopeAttachmentPoint != null)
        {
            return visualBuilder.RopeAttachmentPoint;
        }

        return null;
    }

    private static bool IsValidBucketAttachment(
        Transform bucket,
        Transform attachment)
    {
        if (bucket == null || attachment == null)
        {
            return false;
        }

        if (attachment == bucket)
        {
            return false;
        }

        return attachment.IsChildOf(bucket);
    }
}