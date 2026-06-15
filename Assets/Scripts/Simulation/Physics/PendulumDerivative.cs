public struct PendulumDerivative
{
    public float thetaDerivative;
    public float thetaVelocityDerivative;
    public float phiDerivative;
    public float phiVelocityDerivative;

    public PendulumDerivative(
        float thetaDerivative,
        float thetaVelocityDerivative,
        float phiDerivative,
        float phiVelocityDerivative)
    {
        this.thetaDerivative = thetaDerivative;
        this.thetaVelocityDerivative = thetaVelocityDerivative;
        this.phiDerivative = phiDerivative;
        this.phiVelocityDerivative = phiVelocityDerivative;
    }
}