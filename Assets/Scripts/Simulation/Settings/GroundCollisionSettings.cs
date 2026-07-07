using UnityEngine;

[System.Serializable]
public class GroundCollisionSettings
{
    [Header("Manual Ground Collision")]
    [SerializeField] private bool enableGroundCollision = true;
    [SerializeField] private float groundHeight = 0.0f;

    [Tooltip("Clearance above panel/ground so bucket doesn't visually sink in.")]
    [SerializeField] private float contactSkin = 0.03f;

    [Tooltip("Fallback only if no generated mesh is found.")]
    [SerializeField] private float bucketBottomOffset = 0.90f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float bounciness = 0.02f;

    [SerializeField] private float groundFrictionPerSecond = 8.0f;
    [SerializeField] private float stopVelocityThreshold = 0.15f;

    [Header("Manual Angular Impact")]
    [SerializeField] private bool enableAngularImpact = true;

    [Tooltip("How much angular speed is generated from impact torque.")]
    [SerializeField] private float impactAngularFactor = 0.55f;

    [Tooltip("Maximum angular speed in radians/second.")]
    [SerializeField] private float maxAngularSpeed = 8.0f;

    [SerializeField] private float airAngularDampingPerSecond = 0.35f;
    [SerializeField] private float groundAngularDampingPerSecond = 7.0f;

    [Tooltip("Vertices within this height from the lowest point are treated as contact patch.")]
    [SerializeField] private float contactPatchTolerance = 0.035f;

    [Header("Manual Side Rest Assist")]
    [SerializeField] private bool enableSideRestSettling = true;

    [Tooltip("Wait before helping the bucket settle on its side.")]
    [SerializeField] private float sideRestAssistDelay = 0.45f;

    [Tooltip("Higher value helps the bucket settle faster after impact energy is mostly gone.")]
    [SerializeField] private float sideRestRotationSpeed = 3.0f;

    [SerializeField] private bool forceBucketAxisHorizontalOnGround = true;

    [Header("Mass Influence On Ground Response")]
    [SerializeField] private float referenceMass = 1.0f;
    [SerializeField] private float maxMassForCollisionResponse = 150.0f;

    [Tooltip("Higher value makes heavy buckets rotate more slowly after impact.")]
    [SerializeField] private float massRotationResistance = 0.035f;

    [Tooltip("Higher value reduces bounce more strongly for heavy buckets.")]
    [SerializeField] private float massBounceResistance = 0.08f;

    [Tooltip("Extra friction added for heavier buckets.")]
    [SerializeField] private float heavyMassExtraFriction = 0.03f;

    public bool EnableGroundCollision => enableGroundCollision;
    public float GroundHeight => groundHeight;
    public float ContactSkin => Mathf.Max(0.0f, contactSkin);
    public float BucketBottomOffset => Mathf.Max(0.0f, bucketBottomOffset);
    public float Bounciness => Mathf.Clamp01(bounciness);
    public float GroundFrictionPerSecond => Mathf.Max(0.0f, groundFrictionPerSecond);
    public float StopVelocityThreshold => Mathf.Max(0.0f, stopVelocityThreshold);

    public bool EnableAngularImpact => enableAngularImpact;
    public float ImpactAngularFactor => Mathf.Max(0.0f, impactAngularFactor);
    public float MaxAngularSpeed => Mathf.Max(0.0f, maxAngularSpeed);
    public float AirAngularDampingPerSecond => Mathf.Max(0.0f, airAngularDampingPerSecond);
    public float GroundAngularDampingPerSecond => Mathf.Max(0.0f, groundAngularDampingPerSecond);
    public float ContactPatchTolerance => Mathf.Max(0.001f, contactPatchTolerance);

    public bool EnableSideRestSettling => enableSideRestSettling;
    public float SideRestAssistDelay => Mathf.Max(0.0f, sideRestAssistDelay);
    public float SideRestRotationSpeed => Mathf.Max(0.0f, sideRestRotationSpeed);
    public bool ForceBucketAxisHorizontalOnGround => forceBucketAxisHorizontalOnGround;

    public float ReferenceMass => Mathf.Max(0.001f, referenceMass);
    public float MaxMassForCollisionResponse => Mathf.Max(1.0f, maxMassForCollisionResponse);
    public float MassRotationResistance => Mathf.Max(0.0f, massRotationResistance);
    public float MassBounceResistance => Mathf.Max(0.0f, massBounceResistance);
    public float HeavyMassExtraFriction => Mathf.Max(0.0f, heavyMassExtraFriction);
}