using UnityEngine;

public class ManualRigidBodyState
{
    private Vector3 linearVelocity;
    private Vector3 angularVelocity;
    private float groundedTime;
    private bool sleeping;

    public Vector3 LinearVelocity
    {
        get => linearVelocity;
        set => linearVelocity = value;
    }

    public Vector3 AngularVelocity
    {
        get => angularVelocity;
        set => angularVelocity = value;
    }

    public float GroundedTime
    {
        get => groundedTime;
        set => groundedTime = Mathf.Max(0.0f, value);
    }

    public bool Sleeping
    {
        get => sleeping;
        set => sleeping = value;
    }

    public void Reset()
    {
        linearVelocity = Vector3.zero;
        angularVelocity = Vector3.zero;
        groundedTime = 0.0f;
        sleeping = false;
    }

    public void ApplyGravity(float gravity, float deltaTime)
    {
        if (sleeping)
        {
            return;
        }

        linearVelocity += Vector3.down * gravity * deltaTime;
    }

    public void Integrate(Transform target, float deltaTime)
    {
        if (target == null || sleeping)
        {
            return;
        }

        target.position += linearVelocity * deltaTime;
        ApplyAngularVelocity(target, deltaTime);
    }

    public void ApplyAngularVelocity(Transform target, float deltaTime)
    {
        if (target == null || sleeping)
        {
            return;
        }

        float angularSpeed =
            angularVelocity.magnitude;

        if (angularSpeed <= 0.0001f)
        {
            return;
        }

        Vector3 axis =
            angularVelocity / angularSpeed;

        float angleDegrees =
            angularSpeed * Mathf.Rad2Deg * deltaTime;

        target.rotation =
            Quaternion.AngleAxis(angleDegrees, axis) *
            target.rotation;
    }

    public void ApplyGroundRestDamping(
        GroundCollisionSettings settings,
        float deltaTime)
    {
        if (settings == null || sleeping)
        {
            return;
        }

        float horizontalDamping =
            Mathf.Exp(
                -settings.GroundRestHorizontalDamping *
                deltaTime
            );

        float angularDamping =
            Mathf.Exp(
                -settings.GroundRestAngularDamping *
                deltaTime
            );

        linearVelocity.x *= horizontalDamping;
        linearVelocity.z *= horizontalDamping;

        angularVelocity *= angularDamping;

        if (Mathf.Abs(linearVelocity.x) <= settings.GroundRestLinearVelocity)
        {
            linearVelocity.x = 0.0f;
        }

        if (Mathf.Abs(linearVelocity.z) <= settings.GroundRestLinearVelocity)
        {
            linearVelocity.z = 0.0f;
        }

        if (Mathf.Abs(linearVelocity.y) <= settings.SleepLinearVelocity)
        {
            linearVelocity.y = 0.0f;
        }

        if (angularVelocity.magnitude <= settings.GroundRestAngularVelocity)
        {
            angularVelocity = Vector3.zero;
        }
    }

    public void TryGroundRestLock(GroundCollisionSettings settings)
    {
        if (settings == null || !settings.EnableGroundRestLock)
        {
            return;
        }

        if (groundedTime < settings.GroundRestLockDelay)
        {
            return;
        }

        bool linearSmall =
            linearVelocity.magnitude <= settings.GroundRestLinearVelocity;

        bool angularSmall =
            angularVelocity.magnitude <= settings.GroundRestAngularVelocity;

        if (!linearSmall || !angularSmall)
        {
            return;
        }

        ForceSleep();
    }

    public void TrySleep(GroundCollisionSettings settings)
    {
        if (settings == null)
        {
            sleeping = false;
            return;
        }

        if (groundedTime < settings.SleepDelay)
        {
            sleeping = false;
            return;
        }

        bool linearSmall =
            linearVelocity.magnitude <= settings.SleepLinearVelocity;

        bool angularSmall =
            angularVelocity.magnitude <= settings.SleepAngularVelocity;

        sleeping =
            linearSmall &&
            angularSmall;

        if (sleeping)
        {
            ForceSleep();
        }
    }

    public void ForceSleep()
    {
        linearVelocity = Vector3.zero;
        angularVelocity = Vector3.zero;
        sleeping = true;
    }
}