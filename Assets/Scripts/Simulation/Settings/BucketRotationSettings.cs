using UnityEngine;

[System.Serializable]
public class BucketRotationSettings
{
    [Header("Bucket Rotation")]
    [SerializeField] private bool alignBucketWithRope = true;
    [SerializeField] private bool enableBucketSpin = true;
    [SerializeField] private float spinSpeedDegreesPerSecond = 20.0f;

    public bool AlignBucketWithRope => alignBucketWithRope;
    public bool EnableBucketSpin => enableBucketSpin;
    public float SpinSpeedDegreesPerSecond => spinSpeedDegreesPerSecond;
}