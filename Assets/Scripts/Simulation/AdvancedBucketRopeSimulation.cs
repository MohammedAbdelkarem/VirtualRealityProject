using UnityEngine;

public class AdvancedBucketRopeSimulation : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform pivotPoint;
    [SerializeField] private Transform bucket;
    [SerializeField] private Transform bucketRopeAttachment;
    [SerializeField] private LineRenderer ropeRenderer;

    [Header("Spherical Pendulum Inputs")]
    [SerializeField] private float ropeLength = 4.0f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float bucketMass = 1.0f;

    [Header("Initial Motion")]
    [SerializeField] private float initialThetaDegrees = 35.0f;
    [SerializeField] private float initialPhiDegrees = 0.0f;
    [SerializeField] private float initialThetaVelocity = 0.0f;
    [SerializeField] private float initialPhiVelocity = 1.5f;

    [Header("Energy Loss")]
    [Range(0.0f, 2.0f)]
    [SerializeField] private float dampingPerSecond = 0.08f;

    [Header("RK4 Stability")]
    [SerializeField] private float maxTimeStep = 0.02f;

    [Header("Advanced Rope - Verlet / PBD")]
    [SerializeField] private int ropePointCount = 28;
    [SerializeField] private int constraintIterations = 10;
    [SerializeField] private float ropeGravityMultiplier = 0.18f;
    [SerializeField] private float ropeVerletDamping = 0.992f;

    [Header("Rope Visual Shape")]
    [SerializeField] private float ropeWidth = 0.012f;
    [SerializeField] private Color ropeColor = new Color(0.36f, 0.22f, 0.10f, 1.0f);
    [SerializeField] private int ropeCornerSmoothness = 8;
    [SerializeField] private int ropeCapSmoothness = 8;

    [Header("Bucket Rotation")]
    [SerializeField] private bool alignBucketWithRope = true;
    [SerializeField] private bool enableBucketSpin = true;
    [SerializeField] private float spinSpeedDegreesPerSecond = 20.0f;

    private PendulumState state;
    private Vector3[] ropeCurrentPositions;
    private Vector3[] ropePreviousPositions;
    private float segmentLength;
    private float currentSpinAngle;

    private struct PendulumState
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

    private struct PendulumDerivative
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

    private void Start()
    {
        AutoFindBucketRopeAttachment();
        InitializePendulum();
        InitializeRope();
        UpdateAllVisuals();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        SimulatePendulumWithRK4(deltaTime);
        UpdateBucketPositionFromPendulum();

        /*
         * Important order:
         * Rotate the bucket BEFORE updating the rope.
         * This guarantees that the rope endpoint uses the final handle attachment position.
         */

        UpdateBucketRotation(deltaTime);

        SimulateRopeWithVerletAndPBD(deltaTime);
        UpdateRopeRenderer();
    }

    private void AutoFindBucketRopeAttachment()
    {
        if (bucketRopeAttachment != null || bucket == null)
        {
            return;
        }

        Transform generatedAttachment = bucket.Find("Generated_RopeAttachmentPoint");

        if (generatedAttachment != null)
        {
            bucketRopeAttachment = generatedAttachment;
            return;
        }

        RealisticBucketVisualBuilder visualBuilder = bucket.GetComponent<RealisticBucketVisualBuilder>();

        if (visualBuilder != null && visualBuilder.RopeAttachmentPoint != null)
        {
            bucketRopeAttachment = visualBuilder.RopeAttachmentPoint;
        }
    }

    private void InitializePendulum()
    {
        state = new PendulumState(
            DegreesToRadians(initialThetaDegrees),
            initialThetaVelocity,
            DegreesToRadians(initialPhiDegrees),
            initialPhiVelocity
        );

        currentSpinAngle = 0.0f;
    }

    private void InitializeRope()
    {
        if (ropePointCount < 2)
        {
            ropePointCount = 2;
        }

        ropeCurrentPositions = new Vector3[ropePointCount];
        ropePreviousPositions = new Vector3[ropePointCount];

        segmentLength = ropeLength / (ropePointCount - 1);

        SetupRopeRendererMaterialAndShape();

        Vector3 start = GetPivotPosition();
        Vector3 end = CalculateBucketWorldPosition();
        Vector3 direction = (end - start).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.down;
        }

        for (int i = 0; i < ropePointCount; i++)
        {
            Vector3 pointPosition = start + direction * (segmentLength * i);
            ropeCurrentPositions[i] = pointPosition;
            ropePreviousPositions[i] = pointPosition;
        }

        UpdateBucketPositionFromPendulum();
        UpdateBucketRotation(0.0f);
        UpdateRopeRenderer();
    }

    private void SetupRopeRendererMaterialAndShape()
    {
        if (ropeRenderer == null)
        {
            Debug.LogWarning("Rope Renderer is not assigned.");
            return;
        }

        ropeRenderer.positionCount = ropePointCount;
        ropeRenderer.useWorldSpace = true;
        ropeRenderer.startWidth = ropeWidth;
        ropeRenderer.endWidth = ropeWidth;
        ropeRenderer.numCornerVertices = ropeCornerSmoothness;
        ropeRenderer.numCapVertices = ropeCapSmoothness;
        ropeRenderer.startColor = ropeColor;
        ropeRenderer.endColor = ropeColor;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material ropeMaterial = new Material(shader);
        ropeMaterial.color = ropeColor;
        ropeRenderer.material = ropeMaterial;
    }

    private void UpdateAllVisuals()
    {
        UpdateBucketPositionFromPendulum();
        UpdateBucketRotation(0.0f);
        SimulateRopeWithVerletAndPBD(0.0f);
        UpdateRopeRenderer();
    }

    private void SimulatePendulumWithRK4(float deltaTime)
    {
        if (ropeLength <= 0.0f)
        {
            Debug.LogWarning("Rope length must be greater than zero.");
            return;
        }

        float remainingTime = deltaTime;

        while (remainingTime > 0.0f)
        {
            float step = Mathf.Min(maxTimeStep, remainingTime);
            state = RK4Step(state, step);
            ApplyVelocityDamping(step);
            ClampPendulumState();
            remainingTime -= step;
        }
    }

    private PendulumState RK4Step(PendulumState currentState, float deltaTime)
    {
        PendulumDerivative k1 = EvaluateDerivative(currentState);
        PendulumDerivative k2 = EvaluateDerivative(AddDerivative(currentState, k1, deltaTime * 0.5f));
        PendulumDerivative k3 = EvaluateDerivative(AddDerivative(currentState, k2, deltaTime * 0.5f));
        PendulumDerivative k4 = EvaluateDerivative(AddDerivative(currentState, k3, deltaTime));

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

    private PendulumDerivative EvaluateDerivative(PendulumState currentState)
    {
        float sinTheta = Mathf.Sin(currentState.theta);
        float cosTheta = Mathf.Cos(currentState.theta);

        float safeSinTheta = Mathf.Abs(sinTheta) < 0.001f
            ? 0.001f * Mathf.Sign(sinTheta == 0.0f ? 1.0f : sinTheta)
            : sinTheta;

        float thetaAcceleration =
            sinTheta * cosTheta * currentState.phiVelocity * currentState.phiVelocity
            - (gravity / ropeLength) * sinTheta;

        float phiAcceleration =
            -2.0f * currentState.thetaVelocity * currentState.phiVelocity * cosTheta / safeSinTheta;

        return new PendulumDerivative(
            currentState.thetaVelocity,
            thetaAcceleration,
            currentState.phiVelocity,
            phiAcceleration
        );
    }

    private PendulumState AddDerivative(
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

    private void ApplyVelocityDamping(float deltaTime)
    {
        float dampingFactor = Mathf.Exp(-dampingPerSecond * deltaTime);

        state.thetaVelocity *= dampingFactor;
        state.phiVelocity *= dampingFactor;
    }

    private void ClampPendulumState()
    {
        float minTheta = DegreesToRadians(1.0f);
        float maxTheta = DegreesToRadians(89.0f);

        state.theta = Mathf.Clamp(state.theta, minTheta, maxTheta);
    }

    private void UpdateBucketPositionFromPendulum()
    {
        if (bucket == null)
        {
            Debug.LogWarning("Bucket is not assigned.");
            return;
        }

        /*
         * The bucket transform is treated as the handle top attachment point.
         * The visual mesh is generated below this origin by RealisticBucketVisualBuilder.
         */

        bucket.position = CalculateBucketWorldPosition();
    }

    private Vector3 CalculateBucketWorldPosition()
    {
        Vector3 pivot = GetPivotPosition();

        float x = ropeLength * Mathf.Sin(state.theta) * Mathf.Cos(state.phi);
        float y = -ropeLength * Mathf.Cos(state.theta);
        float z = ropeLength * Mathf.Sin(state.theta) * Mathf.Sin(state.phi);

        return pivot + new Vector3(x, y, z);
    }

    private void SimulateRopeWithVerletAndPBD(float deltaTime)
    {
        if (ropeCurrentPositions == null || ropePreviousPositions == null)
        {
            return;
        }

        ApplyRopeVerlet(deltaTime);

        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            ApplyRopeConstraints();
        }
    }

    private void ApplyRopeVerlet(float deltaTime)
    {
        Vector3 gravityAcceleration = Vector3.down * gravity * ropeGravityMultiplier;

        for (int i = 1; i < ropePointCount - 1; i++)
        {
            Vector3 currentPosition = ropeCurrentPositions[i];
            Vector3 previousPosition = ropePreviousPositions[i];

            Vector3 velocity = (currentPosition - previousPosition) * ropeVerletDamping;

            ropePreviousPositions[i] = currentPosition;
            ropeCurrentPositions[i] =
                currentPosition + velocity + gravityAcceleration * deltaTime * deltaTime;
        }

        ropeCurrentPositions[0] = GetPivotPosition();
        ropeCurrentPositions[ropePointCount - 1] = GetBucketRopeAttachPosition();

        ropePreviousPositions[0] = ropeCurrentPositions[0];
        ropePreviousPositions[ropePointCount - 1] = ropeCurrentPositions[ropePointCount - 1];
    }

    private void ApplyRopeConstraints()
    {
        ropeCurrentPositions[0] = GetPivotPosition();
        ropeCurrentPositions[ropePointCount - 1] = GetBucketRopeAttachPosition();

        for (int i = 0; i < ropePointCount - 1; i++)
        {
            Vector3 pointA = ropeCurrentPositions[i];
            Vector3 pointB = ropeCurrentPositions[i + 1];

            Vector3 delta = pointB - pointA;
            float currentDistance = delta.magnitude;

            if (currentDistance <= 0.0001f)
            {
                continue;
            }

            float difference = (currentDistance - segmentLength) / currentDistance;
            Vector3 correction = delta * difference;

            bool pointAIsFixed = i == 0;
            bool pointBIsFixed = i + 1 == ropePointCount - 1;

            if (pointAIsFixed && pointBIsFixed)
            {
                continue;
            }

            if (pointAIsFixed)
            {
                ropeCurrentPositions[i + 1] -= correction;
            }
            else if (pointBIsFixed)
            {
                ropeCurrentPositions[i] += correction;
            }
            else
            {
                ropeCurrentPositions[i] += correction * 0.5f;
                ropeCurrentPositions[i + 1] -= correction * 0.5f;
            }
        }

        ropeCurrentPositions[0] = GetPivotPosition();
        ropeCurrentPositions[ropePointCount - 1] = GetBucketRopeAttachPosition();
    }

    private void UpdateRopeRenderer()
    {
        if (ropeRenderer == null || ropeCurrentPositions == null)
        {
            return;
        }

        if (ropeRenderer.positionCount != ropePointCount)
        {
            ropeRenderer.positionCount = ropePointCount;
        }

        for (int i = 0; i < ropePointCount; i++)
        {
            ropeRenderer.SetPosition(i, ropeCurrentPositions[i]);
        }

        /*
         * Final hard snap:
         * This guarantees the visible rope endpoint is exactly on the generated handle attachment point.
         */

        ropeRenderer.SetPosition(ropePointCount - 1, GetBucketRopeAttachPosition());
    }

    private void UpdateBucketRotation(float deltaTime)
    {
        if (bucket == null || pivotPoint == null)
        {
            return;
        }

        if (enableBucketSpin)
        {
            currentSpinAngle += spinSpeedDegreesPerSecond * deltaTime;
        }

        if (!alignBucketWithRope)
        {
            return;
        }

        Vector3 ropeDirection = GetBucketRopeAttachPosition() - GetPivotPosition();

        if (ropeDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 bucketUpDirection = -ropeDirection.normalized;
        Quaternion alignRotation = Quaternion.FromToRotation(Vector3.up, bucketUpDirection);
        Quaternion spinRotation = enableBucketSpin
            ? Quaternion.AngleAxis(currentSpinAngle, Vector3.up)
            : Quaternion.identity;

        bucket.rotation = alignRotation * spinRotation;
    }

    private Vector3 GetPivotPosition()
    {
        if (pivotPoint == null)
        {
            return Vector3.zero;
        }

        return pivotPoint.position;
    }

    private Vector3 GetBucketRopeAttachPosition()
    {
        if (bucketRopeAttachment != null)
        {
            return bucketRopeAttachment.position;
        }

        AutoFindBucketRopeAttachment();

        if (bucketRopeAttachment != null)
        {
            return bucketRopeAttachment.position;
        }

        if (bucket != null)
        {
            return bucket.position;
        }

        return CalculateBucketWorldPosition();
    }

    private float DegreesToRadians(float degrees)
    {
        return degrees * Mathf.Deg2Rad;
    }

    public void ResetSimulation()
    {
        AutoFindBucketRopeAttachment();
        InitializePendulum();
        InitializeRope();
        UpdateAllVisuals();
    }

    public void SetRopeLength(float newRopeLength)
    {
        if (newRopeLength <= 0.0f)
        {
            Debug.LogWarning("Rope length must be greater than zero.");
            return;
        }

        ropeLength = newRopeLength;
        segmentLength = ropeLength / (ropePointCount - 1);

        ResetSimulation();
    }

    public void SetGravity(float newGravity)
    {
        gravity = newGravity;
    }

    public void SetInitialThetaDegrees(float newInitialThetaDegrees)
    {
        initialThetaDegrees = newInitialThetaDegrees;
    }

    public void SetInitialPhiDegrees(float newInitialPhiDegrees)
    {
        initialPhiDegrees = newInitialPhiDegrees;
    }

    public void SetDampingPerSecond(float newDampingPerSecond)
    {
        dampingPerSecond = Mathf.Max(0.0f, newDampingPerSecond);
    }

    public void SetBucketMass(float newBucketMass)
    {
        if (newBucketMass <= 0.0f)
        {
            Debug.LogWarning("Bucket mass must be greater than zero.");
            return;
        }

        bucketMass = newBucketMass;
    }
}
