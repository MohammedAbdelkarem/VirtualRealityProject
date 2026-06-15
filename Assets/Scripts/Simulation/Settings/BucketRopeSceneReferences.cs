using UnityEngine;

[System.Serializable]
public class BucketRopeSceneReferences
{
    [SerializeField] private Transform pivotPoint;
    [SerializeField] private Transform bucket;
    [SerializeField] private Transform bucketRopeAttachment;
    [SerializeField] private LineRenderer ropeRenderer;

    public Transform Bucket => bucket;
    public LineRenderer RopeRenderer => ropeRenderer;

    public void ResolveBucketRopeAttachment()
    {
        bucketRopeAttachment =
            SimulationReferenceResolver.ResolveBucketRopeAttachment(
                bucket,
                bucketRopeAttachment
            );
    }

    public void ConfigureSceneAccess(SimulationSceneAccess sceneAccess)
    {
        sceneAccess.Configure(
            pivotPoint,
            bucket,
            bucketRopeAttachment
        );
    }
}