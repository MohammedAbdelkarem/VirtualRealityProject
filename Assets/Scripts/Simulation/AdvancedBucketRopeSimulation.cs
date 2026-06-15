using UnityEngine;

public class AdvancedBucketRopeSimulation : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    private BucketRopeSceneReferences sceneReferences =
        new BucketRopeSceneReferences();

    [SerializeField]
    private PendulumRuntimeSettings pendulumSettings =
        new PendulumRuntimeSettings();

    [SerializeField]
    private RopePbdSettings ropePbdSettings =
        new RopePbdSettings();

    [SerializeField]
    private RopeVisualSettings ropeVisualSettings =
        new RopeVisualSettings();

    [SerializeField]
    private BucketRotationSettings bucketRotationSettings =
        new BucketRotationSettings();

    [SerializeField]
    private RopeLoadSettings ropeLoadSettings =
        new RopeLoadSettings();

    [SerializeField]
    private GroundCollisionSettings groundCollisionSettings =
        new GroundCollisionSettings();

    [Header("Debug Read Only")]
    [SerializeField] private float currentRopeTension;
    [SerializeField] private float currentRopeStretch;
    [SerializeField] private bool ropeIsBroken;

    private float segmentLength;
    private float currentSpinAngle;

    private readonly SimulationSceneAccess sceneAccess =
        new SimulationSceneAccess();

    private readonly PendulumSimulationModel pendulumModel =
        new PendulumSimulationModel();

    private readonly RopePbdSolver ropeSolver =
        new RopePbdSolver();

    private readonly RopeLoadRuntimeModel ropeLoadModel =
        new RopeLoadRuntimeModel();

    private readonly BrokenBucketFallModel brokenBucketFallModel =
        new BrokenBucketFallModel();

    private readonly RopeBreakHandler ropeBreakHandler =
        new RopeBreakHandler();

    private void Start()
    {
        ResolveSceneReferences();
        InitializeSimulationRuntimeState();
        InitializePendulum();
        UpdateRopeLoadState();
        InitializeRope();
        UpdateAllVisuals();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (ropeLoadModel.RopeIsBroken)
        {
            brokenBucketFallModel.Simulate(
            sceneReferences.Bucket,
            pendulumSettings.Gravity,
            pendulumSettings.BucketMass,
            deltaTime,
            groundCollisionSettings
        );

            SyncDebugValues();
            return;
        }

        UpdateRopeLoadState();
        CheckRopeTear();

        if (ropeLoadModel.RopeIsBroken)
        {
            SyncDebugValues();
            return;
        }

        SimulatePendulum(deltaTime);
        UpdateRopeLoadState();
        CheckRopeTear();

        if (ropeLoadModel.RopeIsBroken)
        {
            SyncDebugValues();
            return;
        }

        UpdateBucketPositionFromPendulum();
        SimulateRope(deltaTime);
        UpdateRopeRenderer();
        UpdateBucketRotation(deltaTime);

        SyncDebugValues();
    }

    private void ResolveSceneReferences()
    {
        sceneReferences.ResolveBucketRopeAttachment();
        sceneReferences.ConfigureSceneAccess(sceneAccess);
    }

    private void InitializeSimulationRuntimeState()
    {
        ropeLoadModel.Reset(pendulumSettings.RopeLength);
        UpdateSegmentLength();

        currentSpinAngle = 0.0f;

        brokenBucketFallModel.Reset();
        SyncDebugValues();
    }

    private void InitializePendulum()
    {
        pendulumModel.Initialize(
            pendulumSettings.InitialThetaDegrees,
            pendulumSettings.InitialThetaVelocity,
            pendulumSettings.InitialPhiDegrees,
            pendulumSettings.InitialPhiVelocity
        );
    }

    private void InitializeRope()
    {
        UpdateSegmentLength();

        RopeRendererView.Setup(
            sceneReferences.RopeRenderer,
            ropePbdSettings.RopePointCount,
            ropeVisualSettings.RopeWidth,
            ropeVisualSettings.RopeColor,
            ropeVisualSettings.RopeCornerSmoothness,
            ropeVisualSettings.RopeCapSmoothness
        );

        ropeSolver.Initialize(
            ropePbdSettings.RopePointCount,
            GetActiveRopeLength(),
            GetPivotPosition(),
            CalculateBucketWorldPosition()
        );

        ropeSolver.SetSegmentLength(segmentLength);

        UpdateBucketPositionFromPendulum();
        UpdateBucketRotation(0.0f);
        UpdateRopeRenderer();
    }

    private void UpdateAllVisuals()
    {
        UpdateBucketPositionFromPendulum();
        UpdateBucketRotation(0.0f);
        SimulateRope(0.0f);
        UpdateRopeRenderer();
    }

    private void SimulatePendulum(float deltaTime)
    {
        pendulumModel.Simulate(
            deltaTime,
            pendulumSettings.MaxTimeStep,
            pendulumSettings.Gravity,
            GetActiveRopeLength(),
            pendulumSettings.DampingPerSecond,
            ropeLoadSettings.UseMassEffects,
            ropeLoadSettings.UseMassBasedDamping,
            ropeLoadSettings.AirDragDamping,
            pendulumSettings.BucketMass
        );
    }

    private void UpdateBucketPositionFromPendulum()
    {
        if (sceneReferences.Bucket == null)
        {
            Debug.LogWarning("Bucket is not assigned.");
            return;
        }

        BucketMotionModel.ApplyPendulumPosition(
            sceneReferences.Bucket,
            CalculateBucketWorldPosition()
        );
    }

    private Vector3 CalculateBucketWorldPosition()
    {
        return BucketMotionModel.CalculatePendulumPosition(
            pendulumModel.State,
            GetPivotPosition(),
            GetActiveRopeLength()
        );
    }

    private void SimulateRope(float deltaTime)
    {
        if (!ropeSolver.IsInitialized)
        {
            return;
        }

        ropeSolver.SetSegmentLength(segmentLength);

        ropeSolver.Simulate(
            deltaTime,
            ropePbdSettings.ConstraintIterations,
            pendulumSettings.Gravity,
            ropePbdSettings.RopeGravityMultiplier,
            ropePbdSettings.RopeVerletDamping,
            GetPivotPosition(),
            GetBucketRopeAttachPosition()
        );
    }

    private void UpdateRopeRenderer()
    {
        RopeRendererView.Draw(
            sceneReferences.RopeRenderer,
            ropeSolver.CurrentPositions,
            GetBucketRopeAttachPosition()
        );
    }

    private void UpdateBucketRotation(float deltaTime)
    {
        BucketMotionModel.ApplyRopeAlignedRotation(
            sceneReferences.Bucket,
            GetPivotPosition(),
            GetBucketRopeAttachPosition(),
            bucketRotationSettings.AlignBucketWithRope,
            bucketRotationSettings.EnableBucketSpin,
            bucketRotationSettings.SpinSpeedDegreesPerSecond,
            ref currentSpinAngle,
            deltaTime
        );
    }

    private void UpdateRopeLoadState()
    {
        ropeLoadModel.UpdateStretch(
            pendulumModel.State,
            ropeLoadSettings.UseMassEffects,
            pendulumSettings.RopeLength,
            pendulumSettings.BucketMass,
            pendulumSettings.Gravity,
            ropeLoadSettings.RopeStretchStiffness,
            ropeLoadSettings.MaxRopeStretch
        );

        UpdateSegmentLength();
        SyncDebugValues();
    }

    private void UpdateSegmentLength()
    {
        segmentLength =
            RopeLengthUtility.CalculateSegmentLength(
                GetActiveRopeLength(),
                ropePbdSettings.RopePointCount
            );
    }

    private void CheckRopeTear()
    {
        if (!ropeLoadModel.ShouldBreak(
                ropeLoadSettings.UseMassEffects,
                ropeLoadSettings.RopeCanTear,
                ropeLoadSettings.MaxRopeTension))
        {
            return;
        }

        BreakRope();
    }

    private void BreakRope()
    {
        ropeLoadModel.MarkBroken();

        ropeBreakHandler.BreakRope(
            pendulumModel.State,
            GetActiveRopeLength(),
            ropeLoadModel.CurrentRopeTension,
            ropeLoadSettings.MaxRopeTension,
            brokenBucketFallModel,
            sceneReferences.RopeRenderer,
            ropePbdSettings.RopePointCount,
            GetPivotPosition()
        );

        SyncDebugValues();
    }

    private void SyncDebugValues()
    {
        currentRopeTension = ropeLoadModel.CurrentRopeTension;
        currentRopeStretch = ropeLoadModel.CurrentRopeStretch;
        ropeIsBroken = ropeLoadModel.RopeIsBroken;
    }

    private Vector3 GetPivotPosition()
    {
        return sceneAccess.GetPivotPosition();
    }

    private Vector3 GetBucketRopeAttachPosition()
    {
        return sceneAccess.GetBucketRopeAttachPosition(
            CalculateBucketWorldPosition()
        );
    }

    private float GetActiveRopeLength()
    {
        return ropeLoadModel.GetActiveRopeLength(
            pendulumSettings.RopeLength
        );
    }

    public void ResetSimulation()
    {
        ResolveSceneReferences();
        InitializeSimulationRuntimeState();
        InitializePendulum();
        UpdateRopeLoadState();
        InitializeRope();
        UpdateAllVisuals();
    }

    public void SetRopeLength(float newRopeLength)
    {
        pendulumSettings.SetRopeLength(newRopeLength);
        ropeLoadModel.Reset(pendulumSettings.RopeLength);
        UpdateSegmentLength();
        ResetSimulation();
    }

    public void SetGravity(float newGravity)
    {
        pendulumSettings.SetGravity(newGravity);
    }

    public void SetInitialThetaDegrees(float newInitialThetaDegrees)
    {
        pendulumSettings.SetInitialThetaDegrees(newInitialThetaDegrees);
    }

    public void SetInitialPhiDegrees(float newInitialPhiDegrees)
    {
        pendulumSettings.SetInitialPhiDegrees(newInitialPhiDegrees);
    }

    public void SetDampingPerSecond(float newDampingPerSecond)
    {
        pendulumSettings.SetDampingPerSecond(newDampingPerSecond);
    }

    public void SetBucketMass(float newBucketMass)
    {
        pendulumSettings.SetBucketMass(newBucketMass);
    }
}