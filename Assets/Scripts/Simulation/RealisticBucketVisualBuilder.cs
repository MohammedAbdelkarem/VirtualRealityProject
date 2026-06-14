using UnityEngine;

[ExecuteAlways]
public class RealisticBucketVisualBuilder : MonoBehaviour
{
    [Header("Bucket Shape")]
    [SerializeField] private float topRadius = 0.20f;
    [SerializeField] private float bottomRadius = 0.14f;
    [SerializeField] private float height = 0.30f;
    [SerializeField] private float wallThickness = 0.015f;
    [SerializeField] private int segments = 64;

    [Header("Handle / Grip")]
    [SerializeField] private float handleHeight = 0.18f;
    [SerializeField] private float handleAttachWidthFactor = 0.82f;
    [SerializeField] private float handleWidth = 0.012f;

    [Header("Rope Join / Knot")]
    [SerializeField] private bool showRopeKnot = true;
    [SerializeField] private float knotRadius = 0.025f;
    [SerializeField] private float knotWidth = 0.008f;

    [Header("Visual Materials")]
    [SerializeField] private Color bucketColor = new Color(0.70f, 0.72f, 0.74f, 1.0f);
    [SerializeField] private Color innerBucketColor = new Color(0.42f, 0.44f, 0.45f, 1.0f);
    [SerializeField] private Color rimColor = new Color(0.86f, 0.86f, 0.82f, 1.0f);
    [SerializeField] private Color handleColor = new Color(0.10f, 0.10f, 0.10f, 1.0f);
    [SerializeField] private Color knotColor = new Color(0.08f, 0.05f, 0.025f, 1.0f);

    [Header("Generated Object Names")]
    [SerializeField] private string shellObjectName = "Generated_BucketShell";
    [SerializeField] private string handleObjectName = "Generated_BucketHandle";
    [SerializeField] private string ropeAttachmentObjectName = "Generated_RopeAttachmentPoint";
    [SerializeField] private string ropeKnotObjectName = "Generated_RopeKnot";

    public Transform RopeAttachmentPoint { get; private set; }

    private void Awake()
    {
        BuildBucket();
    }

    private void Start()
    {
        BuildBucket();
    }

    private void OnEnable()
    {
        BuildBucket();
    }

    private void OnValidate()
    {
        topRadius = Mathf.Max(0.05f, topRadius);
        bottomRadius = Mathf.Max(0.03f, bottomRadius);
        height = Mathf.Max(0.10f, height);
        wallThickness = Mathf.Clamp(wallThickness, 0.005f, Mathf.Min(topRadius, bottomRadius) * 0.45f);
        segments = Mathf.Clamp(segments, 12, 128);
        handleHeight = Mathf.Max(0.05f, handleHeight);
        handleAttachWidthFactor = Mathf.Clamp(handleAttachWidthFactor, 0.2f, 1.0f);
        handleWidth = Mathf.Clamp(handleWidth, 0.005f, 0.08f);
        knotRadius = Mathf.Clamp(knotRadius, 0.005f, 0.10f);
        knotWidth = Mathf.Clamp(knotWidth, 0.002f, 0.04f);
    }

    [ContextMenu("Rebuild Bucket")]
    public void BuildBucket()
    {
        ClearGeneratedChildren();

        GameObject shellObject = CreateChild(shellObjectName);
        MeshFilter meshFilter = shellObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = shellObject.AddComponent<MeshRenderer>();

        Mesh bucketMesh = BuildBucketMesh();
        meshFilter.sharedMesh = bucketMesh;

        Material[] materials = new Material[3];
        materials[0] = CreateMaterial(bucketColor);
        materials[1] = CreateMaterial(innerBucketColor);
        materials[2] = CreateMaterial(rimColor);
        meshRenderer.sharedMaterials = materials;

        GameObject handleObject = CreateChild(handleObjectName);
        LineRenderer handleRenderer = handleObject.AddComponent<LineRenderer>();
        SetupHandleRenderer(handleRenderer);

        GameObject attachmentObject = CreateChild(ropeAttachmentObjectName);
        RopeAttachmentPoint = attachmentObject.transform;
        RopeAttachmentPoint.localPosition = Vector3.zero;
        RopeAttachmentPoint.localRotation = Quaternion.identity;
        RopeAttachmentPoint.localScale = Vector3.one;

        if (showRopeKnot)
        {
            GameObject knotObject = CreateChild(ropeKnotObjectName);
            LineRenderer knotRenderer = knotObject.AddComponent<LineRenderer>();
            SetupKnotRenderer(knotRenderer);
        }
    }

