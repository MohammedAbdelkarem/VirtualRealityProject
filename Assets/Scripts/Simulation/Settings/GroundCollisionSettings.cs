using UnityEngine;

[System.Serializable]
public class GroundCollisionSettings
{
    [Header("Solid Ground")]
    [SerializeField] private bool enableGroundCollision = true;
    [SerializeField] private float groundHeight = 0.0f;

    [Tooltip("Small safety distance above ground.")]
    [SerializeField] private float contactSkin = 0.005f;

    [Tooltip("How close a point can be to the ground and still be considered a contact.")]
    [SerializeField] private float contactTolerance = 0.025f;

    [Header("Sequential Impulse Solver")]
    [SerializeField] private int solverIterations = 10;
    [SerializeField] private int maxContactCount = 16;

    [Tooltip("How aggressively penetration is corrected.")]
    [SerializeField] private float positionCorrectionPercent = 0.9f;

    [Tooltip("Small penetration ignored to prevent jitter.")]
    [SerializeField] private float positionCorrectionSlop = 0.001f;

    [Header("Material Response")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float bounciness = 0.01f;

    [Tooltip("Higher value means stronger sliding resistance.")]
    [SerializeField] private float frictionCoefficient = 0.85f;

    [Header("Damping")]
    [SerializeField] private float airAngularDampingPerSecond = 0.25f;
    [SerializeField] private float groundLinearDampingPerSecond = 3.0f;
    [SerializeField] private float groundAngularDampingPerSecond = 7.0f;

    [Header("Sleep / Ground Lock")]
    [SerializeField] private float sleepLinearVelocity = 0.05f;
    [SerializeField] private float sleepAngularVelocity = 0.08f;
    [SerializeField] private float sleepDelay = 0.45f;

    [Header("Anti-Walking Ground Rest")]
    [SerializeField] private bool enableGroundRestLock = true;

    [Tooltip("How long the bucket must stay grounded before hard locking is allowed.")]
    [SerializeField] private float groundRestLockDelay = 0.35f;

    [Tooltip("Extra horizontal damping while the bucket is on the ground.")]
    [SerializeField] private float groundRestHorizontalDamping = 18.0f;

    [Tooltip("Extra angular damping while the bucket is on the ground.")]
    [SerializeField] private float groundRestAngularDamping = 20.0f;

    [SerializeField] private float groundRestLinearVelocity = 0.20f;
    [SerializeField] private float groundRestAngularVelocity = 0.25f;

    [Header("Rope Ground Collision")]
    [SerializeField] private float ropeGroundRadius = 0.01f;
    [SerializeField] private float ropeGroundFriction = 0.65f;

    [Header("Fallback Bucket Proxy")]
    [SerializeField] private float fallbackBucketRadius = 0.35f;
    [SerializeField] private float fallbackBucketHeight = 0.65f;

    public bool EnableGroundCollision => enableGroundCollision;
    public float GroundHeight => groundHeight;
    public float ContactSkin => Mathf.Max(0.0f, contactSkin);
    public float ContactTolerance => Mathf.Max(0.001f, contactTolerance);

    public int SolverIterations => Mathf.Max(1, solverIterations);
    public int MaxContactCount => Mathf.Max(1, maxContactCount);
    public float PositionCorrectionPercent => Mathf.Clamp01(positionCorrectionPercent);
    public float PositionCorrectionSlop => Mathf.Max(0.0f, positionCorrectionSlop);

    public float Bounciness => Mathf.Clamp01(bounciness);
    public float FrictionCoefficient => Mathf.Max(0.0f, frictionCoefficient);

    public float AirAngularDampingPerSecond => Mathf.Max(0.0f, airAngularDampingPerSecond);
    public float GroundLinearDampingPerSecond => Mathf.Max(0.0f, groundLinearDampingPerSecond);
    public float GroundAngularDampingPerSecond => Mathf.Max(0.0f, groundAngularDampingPerSecond);

    public float SleepLinearVelocity => Mathf.Max(0.0f, sleepLinearVelocity);
    public float SleepAngularVelocity => Mathf.Max(0.0f, sleepAngularVelocity);
    public float SleepDelay => Mathf.Max(0.0f, sleepDelay);

    public bool EnableGroundRestLock => enableGroundRestLock;
    public float GroundRestLockDelay => Mathf.Max(0.0f, groundRestLockDelay);
    public float GroundRestHorizontalDamping => Mathf.Max(0.0f, groundRestHorizontalDamping);
    public float GroundRestAngularDamping => Mathf.Max(0.0f, groundRestAngularDamping);
    public float GroundRestLinearVelocity => Mathf.Max(0.0f, groundRestLinearVelocity);
    public float GroundRestAngularVelocity => Mathf.Max(0.0f, groundRestAngularVelocity);

    public float RopeGroundRadius => Mathf.Max(0.0f, ropeGroundRadius);
    public float RopeGroundFriction => Mathf.Clamp01(ropeGroundFriction);

    public float FallbackBucketRadius => Mathf.Max(0.01f, fallbackBucketRadius);
    public float FallbackBucketHeight => Mathf.Max(0.01f, fallbackBucketHeight);
}