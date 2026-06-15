using UnityEngine;

public static class PendulumRK4Solver
{
    public static PendulumState Step(
        PendulumState currentState,
        float deltaTime,
        float gravity,
        float ropeLength)
    {
        PendulumDerivative k1 = EvaluateDerivative(currentState, gravity, ropeLength);
        PendulumDerivative k2 = EvaluateDerivative(AddDerivative(currentState, k1, deltaTime * 0.5f), gravity, ropeLength);
        PendulumDerivative k3 = EvaluateDerivative(AddDerivative(currentState, k2, deltaTime * 0.5f), gravity, ropeLength);
        PendulumDerivative k4 = EvaluateDerivative(AddDerivative(currentState, k3, deltaTime), gravity, ropeLength);

        float oneSixthDeltaTime = deltaTime / 6.0f;

        return new PendulumState(
            currentState.theta + oneSixthDeltaTime * (
                k1.thetaDerivative +
                2.0f * k2.thetaDerivative +
                2.0f * k3.thetaDerivative +
                k4.thetaDerivative
            ),
            currentState.thetaVelocity + oneSixthDeltaTime * (
                k1.thetaVelocityDerivative +
                2.0f * k2.thetaVelocityDerivative +
                2.0f * k3.thetaVelocityDerivative +
                k4.thetaVelocityDerivative
            ),
            currentState.phi + oneSixthDeltaTime * (
                k1.phiDerivative +
                2.0f * k2.phiDerivative +
                2.0f * k3.phiDerivative +
                k4.phiDerivative
            ),
            currentState.phiVelocity + oneSixthDeltaTime * (
                k1.phiVelocityDerivative +
                2.0f * k2.phiVelocityDerivative +
                2.0f * k3.phiVelocityDerivative +
                k4.phiVelocityDerivative
            )
        );
    }

    private static PendulumDerivative EvaluateDerivative(
        PendulumState currentState,
        float gravity,
        float ropeLength)
    {
        float safeRopeLength = Mathf.Max(0.001f, ropeLength);

        float sinTheta = Mathf.Sin(currentState.theta);
        float cosTheta = Mathf.Cos(currentState.theta);

        float safeSinTheta = Mathf.Abs(sinTheta) < 0.001f
            ? 0.001f * Mathf.Sign(sinTheta == 0.0f ? 1.0f : sinTheta)
            : sinTheta;

        float thetaAcceleration =
            sinTheta * cosTheta * currentState.phiVelocity * currentState.phiVelocity
            - (gravity / safeRopeLength) * sinTheta;

        float phiAcceleration =
            -2.0f * currentState.thetaVelocity * currentState.phiVelocity * cosTheta / safeSinTheta;

        return new PendulumDerivative(
            currentState.thetaVelocity,
            thetaAcceleration,
            currentState.phiVelocity,
            phiAcceleration
        );
    }

    private static PendulumState AddDerivative(
        PendulumState baseState,
        PendulumDerivative derivative,
        float deltaTime)
    {
        return new PendulumState(
            baseState.theta + derivative.thetaDerivative * deltaTime,
            baseState.thetaVelocity + derivative.thetaVelocityDerivative * deltaTime,
            baseState.phi + derivative.phiDerivative * deltaTime,
            baseState.phiVelocity + derivative.phiVelocityDerivative * deltaTime
        );
    }
}