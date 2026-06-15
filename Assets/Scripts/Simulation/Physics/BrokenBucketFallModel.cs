using UnityEngine;

public class BrokenBucketFallModel
{
    private Vector3 linearVelocity;
    private Vector3 angularVelocity;
    private float groundedTime;

    public void Reset()
    {
        linearVelocity = Vector3.zero;
        angularVelocity = Vector3.zero;
        groundedTime = 0.0f;
    }

    public void SetInitialVelocity(Vector3 initialVelocity)
    {
        linearVelocity = initialVelocity;

        angularVelocity =
            new Vector3(
                initialVelocity.z,
                0.0f,
                -initialVelocity.x
            ) * 0.35f;
    }

    public void Simulate(
        Transform bucket,
        float gravity,
        float bucketMass,
        float deltaTime,
        GroundCollisionSettings groundCollisionSettings)
    {
        if (bucket == null)
        {
            return;
        }

        linearVelocity +=
            Vector3.down * gravity * deltaTime;

        bucket.position +=
            linearVelocity * deltaTime;

        GroundCollisionModel.ApplyAngularVelocity(
            bucket,
            angularVelocity,
            deltaTime
        );

        bool isGrounded =
            GroundCollisionModel.ResolveGroundCollision(
                bucket,
                ref linearVelocity,
                ref angularVelocity,
                bucketMass,
                groundCollisionSettings,
                groundedTime,
                deltaTime
            );

        groundedTime =
            isGrounded
                ? groundedTime + deltaTime
                : 0.0f;
    }
}