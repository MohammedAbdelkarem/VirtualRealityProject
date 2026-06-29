using UnityEngine;

public class PendulumSimulationModel
{
    private PendulumState state;

    public PendulumState State => state;

    public void Initialize(
        float initialThetaDegrees,
        float initialThetaVelocity,
        float initialPhiDegrees,
        float initialPhiVelocity)
    {
        state = new PendulumState(
            DegreesToRadians(initialThetaDegrees),
            initialThetaVelocity,
            DegreesToRadians(initialPhiDegrees),
            initialPhiVelocity
        );
    }

    public void Simulate(
        float deltaTime,
        float maxTimeStep,
        float gravity,
        float ropeLength,
        float dampingPerSecond,
        bool useMassEffects,
        bool useMassBasedDamping,
        float airDragDamping,
        float bucketMass)
    {
        float activeLength = Mathf.Max(0.001f, ropeLength);
        float safeMaxTimeStep = Mathf.Max(0.001f, maxTimeStep);

        float remainingTime = deltaTime;

        while (remainingTime > 0.0f)
        {
            float step = Mathf.Min(safeMaxTimeStep, remainingTime);

            state = PendulumRK4Solver.Step(
                state,
                step,
                gravity,
                activeLength
            );

            ApplyVelocityDamping(
                step,
                dampingPerSecond,
                useMassEffects,
                useMassBasedDamping,
                airDragDamping,
                bucketMass
            );

            ClampPendulumState();

            remainingTime -= step;
        }
    }

    private void ApplyVelocityDamping(
        float deltaTime,
        float dampingPerSecond,
        bool useMassEffects,
        bool useMassBasedDamping,
        float airDragDamping,
        float bucketMass)
    {
        float effectiveDamping = Mathf.Max(0.0f, dampingPerSecond);

        if (useMassEffects && useMassBasedDamping)
        {
            effectiveDamping += airDragDamping / Mathf.Max(0.1f, bucketMass);
        }

        float dampingFactor = Mathf.Exp(-effectiveDamping * deltaTime);

        state.thetaVelocity *= dampingFactor;
        state.phiVelocity *= dampingFactor;
    }

    private void ClampPendulumState()
    {
        float minTheta = DegreesToRadians(1.0f);
        float maxTheta = DegreesToRadians(89.0f);

        state.theta = Mathf.Clamp(
            state.theta,
            minTheta,
            maxTheta
        );
    }

    public void Stop()
    {
        state.thetaVelocity = 0f;
        state.phiVelocity = 0f;
    }

    private float DegreesToRadians(float degrees)
    {
        return degrees * Mathf.Deg2Rad;
    }
}