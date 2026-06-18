using System.Collections.Generic;
using UnityEngine;

public class BrokenBucketFallModel
{
    private readonly ManualRigidBodyState bodyState =
        new ManualRigidBodyState();

    public void Reset()
    {
        bodyState.Reset();
    }

    public void SetInitialVelocity(Vector3 initialVelocity)
    {
        bodyState.LinearVelocity =
            initialVelocity;

        bodyState.AngularVelocity =
            new Vector3(
                initialVelocity.z,
                0.0f,
                -initialVelocity.x
            ) * 0.08f;

        bodyState.Sleeping =
            false;

        bodyState.GroundedTime =
            0.0f;
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

        bodyState.ApplyGravity(
            gravity,
            deltaTime
        );

        bodyState.Integrate(
            bucket,
            deltaTime
        );

        List<Vector3> proxyPoints =
            BucketCollisionProxy.BuildLocalProxyPoints(
                bucket,
                groundCollisionSettings
            );

        GroundContactManifold manifold =
            BucketGroundContactGenerator.Generate(
                bucket,
                proxyPoints,
                groundCollisionSettings
            );

        bool grounded =
            SequentialImpulseGroundSolver.Solve(
                bucket,
                bodyState,
                manifold,
                bucketMass,
                groundCollisionSettings,
                deltaTime
            );

        bodyState.GroundedTime =
            grounded
                ? bodyState.GroundedTime + deltaTime
                : 0.0f;

        if (grounded)
        {
            if (grounded && bodyState.GroundedTime > 0.25f)
            {
                PreventUnwantedUpsideDownRest(bucket);
            }
            bodyState.ApplyGroundRestDamping(
                groundCollisionSettings,
                deltaTime
            );

            bodyState.TryGroundRestLock(
                groundCollisionSettings
            );
        }

        bodyState.TrySleep(
            groundCollisionSettings
        );
    }

    private void PreventUnwantedUpsideDownRest(Transform bucket)
    {
        if (bucket == null)
        {
            return;
        }

        float upDot =
            Vector3.Dot(
                bucket.up,
                Vector3.up
            );

        if (upDot >= -0.05f)
        {
            return;
        }

        Vector3 safeUp =
            Vector3.ProjectOnPlane(
                bucket.up,
                Vector3.up
            );

        if (safeUp.sqrMagnitude <= 0.0001f)
        {
            safeUp =
                Vector3.ProjectOnPlane(
                    bucket.forward,
                    Vector3.up
                );
        }

        if (safeUp.sqrMagnitude <= 0.0001f)
        {
            safeUp =
                Vector3.up;
        }

        safeUp.Normalize();

        bucket.rotation =
            Quaternion.FromToRotation(
                bucket.up,
                safeUp
            ) * bucket.rotation;

        bodyState.LinearVelocity =
            Vector3.zero;

        bodyState.AngularVelocity *=
            0.05f;
    }
}