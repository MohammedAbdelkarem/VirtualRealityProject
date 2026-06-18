using UnityEngine;

public static class SequentialImpulseGroundSolver
{
    private static readonly Vector3 GroundNormal =
        Vector3.up;

    public static bool Solve(
        Transform bucket,
        ManualRigidBodyState bodyState,
        GroundContactManifold manifold,
        float mass,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        if (bucket == null ||
            bodyState == null ||
            settings == null)
        {
            return false;
        }

        if (manifold == null || manifold.Count == 0)
        {
            ApplyAirDamping(
                bodyState,
                settings,
                deltaTime
            );

            return false;
        }

        float safeMass =
            Mathf.Max(0.001f, mass);

        float inverseMass =
            1.0f / safeMass;

        float inverseInertia =
            EstimateInverseInertia(
                bucket,
                safeMass
            );

        for (int iteration = 0; iteration < settings.SolverIterations; iteration++)
        {
            for (int i = 0; i < manifold.Contacts.Count; i++)
            {
                SolveContactVelocity(
                    bucket,
                    bodyState,
                    manifold.Contacts[i],
                    inverseMass,
                    inverseInertia,
                    settings
                );
            }
        }

        ApplyPositionCorrection(
            bucket,
            manifold,
            settings
        );

        ApplyGroundDamping(
            bodyState,
            settings,
            deltaTime
        );

        return true;
    }

    private static void SolveContactVelocity(
        Transform bucket,
        ManualRigidBodyState bodyState,
        GroundContact contact,
        float inverseMass,
        float inverseInertia,
        GroundCollisionSettings settings)
    {
        Vector3 radius =
            contact.WorldPoint - bucket.position;

        Vector3 contactVelocity =
            bodyState.LinearVelocity +
            Vector3.Cross(
                bodyState.AngularVelocity,
                radius
            );

        float normalVelocity =
            Vector3.Dot(
                contactVelocity,
                GroundNormal
            );

        float normalImpulseMagnitude =
            0.0f;

        if (normalVelocity < 0.0f)
        {
            float denominator =
                inverseMass +
                CalculateAngularDenominator(
                    radius,
                    GroundNormal,
                    inverseInertia
                );

            normalImpulseMagnitude =
                -(1.0f + settings.Bounciness) *
                normalVelocity /
                Mathf.Max(0.0001f, denominator);

            Vector3 normalImpulse =
                GroundNormal * normalImpulseMagnitude;

            ApplyImpulse(
                bodyState,
                radius,
                normalImpulse,
                inverseMass,
                inverseInertia
            );
        }

        ApplyFrictionImpulse(
            bodyState,
            radius,
            inverseMass,
            inverseInertia,
            normalImpulseMagnitude,
            settings
        );
    }

