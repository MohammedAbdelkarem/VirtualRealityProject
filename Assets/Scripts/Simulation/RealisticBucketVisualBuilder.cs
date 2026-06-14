using System.Collections.Generic;
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
        ClearGeneratedVisualChildren();

        float topY = -handleHeight;
        float bottomY = topY - height;
        float outletEndY = createDrainOutlet ? bottomY - drainOutletLength : bottomY;

        GameObject shellObject = CreateChild(shellObjectName);
        MeshFilter meshFilter = shellObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = shellObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = BuildBucketMesh();

        Material[] materials = new Material[4];
        materials[0] = CreateMaterial(bucketColor);
        materials[1] = CreateMaterial(innerBucketColor);
        materials[2] = CreateMaterial(rimColor);
        materials[3] = CreateMaterial(drainOutletColor);
        meshRenderer.sharedMaterials = materials;

        GameObject handleObject = CreateChild(handleObjectName);
        LineRenderer handleRenderer = handleObject.AddComponent<LineRenderer>();
        SetupHandleRenderer(handleRenderer);

        RopeAttachmentPoint = CreateOrUpdatePoint(
            ropeAttachmentPointName,
            new Vector3(0.0f, 0.0f, 0.0f)
        );

        CreateRopeKnotVisual();

        if (createDrainOutlet)
        {
            GameObject outletObject = CreateChild(drainOutletObjectName);
            MeshFilter outletMeshFilter = outletObject.AddComponent<MeshFilter>();
            MeshRenderer outletMeshRenderer = outletObject.AddComponent<MeshRenderer>();

            outletMeshFilter.sharedMesh = BuildDrainOutletMesh(bottomY, outletEndY);
            outletMeshRenderer.sharedMaterial = CreateMaterial(drainOutletColor);
        }

        LiquidExitPoint = CreateOrUpdatePoint(
            liquidExitPointName,
            new Vector3(0.0f, outletEndY - 0.02f, 0.0f)
        );
    }

    private Mesh BuildBucketMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Hollow_Bucket_With_Base_And_Drain_Hole";

        List<Vector3> vertices = new List<Vector3>();
        List<int> outerTriangles = new List<int>();
        List<int> innerTriangles = new List<int>();
        List<int> rimTriangles = new List<int>();
        List<int> baseTriangles = new List<int>();

        float topY = -handleHeight;
        float bottomY = topY - height;
        float baseTopY = bottomY + wallThickness;

        float innerTopRadius = Mathf.Max(0.01f, topRadius - wallThickness);
        float innerBottomRadius = Mathf.Max(0.01f, bottomRadius - wallThickness);
        float safeDrainHoleRadius = Mathf.Clamp(drainHoleRadius, 0.01f, innerBottomRadius * 0.65f);

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(topRadius * cos, topY, topRadius * sin));
            vertices.Add(new Vector3(bottomRadius * cos, bottomY, bottomRadius * sin));
            vertices.Add(new Vector3(innerTopRadius * cos, topY - wallThickness, innerTopRadius * sin));
            vertices.Add(new Vector3(innerBottomRadius * cos, baseTopY, innerBottomRadius * sin));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int topOuterA = i * 4;
            int bottomOuterA = i * 4 + 1;
            int topInnerA = i * 4 + 2;
            int bottomInnerA = i * 4 + 3;

            int topOuterB = next * 4;
            int bottomOuterB = next * 4 + 1;
            int topInnerB = next * 4 + 2;
            int bottomInnerB = next * 4 + 3;

            AddQuad(outerTriangles, topOuterA, topOuterB, bottomOuterB, bottomOuterA);
            AddQuad(innerTriangles, topInnerB, topInnerA, bottomInnerA, bottomInnerB);
            AddQuad(rimTriangles, topOuterB, topOuterA, topInnerA, topInnerB);

            if (!createBottomBase)
            {
                AddQuad(rimTriangles, bottomOuterA, bottomOuterB, bottomInnerB, bottomInnerA);
            }
        }

        if (createBottomBase)
        {
            int baseStart = vertices.Count;

            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * Mathf.PI * i / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(innerBottomRadius * cos, baseTopY, innerBottomRadius * sin));
                vertices.Add(new Vector3(safeDrainHoleRadius * cos, baseTopY, safeDrainHoleRadius * sin));
                vertices.Add(new Vector3(bottomRadius * cos, bottomY, bottomRadius * sin));
                vertices.Add(new Vector3(safeDrainHoleRadius * cos, bottomY, safeDrainHoleRadius * sin));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                int topOuterA = baseStart + i * 4;
                int topHoleA = baseStart + i * 4 + 1;
                int bottomOuterA = baseStart + i * 4 + 2;
                int bottomHoleA = baseStart + i * 4 + 3;

                int topOuterB = baseStart + next * 4;
                int topHoleB = baseStart + next * 4 + 1;
                int bottomOuterB = baseStart + next * 4 + 2;
                int bottomHoleB = baseStart + next * 4 + 3;

                AddTopAnnulusQuad(baseTriangles, topOuterA, topOuterB, topHoleA, topHoleB);
                AddBottomAnnulusQuad(baseTriangles, bottomOuterA, bottomOuterB, bottomHoleA, bottomHoleB);
                AddDoubleSidedQuad(baseTriangles, topHoleA, topHoleB, bottomHoleB, bottomHoleA);
            }
        }

        mesh.SetVertices(vertices);
        mesh.subMeshCount = 4;
        mesh.SetTriangles(outerTriangles, 0);
        mesh.SetTriangles(innerTriangles, 1);
        mesh.SetTriangles(rimTriangles, 2);
        mesh.SetTriangles(baseTriangles, 3);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Mesh BuildDrainOutletMesh(float topY, float bottomY)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Cylindrical_Drain_Outlet";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float innerRadius = Mathf.Clamp(drainHoleRadius, 0.01f, drainOutletOuterRadius * 0.75f);
        float outerRadius = Mathf.Max(drainOutletOuterRadius, innerRadius + 0.01f);

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(outerRadius * cos, topY, outerRadius * sin));
            vertices.Add(new Vector3(outerRadius * cos, bottomY, outerRadius * sin));
            vertices.Add(new Vector3(innerRadius * cos, topY, innerRadius * sin));
            vertices.Add(new Vector3(innerRadius * cos, bottomY, innerRadius * sin));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int outerTopA = i * 4;
            int outerBottomA = i * 4 + 1;
            int innerTopA = i * 4 + 2;
            int innerBottomA = i * 4 + 3;

            int outerTopB = next * 4;
            int outerBottomB = next * 4 + 1;
            int innerTopB = next * 4 + 2;
            int innerBottomB = next * 4 + 3;

            AddQuad(triangles, outerTopA, outerTopB, outerBottomB, outerBottomA);
            AddQuad(triangles, innerTopB, innerTopA, innerBottomA, innerBottomB);

            AddDoubleSidedQuad(triangles, outerBottomA, outerBottomB, innerBottomB, innerBottomA);
            AddDoubleSidedQuad(triangles, outerTopB, outerTopA, innerTopA, innerTopB);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void SetupHandleRenderer(LineRenderer handleRenderer)
    {
        handleRenderer.useWorldSpace = false;
        handleRenderer.positionCount = 17;
        handleRenderer.startWidth = handleWidth;
        handleRenderer.endWidth = handleWidth;
        handleRenderer.numCornerVertices = 8;
        handleRenderer.numCapVertices = 8;
        handleRenderer.material = CreateLineMaterial(handleColor);
        handleRenderer.startColor = handleColor;
        handleRenderer.endColor = handleColor;

        float topY = -handleHeight;
        float attachX = topRadius * handleAttachWidthFactor;
        float sideY = topY + 0.06f;

        for (int i = 0; i < handleRenderer.positionCount; i++)
        {
            float t = i / (float)(handleRenderer.positionCount - 1);
            float x = Mathf.Lerp(-attachX, attachX, t);
            float arch = 1.0f - Mathf.Pow((t - 0.5f) * 2.0f, 2.0f);
            float y = Mathf.Lerp(sideY, 0.0f, arch);

            handleRenderer.SetPosition(i, new Vector3(x, y, 0.0f));
        }
    }

    private void CreateRopeKnotVisual()
    {
        GameObject knotObject = CreateChild(ropeKnotObjectName);
        knotObject.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = knotObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = knotObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = BuildSmallSphereMesh(handleWidth * 1.6f, 10, 16);
        meshRenderer.sharedMaterial = CreateMaterial(handleColor);
    }

    private Mesh BuildSmallSphereMesh(float radius, int rings, int sectors)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Rope_Knot";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int ring = 0; ring <= rings; ring++)
        {
            float verticalT = ring / (float)rings;
            float phi = Mathf.PI * verticalT;
            float y = Mathf.Cos(phi) * radius;
            float ringRadius = Mathf.Sin(phi) * radius;

            for (int sector = 0; sector <= sectors; sector++)
            {
                float horizontalT = sector / (float)sectors;
                float theta = horizontalT * Mathf.PI * 2.0f;

                float x = Mathf.Cos(theta) * ringRadius;
                float z = Mathf.Sin(theta) * ringRadius;

                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int sector = 0; sector < sectors; sector++)
            {
                int current = ring * (sectors + 1) + sector;
                int next = current + sectors + 1;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);

        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    private void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
    {
        AddQuad(triangles, a, b, c, d);
        AddQuad(triangles, d, c, b, a);
    }

    private void AddTopAnnulusQuad(List<int> triangles, int outerA, int outerB, int holeA, int holeB)
    {
        triangles.Add(outerA);
        triangles.Add(holeA);
        triangles.Add(holeB);

        triangles.Add(outerA);
        triangles.Add(holeB);
        triangles.Add(outerB);
    }

    private void AddBottomAnnulusQuad(List<int> triangles, int outerA, int outerB, int holeA, int holeB)
    {
        triangles.Add(outerA);
        triangles.Add(outerB);
        triangles.Add(holeB);

        triangles.Add(outerA);
        triangles.Add(holeB);
        triangles.Add(holeA);
    }

    private GameObject CreateChild(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child;
    }

    private Transform CreateOrUpdatePoint(string objectName, Vector3 localPosition)
    {
        Transform pointTransform = transform.Find(objectName);

        if (pointTransform == null)
        {
            GameObject pointObject = new GameObject(objectName);
            pointTransform = pointObject.transform;
            pointTransform.SetParent(transform, false);
        }

        pointTransform.localPosition = localPosition;
        pointTransform.localRotation = Quaternion.identity;
        pointTransform.localScale = Vector3.one;

        return pointTransform;
    }

    private void ClearGeneratedVisualChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            bool isGeneratedVisual =
                child.name == shellObjectName ||
                child.name == handleObjectName ||
                child.name == ropeKnotObjectName ||
                child.name == drainOutletObjectName;

            if (!isGeneratedVisual)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;

        return material;
    }

    private Material CreateLineMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.color = color;

        return material;
    }

    private void ClampValues()
    {
        topRadius = Mathf.Max(0.05f, topRadius);
        bottomRadius = Mathf.Max(0.03f, bottomRadius);
        height = Mathf.Max(0.10f, height);

        wallThickness = Mathf.Clamp(
            wallThickness,
            0.005f,
            Mathf.Min(topRadius, bottomRadius) * 0.45f
        );

        segments = Mathf.Clamp(segments, 12, 128);

        handleHeight = Mathf.Max(0.05f, handleHeight);
        handleAttachWidthFactor = Mathf.Clamp(handleAttachWidthFactor, 0.2f, 1.0f);
        handleWidth = Mathf.Clamp(handleWidth, 0.005f, 0.08f);

        float innerBottomRadius = Mathf.Max(0.01f, bottomRadius - wallThickness);

        drainHoleRadius = Mathf.Clamp(
            drainHoleRadius,
            0.01f,
            innerBottomRadius * 0.65f
        );

        drainOutletOuterRadius = Mathf.Max(
            drainOutletOuterRadius,
            drainHoleRadius + 0.01f
        );

        drainOutletLength = Mathf.Clamp(drainOutletLength, 0.02f, 0.40f);
    }
}