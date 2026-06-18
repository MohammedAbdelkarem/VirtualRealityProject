using UnityEngine;

public class AdvancedBucketRopeSimulation : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField]
    private BucketRopeSceneReferences sceneReferences =
        new BucketRopeSceneReferences();

    [Header("Pendulum")]
    [SerializeField]
    private PendulumRuntimeSettings pendulumSettings =
        new PendulumRuntimeSettings();

    [Header("Rope PBD")]
    [SerializeField]
    private RopePbdSettings ropePbdSettings =
        new RopePbdSettings();

    [Header("Rope Visual")]
    [SerializeField]
    private RopeVisualSettings ropeVisualSettings =
        new RopeVisualSettings();

    [Header("Bucket Rotation")]
    [SerializeField]
    private BucketRotationSettings bucketRotationSettings =
        new BucketRotationSettings();

    [Header("Rope Load")]
    [SerializeField]
    private RopeLoadSettings ropeLoadSettings =
        new RopeLoadSettings();

    [Header("Ground Collision")]
    [SerializeField]
    private GroundCollisionSettings groundCollisionSettings =
        new GroundCollisionSettings();

    [SerializeField] private float minimumOverstressTimeBeforeTear = 0.25f;

    private float overstressTimer;

    [Header("Debug Read Only")]
    [SerializeField] private float currentRopeTension;
    [SerializeField] private float currentRopeStretch;
    [SerializeField] private bool ropeIsBroken;
    [SerializeField] private bool bucketIsRestingOnGround;

    private float segmentLength;
    private float currentSpinAngle;
    private Quaternion attachedBucketStableRotation;
    private float attachedGroundContactTimer;


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
        float deltaTime =
            Time.deltaTime;

        if (ropeLoadModel.RopeIsBroken)
        {
            SimulateBrokenBucket(deltaTime);
            SyncDebugValues();
            return;
        }

        if (bucketIsRestingOnGround)
        {
            KeepAttachedBucketRestingOnGround(deltaTime);
            SyncDebugValues();
            return;
        }

        UpdateRopeLoadState();

        SimulatePendulum(deltaTime);

        bool collidedWithGround =
            UpdateBucketPositionFromPendulum();

        if (collidedWithGround)
        {
            attachedGroundContactTimer += deltaTime;

            pendulumModel.DampenForSolidGroundContact();

            if (attachedGroundContactTimer >= 0.35f)
            {
                EnterAttachedGroundRestState();
            }
        }
        else
        {
            attachedGroundContactTimer = 0.0f;
        }

        UpdateRopeLoadState();

        SimulateRope(deltaTime);
        UpdateRopeRenderer();
        UpdateBucketRotation(deltaTime);

        CheckRopeTear(deltaTime);

        SyncDebugValues();
    }

    private void ResolveSceneReferences()
    {
        sceneReferences.ResolveBucketRopeAttachment();

        sceneReferences.ConfigureSceneAccess(
            sceneAccess
        );
    }

    private void InitializeSimulationRuntimeState()
    {
        ropeLoadModel.Reset(
            pendulumSettings.RopeLength
        );

        UpdateSegmentLength();
        currentSpinAngle =
            0.0f;

        attachedBucketStableRotation =
            sceneReferences.Bucket != null
                ? sceneReferences.Bucket.rotation
                : Quaternion.identity;

        bucketIsRestingOnGround =
            false;

        overstressTimer =
            0.0f;

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

        ropeSolver.SetSegmentLength(
            segmentLength
        );

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

    private void SimulateBrokenBucket(float deltaTime)
    {
        brokenBucketFallModel.Simulate(
            sceneReferences.Bucket,
            pendulumSettings.Gravity,
            pendulumSettings.BucketMass,
            deltaTime,
            groundCollisionSettings
        );
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

    private bool UpdateBucketPositionFromPendulum()
    {
        if (sceneReferences.Bucket == null)
        {
            Debug.LogWarning("Bucket is not assigned.");
            return false;
        }

        BucketMotionModel.ApplyPendulumPosition(
            sceneReferences.Bucket,
            CalculateBucketWorldPosition()
        );

        bool collidedWithGround =
            BucketGroundContactGenerator.ResolveKinematicSolidGround(
                sceneReferences.Bucket,
                groundCollisionSettings
            );

        if (collidedWithGround)
        {
            pendulumModel.SyncStateFromWorldPosition(
                GetPivotPosition(),
                sceneReferences.Bucket.position,
                GetActiveRopeLength()
            );

            pendulumModel.DampenForSolidGroundContact();
        }

        return collidedWithGround;
    }

    private void EnterAttachedGroundRestState()
    {
        bucketIsRestingOnGround =
            true;

        pendulumModel.StopMotion();

        if (sceneReferences.Bucket == null)
        {
            return;
        }

        BucketGroundContactGenerator.ResolveKinematicSolidGround(
            sceneReferences.Bucket,
            groundCollisionSettings
        );

        pendulumModel.SyncStateFromWorldPosition(
            GetPivotPosition(),
            sceneReferences.Bucket.position,
            GetActiveRopeLength()
        );
    }

    private void KeepAttachedBucketRestingOnGround(float deltaTime)
    {
        if (sceneReferences.Bucket == null)
        {
            return;
        }

        pendulumModel.StopMotion();

        BucketGroundContactGenerator.ResolveKinematicSolidGround(
            sceneReferences.Bucket,
            groundCollisionSettings
        );

        pendulumModel.SyncStateFromWorldPosition(
            GetPivotPosition(),
            sceneReferences.Bucket.position,
            GetActiveRopeLength()
        );

        SimulateRope(deltaTime);
        UpdateRopeRenderer();
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

        ropeSolver.SetSegmentLength(
            segmentLength
        );

        ropeSolver.Simulate(
            deltaTime,
            ropePbdSettings.ConstraintIterations,
            pendulumSettings.Gravity,
            ropePbdSettings.RopeGravityMultiplier,
            ropePbdSettings.RopeVerletDamping,
            GetPivotPosition(),
            GetBucketRopeAttachPosition(),
            groundCollisionSettings
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
        BucketMotionModel.ApplyStableAttachedRotationWithSelfSpin(
            sceneReferences.Bucket,
            GetPivotPosition(),
            GetBucketRopeAttachPosition(),
            attachedBucketStableRotation,
            bucketRotationSettings.EnableBucketSpin,
            bucketRotationSettings.SpinSpeedDegreesPerSecond,
            ref currentSpinAngle,
            deltaTime
        );
    }
    private void UpdateRopeLoadState()
    {
        if (bucketIsRestingOnGround)
        {
            SyncDebugValues();
            return;
        }

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

    private void CheckRopeTear(float deltaTime)
    {
        if (bucketIsRestingOnGround)
        {
            overstressTimer = 0.0f;
            return;
        }

        bool shouldBreak =
            ropeLoadModel.ShouldBreak(
                ropeLoadSettings.UseMassEffects,
                ropeLoadSettings.RopeCanTear,
                ropeLoadSettings.MaxRopeTension
            );

        if (!shouldBreak)
        {
            overstressTimer = 0.0f;
            return;
        }

        overstressTimer += deltaTime;

        if (overstressTimer < minimumOverstressTimeBeforeTear)
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
        currentRopeTension =
            ropeLoadModel.CurrentRopeTension;

        currentRopeStretch =
            ropeLoadModel.CurrentRopeStretch;

        ropeIsBroken =
            ropeLoadModel.RopeIsBroken;
    }

    private Vector3 GetPivotPosition()
    {
        return sceneAccess.GetPivotPosition();
    }

    private Vector3 GetBucketRopeAttachPosition()
    {
        return sceneAccess.GetBucketRopeAttachPosition(
            sceneReferences.Bucket != null
                ? sceneReferences.Bucket.position
                : CalculateBucketWorldPosition()
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
}