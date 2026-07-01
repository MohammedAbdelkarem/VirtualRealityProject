using UnityEngine;
using UnityEngine.Rendering;

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
    [SerializeField] private float handleHeight = 0.12f;
    [SerializeField] private float handleAttachWidthFactor = 0.55f;
    [SerializeField] private float handleWidth = 0.006f;

    [Header("Visual Materials")]
    [SerializeField] private Color bucketColor = new Color(0.70f, 0.72f, 0.74f, 1.0f);
    [SerializeField] private Color innerBucketColor = new Color(0.50f, 0.52f, 0.54f, 1.0f);
    [SerializeField] private Color rimColor = new Color(0.86f, 0.86f, 0.82f, 1.0f);
    [SerializeField] private Color handleColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
    [SerializeField] private Color drainOutletColor = new Color(0.35f, 0.35f, 0.35f, 1.0f);

    [Header("Generated Object Names")]
    [SerializeField] private string shellObjectName = "Generated_BucketShell";
    [SerializeField] private string handleObjectName = "Generated_BucketHandle";
    [SerializeField] private string rimObjectName = "Generated_TopRim";
    [SerializeField] private string ropeAttachmentPointName = "Generated_RopeAttachmentPoint";
    [SerializeField] private string ropeKnotObjectName = "Generated_RopeKnot";
    [SerializeField] private string drainOutletObjectName = "Generated_DrainOutlet";
    [SerializeField] private string liquidExitPointName = "Generated_LiquidExitPoint";

    [Header("Box Shape (used instead of cylinder)")]
    [SerializeField] private float boxInnerHalfX = 0.18f;
    [SerializeField] private float boxInnerHalfZ = 0.18f;
    [SerializeField] private Color boxColor = new Color(0.65f, 0.8f, 0.95f, 0.55f);
    [SerializeField] private Color rimColorBox = new Color(0.9f, 0.88f, 0.82f, 1f);
    [SerializeField] private float rimThickness = 0.02f;
    [SerializeField] private float drainOutletOuterRadiusBox = 0.035f;

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
        // Destroy only previously generated objects
        string[] generatedNames = new[] { shellObjectName, handleObjectName, rimObjectName, ropeAttachmentPointName, ropeKnotObjectName, drainOutletObjectName, liquidExitPointName };
        foreach (string name in generatedNames)
        {
            Transform t = transform.Find(name);
            if (t != null) DestroyImmediate(t.gameObject);
        }

        // Dimensions
        float halfW = boxInnerHalfX + wallThickness;
        float halfD = boxInnerHalfZ + wallThickness;
        float topYRim = -handleHeight - wallThickness;
        float baseTopY = -handleHeight - height + wallThickness;

        // ---- Open-top bucket shell (bottom + 4 walls, no top face) ----
        var shell = new GameObject(shellObjectName);
        shell.transform.SetParent(transform, false);
        Mesh mesh = new Mesh();

        Vector3[] verts = new Vector3[]
        {
            // Bottom face (normal down)
            new(-halfW, baseTopY, -halfD), new( halfW, baseTopY, -halfD),
            new( halfW, baseTopY,  halfD), new(-halfW, baseTopY,  halfD),
            // Front wall (+Z)
            new(-halfW, baseTopY, halfD), new( halfW, baseTopY, halfD),
            new( halfW, topYRim, halfD), new(-halfW, topYRim, halfD),
            // Back wall (-Z)
            new( halfW, baseTopY, -halfD), new(-halfW, baseTopY, -halfD),
            new(-halfW, topYRim, -halfD), new( halfW, topYRim, -halfD),
            // Left wall (-X)
            new(-halfW, baseTopY, -halfD), new(-halfW, baseTopY,  halfD),
            new(-halfW, topYRim,  halfD), new(-halfW, topYRim, -halfD),
            // Right wall (+X)
            new( halfW, baseTopY,  halfD), new( halfW, baseTopY, -halfD),
            new( halfW, topYRim, -halfD), new( halfW, topYRim,  halfD),
        };

        int[] tris = new int[]
        {
            // Bottom
            0,1,2, 0,2,3,
            // Front (+Z)
            4,5,6, 4,6,7,
            // Back (-Z)
            8,9,10, 8,10,11,
            // Left (-X)
            12,13,14, 12,14,15,
            // Right (+X)
            16,17,18, 16,18,19,
        };

        Vector3[] normals = new Vector3[]
        {
            // Bottom
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            // Front
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            // Back
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            // Left
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            // Right
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,
        };

        Vector2[] uvs = new Vector2[]
        {
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.normals = normals;
        mesh.uv = uvs;
        shell.AddComponent<MeshFilter>().sharedMesh = mesh;
        var shellRend = shell.AddComponent<MeshRenderer>();
        var shellMat = new Material(Shader.Find("Standard"));
        shellMat.SetFloat("_Mode", 3f);
        shellMat.SetOverrideTag("RenderType", "Transparent");
        shellMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        shellMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        shellMat.SetInt("_ZWrite", 0);
        shellMat.renderQueue = (int)RenderQueue.Transparent;
        shellMat.EnableKeyword("_ALPHABLEND_ON");
        shellMat.SetInt("_Cull", 0);
        shellMat.color = boxColor;
        shellRend.sharedMaterial = shellMat;

        // ---- Handle (U-shape) ----
        var handle = new GameObject(handleObjectName);
        handle.transform.SetParent(transform, false);

        float armH = handleHeight - 0.01f;
        float handleTopY = topYRim + armH;
        float attachHalf = halfW * handleAttachWidthFactor;
        var cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        var handleMat = new Material(Shader.Find("Standard"));
        handleMat.color = handleColor;
        handleMat.SetFloat("_Metallic", 0.3f);
        handleMat.SetFloat("_Glossiness", 0.5f);

        // Left arm
        var left = new GameObject("HandleArm_L");
        left.transform.SetParent(handle.transform, false);
        left.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        left.AddComponent<MeshRenderer>().sharedMaterial = handleMat;
        left.transform.localPosition = new Vector3(-attachHalf, topYRim + armH * 0.5f, 0);
        left.transform.localScale = new Vector3(handleWidth, armH, handleWidth);

        // Right arm
        var right = new GameObject("HandleArm_R");
        right.transform.SetParent(handle.transform, false);
        right.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        right.AddComponent<MeshRenderer>().sharedMaterial = handleMat;
        right.transform.localPosition = new Vector3(attachHalf, topYRim + armH * 0.5f, 0);
        right.transform.localScale = new Vector3(handleWidth, armH, handleWidth);

        // Top bar
        var topBar = new GameObject("HandleBar_Top");
        topBar.transform.SetParent(handle.transform, false);
        topBar.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        topBar.AddComponent<MeshRenderer>().sharedMaterial = handleMat;
        topBar.transform.localPosition = new Vector3(0, handleTopY, 0);
        topBar.transform.localScale = new Vector3(attachHalf * 2f, handleWidth, handleWidth);

        // ---- Top rim (shows the opening clearly) ----
        var rim = new GameObject(rimObjectName);
        rim.transform.SetParent(transform, false);
        var rimMat = new Material(Shader.Find("Standard"));
        rimMat.color = rimColorBox;
        float rt = rimThickness;

        float rimExt = halfW + rt * 0.5f;
        float rimExtZ = halfD + rt * 0.5f;

        // Front rim bar (overlaps corners)
        var rimF = new GameObject("Rim_F");
        rimF.transform.SetParent(rim.transform, false);
        rimF.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        rimF.AddComponent<MeshRenderer>().sharedMaterial = rimMat;
        rimF.transform.localPosition = new Vector3(0, topYRim, halfD);
        rimF.transform.localScale = new Vector3(rimExt * 2f, rt, rt);

        // Back rim bar
        var rimB = new GameObject("Rim_B");
        rimB.transform.SetParent(rim.transform, false);
        rimB.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        rimB.AddComponent<MeshRenderer>().sharedMaterial = rimMat;
        rimB.transform.localPosition = new Vector3(0, topYRim, -halfD);
        rimB.transform.localScale = new Vector3(rimExt * 2f, rt, rt);

        // Left rim bar
        var rimL = new GameObject("Rim_L");
        rimL.transform.SetParent(rim.transform, false);
        rimL.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        rimL.AddComponent<MeshRenderer>().sharedMaterial = rimMat;
        rimL.transform.localPosition = new Vector3(-halfW, topYRim, 0);
        rimL.transform.localScale = new Vector3(rt, rt, rimExtZ * 2f);

        // Right rim bar
        var rimR = new GameObject("Rim_R");
        rimR.transform.SetParent(rim.transform, false);
        rimR.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        rimR.AddComponent<MeshRenderer>().sharedMaterial = rimMat;
        rimR.transform.localPosition = new Vector3(halfW, topYRim, 0);
        rimR.transform.localScale = new Vector3(rt, rt, rimExtZ * 2f);

        // ---- Drain outlet (sticking below the bucket) ----
        var cylinderMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var drain = new GameObject(drainOutletObjectName);
        drain.transform.SetParent(transform, false);
        var drainRend = drain.AddComponent<MeshRenderer>();
        var drainMat = new Material(Shader.Find("Standard"));
        drainMat.color = drainOutletColor;
        drainMat.SetFloat("_Metallic", 0.4f);
        drainMat.SetFloat("_Glossiness", 0.6f);
        drainRend.sharedMaterial = drainMat;
        drain.AddComponent<MeshFilter>().sharedMesh = cylinderMesh;
        float drainLen = 0.04f;
        float drainOuterR = drainOutletOuterRadiusBox;
        drain.transform.localPosition = new Vector3(0f, baseTopY - drainLen * 0.5f, 0f);
        drain.transform.localScale = new Vector3(drainOuterR * 2f, drainLen, drainOuterR * 2f);

        // Rope attachment point at top center of handle
        var attach = new GameObject(ropeAttachmentPointName);
        attach.transform.SetParent(transform, false);
        attach.transform.localPosition = new Vector3(0f, handleTopY + handleWidth * 0.5f, 0f);
        RopeAttachmentPoint = attach.transform;

        // Liquid exit point (at drain hole)
        var exit = new GameObject(liquidExitPointName);
        exit.transform.SetParent(transform, false);
        exit.transform.localPosition = new Vector3(0f, baseTopY, 0f);
        LiquidExitPoint = exit.transform;
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