using UnityEngine;

[System.Serializable]
public class PendulumRuntimeSettings
{
    [Header("Spherical Pendulum Inputs")]
    [SerializeField] private float ropeLength = 4.0f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float bucketMass = 1.0f;

    [Header("Initial Motion")]
    [SerializeField] private float initialThetaDegrees = 35.0f;
    [SerializeField] private float initialPhiDegrees = 0.0f;
    [SerializeField] private float initialThetaVelocity = 0.0f;
    [SerializeField] private float initialPhiVelocity = 1.5f;

    [Header("Energy Loss")]
    [Range(0.0f, 2.0f)]
    [SerializeField] private float dampingPerSecond = 0.08f;

    [Header("RK4 Stability")]
    [SerializeField] private float maxTimeStep = 0.02f;

    public float RopeLength => ropeLength;
    public float Gravity => gravity;
    public float BucketMass => bucketMass;
    public float InitialThetaDegrees => initialThetaDegrees;
    public float InitialPhiDegrees => initialPhiDegrees;
    public float InitialThetaVelocity => initialThetaVelocity;
    public float InitialPhiVelocity => initialPhiVelocity;
    public float DampingPerSecond => dampingPerSecond;
    public float MaxTimeStep => maxTimeStep;

    public void SetRopeLength(float value)
    {
        ropeLength = Mathf.Max(0.001f, value);
    }

    public void SetGravity(float value)
    {
        gravity = value;
    }

    public void SetBucketMass(float value)
    {
        bucketMass = Mathf.Max(0.001f, value);
    }

    public void SetInitialThetaDegrees(float value)
    {
        initialThetaDegrees = value;
    }

    public void SetInitialPhiDegrees(float value)
    {
        initialPhiDegrees = value;
    }

    public void SetDampingPerSecond(float value)
    {
        dampingPerSecond = Mathf.Max(0.0f, value);
    }
}