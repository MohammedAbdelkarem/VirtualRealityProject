using UnityEngine;

public static class RopeMassModel
{
    public static float CalculateTension(
        PendulumState state,
        float bucketMass,
        float gravity,
        float ropeLength)
    {
        float safeMass = Mathf.Max(0.001f, bucketMass);
        float safeRopeLength = Mathf.Max(0.001f, ropeLength);

        float sinTheta = Mathf.Sin(state.theta);
        float cosTheta = Mathf.Cos(state.theta);

        float angularSpeedSquared =
            state.thetaVelocity * state.thetaVelocity +
            sinTheta * sinTheta * state.phiVelocity * state.phiVelocity;

        float tension =
            safeMass *
            (
                gravity * cosTheta +
                safeRopeLength * angularSpeedSquared
            );

        return Mathf.Max(0.0f, tension);
    }

    public static float CalculateStretch(
        float tension,
        float ropeStretchStiffness,
        float maxRopeStretch)
    {
        float safeStiffness = Mathf.Max(1.0f, ropeStretchStiffness);
        float stretch = tension / safeStiffness;

        return Mathf.Clamp(stretch, 0.0f, maxRopeStretch);
    }

    public static Vector3 CalculateTangentialVelocity(
        PendulumState state,
        float ropeLength)
    {
        float safeRopeLength = Mathf.Max(0.001f, ropeLength);

        float sinTheta = Mathf.Sin(state.theta);
        float cosTheta = Mathf.Cos(state.theta);
        float sinPhi = Mathf.Sin(state.phi);
        float cosPhi = Mathf.Cos(state.phi);

        Vector3 thetaDirection = new Vector3(
            cosTheta * cosPhi,
            sinTheta,
            cosTheta * sinPhi
        );

        Vector3 phiDirection = new Vector3(
            -sinPhi,
            0.0f,
            cosPhi
        );

        return
            safeRopeLength * state.thetaVelocity * thetaDirection +
            safeRopeLength * sinTheta * state.phiVelocity * phiDirection;
    }
}