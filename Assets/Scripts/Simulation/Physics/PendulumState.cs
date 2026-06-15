public struct PendulumState
{
    public float theta;
    public float thetaVelocity;
    public float phi;
    public float phiVelocity;

    public PendulumState(float theta, float thetaVelocity, float phi, float phiVelocity)
    {
        this.theta = theta;
        this.thetaVelocity = thetaVelocity;
        this.phi = phi;
        this.phiVelocity = phiVelocity;
    }
}