    private Mesh BuildBucketMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Hollow_Tapered_Bucket";

        System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<int> outerTriangles = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<int> innerTriangles = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<int> rimTriangles = new System.Collections.Generic.List<int>();

        float topY = -handleHeight;
        float bottomY = topY - height;

        float innerTopRadius = Mathf.Max(0.01f, topRadius - wallThickness);
        float innerBottomRadius = Mathf.Max(0.01f, bottomRadius - wallThickness);

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(topRadius * cos, topY, topRadius * sin));
            vertices.Add(new Vector3(bottomRadius * cos, bottomY, bottomRadius * sin));
            vertices.Add(new Vector3(innerTopRadius * cos, topY - wallThickness, innerTopRadius * sin));
            vertices.Add(new Vector3(innerBottomRadius * cos, bottomY + wallThickness, innerBottomRadius * sin));
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
            AddQuad(rimTriangles, bottomOuterA, bottomOuterB, bottomInnerB, bottomInnerA);
        }

        mesh.SetVertices(vertices);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(outerTriangles, 0);
        mesh.SetTriangles(innerTriangles, 1);
        mesh.SetTriangles(rimTriangles, 2);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void SetupHandleRenderer(LineRenderer handleRenderer)
    {
        handleRenderer.useWorldSpace = false;
        handleRenderer.positionCount = 21;
        handleRenderer.startWidth = handleWidth;
        handleRenderer.endWidth = handleWidth;
        handleRenderer.numCornerVertices = 8;
        handleRenderer.numCapVertices = 8;
        handleRenderer.material = CreateLineMaterial(handleColor);
        handleRenderer.startColor = handleColor;
        handleRenderer.endColor = handleColor;

        float topRimY = -handleHeight;
        float attachX = topRadius * handleAttachWidthFactor;

        for (int i = 0; i < handleRenderer.positionCount; i++)
        {
            float t = i / (float)(handleRenderer.positionCount - 1);
            float x = Mathf.Lerp(-attachX, attachX, t);

            /*
             * Endpoints are exactly on the bucket rim.
             * Middle point is exactly at local origin (0,0,0).
             * The rope attachment object is also at local origin.
             */

            float arch = 1.0f - Mathf.Pow((t - 0.5f) * 2.0f, 2.0f);
            float y = Mathf.Lerp(topRimY, 0.0f, arch);

            handleRenderer.SetPosition(i, new Vector3(x, y, 0.0f));
        }
    }

    private void SetupKnotRenderer(LineRenderer knotRenderer)
    {
        int pointCount = 33;

        knotRenderer.useWorldSpace = false;
        knotRenderer.positionCount = pointCount;
        knotRenderer.startWidth = knotWidth;
        knotRenderer.endWidth = knotWidth;
        knotRenderer.numCornerVertices = 8;
        knotRenderer.numCapVertices = 8;
        knotRenderer.material = CreateLineMaterial(knotColor);
        knotRenderer.startColor = knotColor;
        knotRenderer.endColor = knotColor;

        /*
         * Small visual ring at local origin.
         * This makes the rope-to-handle joint visible.
         */

        for (int i = 0; i < pointCount; i++)
        {
            float angle = 2.0f * Mathf.PI * i / (pointCount - 1);
            float x = Mathf.Cos(angle) * knotRadius;
            float y = Mathf.Sin(angle) * knotRadius;

            knotRenderer.SetPosition(i, new Vector3(x, y, 0.0f));
        }
    }

    private void AddQuad(System.Collections.Generic.List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);

        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
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

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (
                child.name.StartsWith(shellObjectName) ||
                child.name.StartsWith(handleObjectName) ||
                child.name.StartsWith(ropeAttachmentObjectName) ||
                child.name.StartsWith(ropeKnotObjectName)
            )
            {
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

        RopeAttachmentPoint = null;
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
}
