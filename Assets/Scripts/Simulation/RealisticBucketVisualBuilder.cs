using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RealisticBucketVisualBuilder : MonoBehaviour
{
    [Header("Bucket Shape")]
    [SerializeField] private float topRadius = 0.32f;
    [SerializeField] private float bottomRadius = 0.22f;
    [SerializeField] private float height = 0.48f;
    [SerializeField] private float wallThickness = 0.025f;
    [SerializeField] private int segments = 64;

    [Header("Bottom Base / Drain Opening")]
    [SerializeField] private bool createBottomBase = true;
    [SerializeField] private float drainHoleRadius = 0.045f;
    [SerializeField] private bool createDrainOutlet = true;
    [SerializeField] private float drainOutletOuterRadius = 0.07f;
    [SerializeField] private float drainOutletLength = 0.12f;

    [Header("Handle / Grip")]
    [SerializeField] private float handleHeight = 0.28f;
    [SerializeField] private float handleAttachWidthFactor = 0.82f;
    [SerializeField] private float handleWidth = 0.025f;

    [Header("Visual Materials")]
    [SerializeField] private Color bucketColor = new Color(0.70f, 0.72f, 0.74f, 1.0f);
    [SerializeField] private Color innerBucketColor = new Color(0.50f, 0.52f, 0.54f, 1.0f);
    [SerializeField] private Color rimColor = new Color(0.86f, 0.86f, 0.82f, 1.0f);
    [SerializeField] private Color handleColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
    [SerializeField] private Color drainOutletColor = new Color(0.35f, 0.35f, 0.35f, 1.0f);

    [Header("Generated Object Names")]
    [SerializeField] private string shellObjectName = "Generated_BucketShell";
    [SerializeField] private string handleObjectName = "Generated_BucketHandle";
    [SerializeField] private string ropeAttachmentPointName = "Generated_RopeAttachmentPoint";
    [SerializeField] private string ropeKnotObjectName = "Generated_RopeKnot";
    [SerializeField] private string drainOutletObjectName = "Generated_DrainOutlet";
    [SerializeField] private string liquidExitPointName = "Generated_LiquidExitPoint";

    public Transform RopeAttachmentPoint { get; private set; }
    public Transform LiquidExitPoint { get; private set; }

    public float TopRadius => topRadius;
    public float BottomRadius => bottomRadius;
    public float Height => height;
    public float WallThickness => wallThickness;
    public float HandleHeight => handleHeight;
    public float DrainHoleRadius => drainHoleRadius;

#if UNITY_EDITOR
    private bool editorRebuildQueued;
#endif

    private void OnEnable()
    {
        ClampValues();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRebuild();
            return;
        }
#endif

        BuildBucket();
    }

    private void Start()
    {
        ClampValues();
        BuildBucket();
    }

    private void OnValidate()
    {
        ClampValues();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRebuild();
        }
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorRebuild()
    {
        if (editorRebuildQueued)
        {
            return;
        }

        editorRebuildQueued = true;
        EditorApplication.delayCall += DelayedEditorRebuild;
    }

    private void DelayedEditorRebuild()
    {
        editorRebuildQueued = false;

        if (this == null)
        {
            return;
        }

        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        ClampValues();
        BuildBucket();

        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
    }
#endif

    [ContextMenu("Rebuild Bucket Visuals")]
    public void BuildBucket()
    {
        BucketVisualBuildRequest request =
            CreateBuildRequest();

        BucketVisualBuildResult result =
            BucketVisualAssembler.Build(request);

        RopeAttachmentPoint = result.RopeAttachmentPoint;
        LiquidExitPoint = result.LiquidExitPoint;
    }

    private BucketVisualBuildRequest CreateBuildRequest()
    {
        return new BucketVisualBuildRequest(
            transform,
            Application.isPlaying,
            topRadius,
            bottomRadius,
            height,
            wallThickness,
            segments,
            createBottomBase,
            drainHoleRadius,
            createDrainOutlet,
            drainOutletOuterRadius,
            drainOutletLength,
            handleHeight,
            handleAttachWidthFactor,
            handleWidth,
            bucketColor,
            innerBucketColor,
            rimColor,
            handleColor,
            drainOutletColor,
            shellObjectName,
            handleObjectName,
            ropeAttachmentPointName,
            ropeKnotObjectName,
            drainOutletObjectName,
            liquidExitPointName
        );
    }

    private void ClampValues()
    {
        BucketVisualParameterValidator.Clamp(
            ref topRadius,
            ref bottomRadius,
            ref height,
            ref wallThickness,
            ref segments,
            ref handleHeight,
            ref handleAttachWidthFactor,
            ref handleWidth,
            ref drainHoleRadius,
            ref drainOutletOuterRadius,
            ref drainOutletLength
        );
    }
}