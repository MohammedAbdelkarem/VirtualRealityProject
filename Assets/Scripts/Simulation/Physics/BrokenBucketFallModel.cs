using UnityEngine;

public class BrokenBucketFallModel
{
    private Vector3 velocity;

    public void Reset()
    {
        velocity = Vector3.zero;
    }

    public void SetInitialVelocity(Vector3 initialVelocity)
    {
        velocity = initialVelocity;
    }

    public void Simulate(Transform bucket, float gravity, float deltaTime)
    {
        if (bucket == null)
        {
            return;
        }

        velocity += Vector3.down * gravity * deltaTime;
        bucket.position += velocity * deltaTime;
    }
}