    private static void ApplyFrictionImpulse(
        ManualRigidBodyState bodyState,
        Vector3 radius,
        float inverseMass,
        float inverseInertia,
        float normalImpulseMagnitude,
        GroundCollisionSettings settings)
    {
        Vector3 contactVelocity =
            bodyState.LinearVelocity +
            Vector3.Cross(
                bodyState.AngularVelocity,
                radius
            );

        Vector3 tangentVelocity =
            contactVelocity -
            Vector3.Dot(contactVelocity, GroundNormal) *
            GroundNormal;

        if (tangentVelocity.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 tangent =
            tangentVelocity.normalized;

        float denominator =
            inverseMass +
            CalculateAngularDenominator(
                radius,
                tangent,
                inverseInertia
            );

        float frictionImpulseMagnitude =
            -Vector3.Dot(contactVelocity, tangent) /
            Mathf.Max(0.0001f, denominator);

        float maxFriction =
            settings.FrictionCoefficient *
            Mathf.Max(
                normalImpulseMagnitude,
                0.03f
            );

        frictionImpulseMagnitude =
            Mathf.Clamp(
                frictionImpulseMagnitude,
                -maxFriction,
                maxFriction
            );

        Vector3 frictionImpulse =
            tangent * frictionImpulseMagnitude;

        ApplyImpulse(
            bodyState,
            radius,
            frictionImpulse,
            inverseMass,
            inverseInertia
        );
    }

    private static void ApplyImpulse(
        ManualRigidBodyState bodyState,
        Vector3 radius,
        Vector3 impulse,
        float inverseMass,
        float inverseInertia)
    {
        bodyState.LinearVelocity +=
            impulse * inverseMass;

        bodyState.AngularVelocity +=
            Vector3.Cross(radius, impulse) *
            inverseInertia;
    }

    private static float CalculateAngularDenominator(
        Vector3 radius,
        Vector3 direction,
        float inverseInertia)
    {
        Vector3 radiusCrossDirection =
            Vector3.Cross(radius, direction);

        Vector3 angularTerm =
            Vector3.Cross(
                radiusCrossDirection * inverseInertia,
                radius
            );

        return Vector3.Dot(
            direction,
            angularTerm
        );
    }

    private static void ApplyPositionCorrection(
        Transform bucket,
        GroundContactManifold manifold,
        GroundCollisionSettings settings)
    {
        float maxPenetration =
            manifold.GetMaxPenetrationDepth();

        float correctedPenetration =
            Mathf.Max(
                0.0f,
                maxPenetration - settings.PositionCorrectionSlop
            );

        if (correctedPenetration <= 0.0f)
        {
            return;
        }

        bucket.position +=
            GroundNormal *
            correctedPenetration *
            settings.PositionCorrectionPercent;
    }

    private static void ApplyGroundDamping(
        ManualRigidBodyState bodyState,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float linearDamping =
            Mathf.Exp(
                -settings.GroundLinearDampingPerSecond *
                deltaTime
            );

        float angularDamping =
            Mathf.Exp(
                -settings.GroundAngularDampingPerSecond *
                deltaTime
            );

        Vector3 velocity =
            bodyState.LinearVelocity;

        velocity.x *= linearDamping;
        velocity.z *= linearDamping;

        if (Mathf.Abs(velocity.y) <= settings.SleepLinearVelocity)
        {
            velocity.y = 0.0f;
        }

        bodyState.LinearVelocity =
            velocity;

        bodyState.AngularVelocity *=
            angularDamping;
    }

    private static void ApplyAirDamping(
        ManualRigidBodyState bodyState,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float angularDamping =
            Mathf.Exp(
                -settings.AirAngularDampingPerSecond *
                deltaTime
            );

        bodyState.AngularVelocity *=
            angularDamping;
    }

    private static float EstimateInverseInertia(
        Transform bucket,
        float mass)
    {
        Bounds bounds =
            CalculateBounds(bucket);

        float sizeSquared =
            Mathf.Max(
                0.001f,
                bounds.size.sqrMagnitude
            );

        float inertia =
            mass *
            sizeSquared /
            12.0f;

        return 1.0f /
            Mathf.Max(0.001f, inertia);
    }

    private static Bounds CalculateBounds(Transform bucket)
    {
        MeshFilter[] meshFilters =
            bucket.GetComponentsInChildren<MeshFilter>();

        bool hasBounds =
            false;

        Bounds combinedBounds =
            new Bounds(
                bucket.position,
                Vector3.one
            );

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter =
                meshFilters[i];

            if (meshFilter == null ||
                meshFilter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshBounds =
                meshFilter.sharedMesh.bounds;

            Vector3 worldCenter =
                meshFilter.transform.TransformPoint(
                    meshBounds.center
                );

            Vector3 worldSize =
                Vector3.Scale(
                    meshBounds.size,
                    meshFilter.transform.lossyScale
                );

            Bounds worldBounds =
                new Bounds(
                    worldCenter,
                    worldSize
                );

            if (!hasBounds)
            {
                combinedBounds = worldBounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(worldBounds);
            }
        }

        return combinedBounds;
    }
}