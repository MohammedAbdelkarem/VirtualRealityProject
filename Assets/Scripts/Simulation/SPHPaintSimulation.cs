using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class SPHPaintSimulation : MonoBehaviour
{
    [Header("Particle Settings")]
    public float particleRadius = 0.007f;
    public float particleMass = 0.06f;
    public int substeps = 1;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 25f;
    [Range(0f, 1f)]
    public float viscosity = 0.08f;
    public float surfaceTension = 0.08f;
    public float gravityAccel = -9.81f;
    [Range(0.5f, 2f)]
    public float particleSpacing = 1f;
    [Range(0.5f, 2f)]
    public float bucketScale = 1f;
    [Range(0f, 10f)]
    public float swirl = 0f;
    [Range(0.3f, 3f)]
    public float particleSizeScale = 1f;
    [Range(0f, 1f)]
    public float particleHue = 0f;
    public int relaxSteps = 3;
    [Range(0.5f, 1f)]
    public float velocityDamping = 0.9f;

    [Header("GPU Compute")]
    public ComputeShader computeShader;

    [Header("Fill")]
    [Range(0f, 1f)]
    public float fillLevel = 1f;

    [Header("Colors")]
    public Color[] colorPalette = new Color[] {
        new Color(1f, 0.15f, 0.1f),
        new Color(0.1f, 0.5f, 1f),
        new Color(1f, 0.85f, 0f),
        new Color(0f, 0.95f, 0.3f),
        new Color(1f, 0.2f, 0.6f),
        new Color(0.6f, 0.15f, 1f)
    };
    [Range(0f, 10f)]
    public float colorMixRate = 3f;

    [Header("Appearance")]
    [Range(0f, 1f)]
    public float glossiness = 0.85f;
    [Range(0f, 1f)]
    public float metallic = 0.1f;

    [HideInInspector]
    public float ropeGravityMultiplier = 0f;

    [Header("Drain")]
    public bool drainActive = true;
    [Range(0f, 3f)]
    public float drainRate = 1.5f;
    [Range(0.1f, 3f)]
    public float trailDuration = 1f;
    public float destroyHeight = -3f;
    public DripPanel dripPanel;

    [Header("Panel Material")]
    public DripPanel.PanelMaterial panelMaterial = DripPanel.PanelMaterial.Wood;
    [Header("Wood")]
    public Color woodBaseColor = new Color(0.55f, 0.38f, 0.22f);
    public Color woodGrainColor = new Color(0.35f, 0.22f, 0.12f);
    [Range(0.5f, 4f)]
    public float woodGrainScale = 1.5f;
    [Range(0f, 5f)]
    public float woodAbsorptionRate = 0.8f;
    [Range(0f, 0.5f)]
    public float woodWetSheen = 0.2f;
    [Header("Cloth")]
    public Color clothBaseColor = new Color(0.92f, 0.9f, 0.88f);
    public Color clothThreadColor = new Color(0.8f, 0.78f, 0.75f);
    [Range(0.5f, 8f)]
    public float clothWeaveScale = 3f;
    [Range(0f, 0.5f)]
    public float clothWickingRate = 0.05f;


    [Header("Lissajous Motion (unused — kept for reference)")]
    public bool useLissajousMotion = false;
    public float lissajousAmpX = 2.5f;
    public float lissajousAmpZ = 2.5f;
    [Range(1f, 10f)] public float lissajousFreqX = 3f;
    [Range(1f, 10f)] public float lissajousFreqZ = 4f;
    public float lissajousPhase = 0.5f;
    public float lissajousHeight = 1.5f;

    public bool showDebug;

    private float topY, baseTopY, innerHalfX, innerHalfZ;
    private float drainHoleR, minY, maxY;
    private Vector3[] pos, vel;
    private float[] dens, pres;
    private Color[] colors;
    private bool[] drained;
    private Transform bucketT;
    private int particleCount;
    private bool ready;
    private Material mat, trailMat;
    public Material particleMaterial;
    public Material trailMaterial;

    // GPU compute
    private ComputeBuffer posBuffer, velBuffer, densBuffer, presBuffer, colorsBuffer, drainedBuffer;
    private ComputeBuffer cellCountsBuffer, cellParticlesBuffer;
    private int clearKernel, buildKernel, densityKernel, forcesKernel;
    private int totalCells, gridThreadGroups, particleThreadGroups;
    private Vector3[] gpuPosReadback, gpuColReadback, lastCpuRawPos;
    private int[] gpuSyncDrained;
    private AsyncGPUReadbackRequest posReadbackRequest;
    private AsyncGPUReadbackRequest colReadbackRequest;
    private bool posReadbackPending;
    private bool colReadbackPending;
    private const int CELL_MAX = 256;

    private AdvancedBucketRopeSimulation ropeSimCached;
    private Mesh sphereMesh;
    private MaterialPropertyBlock mpb, panelPB;
    private Matrix4x4[] particleMatrices;
    private Vector4[] particleColors;
    private Vector3 prevBucketPos;
    private Vector3 prevBucketWorldVel;
    private MaterialPropertyBlock instanceProps;
    private Matrix4x4[] batchMatrices;
    private Vector4[] batchColors;
    private float drainTimer;
    private bool ownsPanel;
    private Queue<GameObject> trailPool = new Queue<GameObject>();
    private List<GameObject> activeTrails = new List<GameObject>();
    private LineRenderer streamLine;
    private Vector3 lastBucketPos;
    private float smoothSpeed;

    private float splatDotTimer;
    private Vector3 origPosition;
    private bool wasMoving;

    void Update()
    {
        if (!Application.isPlaying) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h = 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v = 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v = -1f;
        wasMoving = h != 0 || v != 0;

        if (wasMoving && ropeSimCached != null)
            ropeSimCached.enabled = false;
        else if (!wasMoving && ropeSimCached != null && !ropeSimCached.enabled)
            ropeSimCached.enabled = true;

        if (wasMoving)
        {
            bucketT.position += bucketT.TransformDirection(new Vector3(h, 0f, v)) * 3f * Time.deltaTime;
        }

        if (kb.rKey.wasPressedThisFrame)
        {
            if (ropeSimCached != null)
            {
                ropeSimCached.enabled = true;
                ropeSimCached.SetInitialThetaDegrees(0f);
                ropeSimCached.SetInitialPhiVelocity(0f);
            }
            bucketT.position = origPosition;
            prevBucketPos = bucketT.position;
            prevBucketWorldVel = Vector3.zero;
            System.Array.Clear(drained, 0, drained.Length);
            GenerateLattice();
            posBuffer.SetData(pos);
            velBuffer.SetData(vel);
            int[] zeros = new int[particleCount];
            drainedBuffer.SetData(zeros);
        }
    }

    void Start()
    {
        RealisticBucketVisualBuilder b = GetComponentInParent<RealisticBucketVisualBuilder>();
        if (b == null) b = GetComponent<RealisticBucketVisualBuilder>();
        if (b == null) b = GetComponentInChildren<RealisticBucketVisualBuilder>();

        gasStiffness = 25f;
        velocityDamping = 0.95f;

        if (b != null)
        {
            origPosition = b.transform.position;
            bucketT = b.transform;
            innerHalfX = 0.18f;
            innerHalfZ = 0.18f;
            float hh = b.HandleHeight, h = b.Height, wt = b.WallThickness;
            topY = -hh - wt;
            baseTopY = -hh - h + wt;
            drainHoleR = b.DrainHoleRadius + 0.01f;
        }
        else
        {
            Debug.LogWarning("SPHPaintSimulation: No RealisticBucketVisualBuilder. Using default bucket dimensions.");
            origPosition = Vector3.zero;
            bucketT = transform;
            innerHalfX = 0.18f;
            innerHalfZ = 0.18f;
            topY = -0.15f;
            baseTopY = -0.35f;
            drainHoleR = 0.01f;
        }
        prevBucketPos = bucketT.position;
        prevBucketWorldVel = Vector3.zero;

        if (!Application.isPlaying) return;

        lastBucketPos = bucketT.position;
        minY = baseTopY + particleRadius * 1.5f;
        maxY = topY - 0.005f;

        // --- One-time initialization (needed even when empty) ---
        if (dripPanel == null)
            dripPanel = FindFirstObjectByType<DripPanel>();
        if (dripPanel == null)
        {
            GameObject panelGO = new GameObject("DripPanel");
            panelGO.transform.position = new Vector3(0f, 0.02f, 0f);
            panelGO.transform.rotation = Quaternion.identity;
            dripPanel = panelGO.AddComponent<DripPanel>();
            ownsPanel = true;
        }

        ApplyPanelConfigBase();

        ropeSimCached = FindFirstObjectByType<AdvancedBucketRopeSimulation>();
        if (ropeSimCached != null)
        {
            ropeSimCached.SetInitialThetaDegrees(45f);
            ropeSimCached.SetInitialPhiVelocity(2f);
            ropeSimCached.SetRopeLength(2.2f);
            ropeSimCached.SetConstraintIterations(10);
            ropeSimCached.SetDampingPerSecond(0.01f);
            ropeSimCached.SetRopeGravityMultiplier(0f);
        }

        var skyboxShader = Shader.Find("Skybox/Procedural");
        if (skyboxShader != null)
        {
            var skyMat = new Material(skyboxShader);
            skyMat.SetColor("_SkyTint", new Color(0.75f, 0.85f, 1f));
            skyMat.SetColor("_GroundColor", new Color(0.7f, 0.7f, 0.72f));
            skyMat.SetFloat("_AtmosphereThickness", 0.6f);
            skyMat.SetFloat("_Exposure", 1.1f);
            skyMat.SetFloat("_SunSize", 0.03f);
            RenderSettings.skybox = skyMat;
        }

        if (particleMaterial != null)
        {
            mat = new Material(particleMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Custom/FluidParticle");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) { enabled = false; return; }
            mat = new Material(shader);
            if (shader.name == "Standard" || shader.name == "Legacy Shaders/Diffuse")
            {
                mat.SetFloat("_Mode", 2.0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
        }
        if (mat == null) { enabled = false; return; }
        ApplyMaterialProperties();

        Shader trailShader = Shader.Find("Unlit/Transparent");
        if (trailShader == null) trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (trailShader == null) trailShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (trailShader == null) trailShader = Shader.Find("Standard");
        if (trailShader == null) { enabled = false; return; }
        trailMat = new Material(trailShader);
        trailMat.color = Color.white;
        trailMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        trailMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        trailMat.SetInt("_ZWrite", 0);
        trailMat.renderQueue = 3000;

        GameObject streamGO = new GameObject("DrainStream");
        streamGO.transform.SetParent(transform);
        streamLine = streamGO.AddComponent<LineRenderer>();
        streamLine.positionCount = 2;
        streamLine.material = trailMat;
        streamLine.startWidth = particleRadius * 0.8f;
        streamLine.endWidth = particleRadius * 0.1f;

        sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (sphereMesh == null) sphereMesh = BuildSphereMesh();

        mpb = new MaterialPropertyBlock();
        panelPB = new MaterialPropertyBlock();
        instanceProps = new MaterialPropertyBlock();
        mat.enableInstancing = true;

        // --- GPU compute initialization ---
        if (computeShader == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("SPHSimulation t:ComputeShader");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                computeShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            }
#endif
            if (computeShader == null)
                computeShader = Resources.Load<ComputeShader>("SPHSimulation");
        }

        if (computeShader == null)
        {
            Debug.LogError("SPHPaintSimulation: No compute shader assigned or found. Disabling. Drag SPHSimulation.compute to the Compute Shader field in the Inspector.");
            enabled = false;
            return;
        }

        clearKernel = computeShader.FindKernel("ClearGrid");
        buildKernel = computeShader.FindKernel("BuildGrid");
        densityKernel = computeShader.FindKernel("ComputeDensity");
        forcesKernel = computeShader.FindKernel("ComputeForces");

        GenerateLattice();
        Debug.Log("Particle count: " + particleCount);
        if (particleCount == 0) { enabled = false; return; }

        SetupGrid();

        // Create compute buffers
        posBuffer = new ComputeBuffer(particleCount, 12);
        velBuffer = new ComputeBuffer(particleCount, 12);
        densBuffer = new ComputeBuffer(particleCount, 4);
        presBuffer = new ComputeBuffer(particleCount, 4);
        colorsBuffer = new ComputeBuffer(particleCount, 12);
        drainedBuffer = new ComputeBuffer(particleCount, 4);
        cellCountsBuffer = new ComputeBuffer(totalCells, 4);
        cellParticlesBuffer = new ComputeBuffer(totalCells * CELL_MAX, 4);

        // Upload initial data
        Vector3[] initColors = new Vector3[particleCount];
        for (int i = 0; i < particleCount; i++)
            initColors[i] = new Vector3(colors[i].r, colors[i].g, colors[i].b);
        int[] initDrained = new int[particleCount];

        posBuffer.SetData(pos);
        velBuffer.SetData(vel);
        colorsBuffer.SetData(initColors);
        drainedBuffer.SetData(initDrained);
        // Set buffers on all kernels
        computeShader.SetBuffer(clearKernel, "pos", posBuffer);
        computeShader.SetBuffer(clearKernel, "vel", velBuffer);
        computeShader.SetBuffer(clearKernel, "dens", densBuffer);
        computeShader.SetBuffer(clearKernel, "pres", presBuffer);
        computeShader.SetBuffer(clearKernel, "colors", colorsBuffer);
        computeShader.SetBuffer(clearKernel, "drained", drainedBuffer);
        computeShader.SetBuffer(clearKernel, "cellCounts", cellCountsBuffer);
        computeShader.SetBuffer(clearKernel, "cellParticles", cellParticlesBuffer);

        computeShader.SetBuffer(buildKernel, "pos", posBuffer);
        computeShader.SetBuffer(buildKernel, "vel", velBuffer);
        computeShader.SetBuffer(buildKernel, "dens", densBuffer);
        computeShader.SetBuffer(buildKernel, "pres", presBuffer);
        computeShader.SetBuffer(buildKernel, "colors", colorsBuffer);
        computeShader.SetBuffer(buildKernel, "drained", drainedBuffer);
        computeShader.SetBuffer(buildKernel, "cellCounts", cellCountsBuffer);
        computeShader.SetBuffer(buildKernel, "cellParticles", cellParticlesBuffer);

        computeShader.SetBuffer(densityKernel, "pos", posBuffer);
        computeShader.SetBuffer(densityKernel, "vel", velBuffer);
        computeShader.SetBuffer(densityKernel, "dens", densBuffer);
        computeShader.SetBuffer(densityKernel, "pres", presBuffer);
        computeShader.SetBuffer(densityKernel, "colors", colorsBuffer);
        computeShader.SetBuffer(densityKernel, "drained", drainedBuffer);
        computeShader.SetBuffer(densityKernel, "cellCounts", cellCountsBuffer);
        computeShader.SetBuffer(densityKernel, "cellParticles", cellParticlesBuffer);

        computeShader.SetBuffer(forcesKernel, "pos", posBuffer);
        computeShader.SetBuffer(forcesKernel, "vel", velBuffer);
        computeShader.SetBuffer(forcesKernel, "dens", densBuffer);
        computeShader.SetBuffer(forcesKernel, "pres", presBuffer);
        computeShader.SetBuffer(forcesKernel, "colors", colorsBuffer);
        computeShader.SetBuffer(forcesKernel, "drained", drainedBuffer);
        computeShader.SetBuffer(forcesKernel, "cellCounts", cellCountsBuffer);
        computeShader.SetBuffer(forcesKernel, "cellParticles", cellParticlesBuffer);

        // Readback arrays
        gpuPosReadback = new Vector3[particleCount];
        gpuColReadback = new Vector3[particleCount];
        gpuSyncDrained = new int[particleCount];
        System.Array.Copy(pos, gpuPosReadback, particleCount);
        lastCpuRawPos = new Vector3[particleCount];
        System.Array.Copy(pos, lastCpuRawPos, particleCount);

        // Compute shader constants (set once)
        computeShader.SetInt("particleCount", particleCount);
        computeShader.SetFloat("particleRadius", particleRadius);
        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasStiffness", gasStiffness);
        computeShader.SetFloat("viscosity", viscosity);
        computeShader.SetFloat("surfaceTension", surfaceTension);
        computeShader.SetFloat("speedCap", speedCap);
        computeShader.SetFloat("colorMixRate", colorMixRate);
        computeShader.SetFloat("baseTopY", baseTopY);
        computeShader.SetFloat("topY", topY);
        computeShader.SetFloat("innerHalfX", innerHalfX);
        computeShader.SetFloat("innerHalfZ", innerHalfZ);
        computeShader.SetFloat("drainHoleR", drainHoleR);

        particleThreadGroups = Mathf.CeilToInt(particleCount / 64f);
        gridThreadGroups = Mathf.CeilToInt(totalCells / 64f);

        PreRelaxGPU();
        ready = true;
    }

    void GenerateLattice()
    {
        float spacing = particleRadius * particleSpacing;
        float fillTop = Mathf.Lerp(baseTopY, topY, fillLevel);
        if (colorPalette.Length == 0) colorPalette = new Color[] { Color.red };

        List<Vector3> posList = new List<Vector3>();
        List<Color> colList = new List<Color>();

        float topGap = (fillLevel > 0.99f) ? spacing * 0.15f : spacing;
        for (float y = baseTopY + spacing; y <= fillTop - topGap; y += spacing)
        {
            float halfX = innerHalfX - particleRadius * 1.5f;
            float halfZ = innerHalfZ - particleRadius * 1.5f;
            if (halfX < spacing * 0.5f || halfZ < spacing * 0.5f) continue;

            int nx = Mathf.Max(1, Mathf.RoundToInt(halfX * 2f / spacing));
            int nz = Mathf.Max(1, Mathf.RoundToInt(halfZ * 2f / spacing));

            for (int ix = 0; ix < nx; ix++)
            {
                float px = (ix + 0.5f) * 2f * halfX / nx - halfX;
                for (int iz = 0; iz < nz; iz++)
                {
                    float pz = (iz + 0.5f) * 2f * halfZ / nz - halfZ;
                    float r = Mathf.Sqrt(px * px + pz * pz);
                    if (r < drainHoleR && y < baseTopY + spacing * 5f) continue;
                    posList.Add(new Vector3(px, y, pz));
                    colList.Add(colorPalette[Random.Range(0, colorPalette.Length)]);
                }
            }
        }

        if (posList.Count == 0)
        {
            particleCount = 0;
            pos = new Vector3[0];
            vel = new Vector3[0];
            dens = new float[0];
            pres = new float[0];
            colors = new Color[0];
            drained = new bool[0];
            particleMatrices = new Matrix4x4[0];
            particleColors = new Vector4[0];
            batchMatrices = new Matrix4x4[0];
            batchColors = new Vector4[0];
            return;
        }

        particleCount = posList.Count;
        pos = new Vector3[particleCount];
        vel = new Vector3[particleCount];
        dens = new float[particleCount];
        pres = new float[particleCount];
        colors = new Color[particleCount];
        drained = new bool[particleCount];
        particleMatrices = new Matrix4x4[particleCount];
        particleColors = new Vector4[particleCount];
        batchMatrices = new Matrix4x4[1023];
        batchColors = new Vector4[1023];

        for (int i = 0; i < particleCount; i++)
        {
            pos[i] = posList[i];
            colors[i] = colList[i];
        }
    }

    public void ApplyBucketScale()
    {
        float s = bucketScale;
        if (bucketT != null)
            bucketT.localScale = new Vector3(s, s, s);
        RealisticBucketVisualBuilder b = FindFirstObjectByType<RealisticBucketVisualBuilder>();
        if (b != null)
        {
            innerHalfX = 0.18f * s;
            innerHalfZ = 0.18f * s;
            float hh = b.HandleHeight, h = b.Height, wt = b.WallThickness;
            topY = (-hh - wt) * s;
            baseTopY = (-hh - h + wt) * s;
            drainHoleR = (b.DrainHoleRadius + 0.01f) * s;
        }
        minY = baseTopY + particleRadius * 1.5f;
        maxY = topY - 0.005f;
    }

    public void RegenerateParticles()
    {
        posReadbackPending = false;
        colReadbackPending = false;
        int oldCount = particleCount;
        GenerateLattice();
        if (particleCount == 0)
        {
            ready = false;
            foreach (var buf in new ComputeBuffer[] { posBuffer, velBuffer, densBuffer, presBuffer, colorsBuffer, drainedBuffer })
                if (buf != null) buf.Release();
            posBuffer = null; velBuffer = null;
            densBuffer = null; presBuffer = null; colorsBuffer = null; drainedBuffer = null;
            if (cellCountsBuffer != null) { cellCountsBuffer.Release(); cellCountsBuffer = null; }
            if (cellParticlesBuffer != null) { cellParticlesBuffer.Release(); cellParticlesBuffer = null; }
            gpuPosReadback = null;
            lastCpuRawPos = null;
            if (dripPanel != null) hasPrevPanelPos = false;
            return;
        }
        if (!ready) ready = true;
        if (!enabled) enabled = true;
        if (cellCountsBuffer == null)
        {
            if (computeShader == null)
            {
                // Attempt auto-find if Start() never got past the particleCount==0 check
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("SPHSimulation t:ComputeShader");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    computeShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                }
#endif
                if (computeShader == null)
                    computeShader = Resources.Load<ComputeShader>("SPHSimulation");
                if (computeShader == null)
                {
                    Debug.LogError("computeShader is null — drag SPHSimulation.compute to the Inspector field.");
                    return;
                }
                clearKernel = computeShader.FindKernel("ClearGrid");
                buildKernel = computeShader.FindKernel("BuildGrid");
                densityKernel = computeShader.FindKernel("ComputeDensity");
                forcesKernel = computeShader.FindKernel("ComputeForces");
                enabled = true;
            }
            SetupGrid();
            cellCountsBuffer = new ComputeBuffer(totalCells, 4);
            cellParticlesBuffer = new ComputeBuffer(totalCells * CELL_MAX, 4);
            foreach (int k in new[] { clearKernel, buildKernel, densityKernel, forcesKernel })
            {
                computeShader.SetBuffer(k, "cellCounts", cellCountsBuffer);
                computeShader.SetBuffer(k, "cellParticles", cellParticlesBuffer);
            }
        }
        if (particleCount != oldCount)
        {
            if (posBuffer != null) posBuffer.Release();
            if (velBuffer != null) velBuffer.Release();
            if (densBuffer != null) densBuffer.Release();
            if (presBuffer != null) presBuffer.Release();
            if (colorsBuffer != null) colorsBuffer.Release();
            if (drainedBuffer != null) drainedBuffer.Release();
            int[] oldCounts = new int[totalCells];
            int[] oldParts = new int[totalCells * CELL_MAX];
            posBuffer = new ComputeBuffer(particleCount, 12);
            velBuffer = new ComputeBuffer(particleCount, 12);
            densBuffer = new ComputeBuffer(particleCount, 4);
            presBuffer = new ComputeBuffer(particleCount, 4);
            colorsBuffer = new ComputeBuffer(particleCount, 12);
            drainedBuffer = new ComputeBuffer(particleCount, 4);
            cellCountsBuffer.SetData(oldCounts);
            cellParticlesBuffer.SetData(oldParts);
            foreach (int k in new[] { clearKernel, buildKernel, densityKernel, forcesKernel })
            {
                computeShader.SetBuffer(k, "pos", posBuffer);
                computeShader.SetBuffer(k, "vel", velBuffer);
                computeShader.SetBuffer(k, "dens", densBuffer);
                computeShader.SetBuffer(k, "pres", presBuffer);
                computeShader.SetBuffer(k, "colors", colorsBuffer);
                computeShader.SetBuffer(k, "drained", drainedBuffer);
                computeShader.SetBuffer(k, "cellCounts", cellCountsBuffer);
                computeShader.SetBuffer(k, "cellParticles", cellParticlesBuffer);
            }
            particleThreadGroups = Mathf.CeilToInt(particleCount / 64f);
            computeShader.SetInt("particleCount", particleCount);
            gpuPosReadback = new Vector3[particleCount];
            gpuColReadback = new Vector3[particleCount];
            gpuSyncDrained = new int[particleCount];
            particleMatrices = new Matrix4x4[particleCount];
            particleColors = new Vector4[particleCount];
            lastCpuRawPos = new Vector3[particleCount];
        }
        Vector3[] initColors = new Vector3[particleCount];
        for (int i = 0; i < particleCount; i++)
            initColors[i] = new Vector3(colors[i].r, colors[i].g, colors[i].b);
        int[] initDrained = new int[particleCount];
        posBuffer.SetData(pos);
        velBuffer.SetData(vel);
        colorsBuffer.SetData(initColors);
        drainedBuffer.SetData(initDrained);
        if (gpuPosReadback == null) gpuPosReadback = new Vector3[particleCount];
        System.Array.Copy(pos, gpuPosReadback, particleCount);
        for (int i = 0; i < particleCount; i++)
            lastCpuRawPos[i] = pos[i];
    }

    void SetupGrid()
    {
        float cs = particleRadius * 2.8f;
        Vector3 gridMin = new Vector3(-innerHalfX - 0.05f, baseTopY - 1f, -innerHalfZ - 0.05f) - Vector3.one * cs;
        Vector3 gridMax = new Vector3(innerHalfX + 0.05f, topY + 0.05f, innerHalfZ + 0.05f) + Vector3.one * cs;
        Vector3 gridSize = gridMax - gridMin;
        int gx = Mathf.CeilToInt(gridSize.x / cs);
        int gy = Mathf.CeilToInt(gridSize.y / cs);
        int gz = Mathf.CeilToInt(gridSize.z / cs);
        totalCells = gx * gy * gz;
        gridThreadGroups = Mathf.CeilToInt(totalCells / 64f);

        computeShader.SetInts("gridRes", gx, gy, gz);
        computeShader.SetInt("gridResXY", gx * gy);
        computeShader.SetVector("gridMin", gridMin);
        computeShader.SetFloat("cellSize", cs);
    }

    void PreRelaxGPU()
    {
        float g = gravityAccel;
        gravityAccel = 0f;
        float savedDrainRate = drainRate;
        drainRate = 0f;
        Vector3 grav = Vector3.zero;
        float dt = 0.003f;
        float h = particleRadius * 2.8f;

        SetShaderConstants(h, grav, dt);

        for (int s = 0; s < relaxSteps; s++)
        {
            float damp = 1f - 0.6f * s / relaxSteps;
            computeShader.SetFloat("speedCap", damp * 10f);

            computeShader.Dispatch(clearKernel, gridThreadGroups, 1, 1);
            computeShader.Dispatch(buildKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(forcesKernel, particleThreadGroups, 1, 1);
        }

        posBuffer.GetData(gpuPosReadback);
        for (int i = 0; i < particleCount; i++)
        {
            pos[i] = gpuPosReadback[i];
            vel[i] = Vector3.zero;
        }
        velBuffer.SetData(vel);

        computeShader.SetFloat("speedCap", speedCap);
        drainRate = savedDrainRate;
        gravityAccel = g;
    }

    void SetShaderConstants(float h, Vector3 grav, float dt)
    {
        float h2 = h * h;
        float h9 = h2 * h2 * h2 * h2 * h;
        float h6 = h * h * h * h * h * h;

        float poly6Const = 315f / (64f * Mathf.PI * h9);
        float spikyConst = -45f / (Mathf.PI * h6);
        float viscConst = 45f / (Mathf.PI * h6);
        float wZero = poly6Const * h2 * h2 * h2;

        computeShader.SetFloat("h", h);
        computeShader.SetFloat("h2", h2);
        computeShader.SetFloat("dt", dt);
        computeShader.SetFloat("poly6Const", poly6Const);
        computeShader.SetFloat("spikyConst", spikyConst);
        computeShader.SetFloat("viscConst", viscConst);
        computeShader.SetFloat("wZero", wZero);
        computeShader.SetVector("gravityVec", grav);
        computeShader.SetFloat("viscosity", viscosity);
        float dampDt = Mathf.Max(Time.deltaTime, 0.008f);
        float effectiveDamping = wasMoving ? velocityDamping : 0.001f;
        computeShader.SetFloat("velocityDamping", Mathf.Pow(effectiveDamping, dampDt * 60f));
        computeShader.SetFloat("surfaceTension", surfaceTension);
        computeShader.SetFloat("speedCap", speedCap);
        computeShader.SetFloat("colorMixRate", colorMixRate);
        computeShader.SetFloat("drainRate", drainRate);
        computeShader.SetFloat("swirl", swirl);
        computeShader.SetInt("spillMode", 0);
    }

    void ApplyMaterialProperties()
    {
        if (mat == null) return;
        mat.SetFloat("_Opacity", 0.75f);
        mat.SetFloat("_Softness", 0.30f);
        mat.SetFloat("_FresnelPower", 2.0f);
        mat.SetFloat("_GlossIntensity", 1.6f);
        mat.SetFloat("_Smoothness", glossiness);
        mat.SetColor("_SpecGloss", new Color(0.95f, 0.98f, 1f));
        mat.enableInstancing = true;
    }

    private float lissajousTime;
    private Vector3 prevPanelPos;
    private bool hasPrevPanelPos;

    void LateUpdate()
    {
        if (!ready) return;
        if (computeShader == null)
        {
            Debug.LogError("computeShader is null – drag SPHSimulation.compute to the Inspector field and restart.");
            enabled = false;
            return;
        }

        ApplyMaterialProperties();

        float dt = Mathf.Min(Time.deltaTime, 0.025f) / substeps;
        float h = particleRadius * 2.8f;

        // Effective gravity in world space (real gravity + inertial force from bucket acceleration)
        Vector3 bucketWorldVel = (bucketT.position - prevBucketPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 bucketWorldAccel = (bucketWorldVel - prevBucketWorldVel) / Mathf.Max(Time.deltaTime, 0.0001f);
        prevBucketPos = bucketT.position;
        prevBucketWorldVel = bucketWorldVel;

        Vector3 worldGravity = new Vector3(0f, gravityAccel, 0f);
        Vector3 grav = bucketT.InverseTransformDirection(worldGravity - bucketWorldAccel);

        SetShaderConstants(h, grav, dt);

        for (int s = 0; s < substeps; s++)
        {
            computeShader.Dispatch(clearKernel, gridThreadGroups, 1, 1);
            computeShader.Dispatch(buildKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(forcesKernel, particleThreadGroups, 1, 1);
        }

        // Async readback — never stall CPU waiting for GPU
        if (posReadbackPending && posReadbackRequest.done)
        {
            if (!posReadbackRequest.hasError)
                posReadbackRequest.GetData<Vector3>().CopyTo(gpuPosReadback);
            posReadbackPending = false;
        }
        if (colReadbackPending && colReadbackRequest.done)
        {
            if (colorMixRate > 0.001f && !colReadbackRequest.hasError)
                colReadbackRequest.GetData<Vector3>().CopyTo(gpuColReadback);
            colReadbackPending = false;
        }

        if (drainActive) HandleDrain(dt);

        int undrainedCount = 0;
        int activeCount = 0;
        float pScale = particleRadius * 2.6f * particleSizeScale;
        Vector3 scl = Vector3.one * pScale;
        if (gpuPosReadback == null || gpuPosReadback.Length < particleCount)
        {
            gpuPosReadback = new Vector3[particleCount];
            lastCpuRawPos = new Vector3[particleCount];
            System.Array.Copy(pos, gpuPosReadback, particleCount);
        }
        if (gpuColReadback == null || gpuColReadback.Length < particleCount)
            gpuColReadback = new Vector3[particleCount];
        if (gpuSyncDrained == null || gpuSyncDrained.Length < particleCount)
            gpuSyncDrained = new int[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            if (!drained[i])
            {
                Vector3 rp = gpuPosReadback[i];
                lastCpuRawPos[i] = Vector3.Lerp(lastCpuRawPos[i], rp, 0.02f);
                gpuPosReadback[i] = lastCpuRawPos[i];
            }
            pos[i] = gpuPosReadback[i];
            if (colorMixRate > 0.001f)
                colors[i] = new Color(gpuColReadback[i].x, gpuColReadback[i].y, gpuColReadback[i].z, 1f);
            gpuSyncDrained[i] = drained[i] ? 1 : 0;
            if (drained[i]) continue;
            undrainedCount++;
            particleMatrices[activeCount] = Matrix4x4.TRS(bucketT.TransformPoint(pos[i]), bucketT.rotation, scl);
            particleColors[activeCount] = colors[i];
            activeCount++;
        }
        drainedBuffer.SetData(gpuSyncDrained);

        // Request readback of this frame's GPU data for next frame
        if (!posReadbackPending)
        {
            posReadbackRequest = AsyncGPUReadback.Request(posBuffer);
            posReadbackPending = true;
        }
        if (!colReadbackPending && colorMixRate > 0.001f)
        {
            colReadbackRequest = AsyncGPUReadback.Request(colorsBuffer);
            colReadbackPending = true;
        }

        // Paint trail where the liquid stream hits the panel
        bool shouldDrain = drainActive && drainRate > 0.001f;

        float streamSpeed = 0.5f;
        if (bucketT != null)
        {
            float rawSpeed = (bucketT.position - lastBucketPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastBucketPos = bucketT.position;
            smoothSpeed = Mathf.Lerp(smoothSpeed, rawSpeed, 0.02f);
            streamSpeed = Mathf.Clamp01(smoothSpeed * 2f);
        }

        if (dripPanel != null && shouldDrain)
        {
            Vector3 drainWorld = bucketT.TransformPoint(new Vector3(0, baseTopY, 0));
            float panelY = dripPanel.transform.position.y;
            Vector3 streamEnd = new Vector3(drainWorld.x, panelY + 0.001f, drainWorld.z);

            Color paintColor = Color.white;
            if (undrainedCount > 0)
            {
                int pick = Random.Range(0, undrainedCount);
                int idx = 0;
                for (int i = 0; i < particleCount; i++)
                {
                    if (drained[i]) continue;
                    if (idx == pick) { paintColor = colors[i]; break; }
                    idx++;
                }
            }

            if (hasPrevPanelPos)
                dripPanel.PaintLine(prevPanelPos, streamEnd, paintColor, 0.1f);
            else
                dripPanel.PaintDot(streamEnd, paintColor, 0.1f);
            prevPanelPos = streamEnd;
            hasPrevPanelPos = true;

            splatDotTimer += dt;
            while (splatDotTimer >= 0.04f)
            {
                splatDotTimer -= 0.04f;
                if (dripPanel.materialType != DripPanel.PanelMaterial.Cloth)
                {
                    int splatCount = dripPanel.materialType == DripPanel.PanelMaterial.Wood ? 4 : 20;
                    float radiusMul = dripPanel.materialType == DripPanel.PanelMaterial.Wood ? 0.05f : 0.1f;
                    for (int i = 0; i < splatCount; i++)
                    {
                        float a = Random.Range(0f, 6.2832f);
                        float d = Random.Range(0.01f, 0.07f);
                        Vector3 off = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                        dripPanel.PaintDot(streamEnd + off, paintColor, radiusMul);
                    }
                }
            }

            if (streamLine != null)
            {
                streamLine.enabled = true;
                Vector3 streamStart = drainWorld + Vector3.up * 0.008f;
                streamLine.SetPosition(0, streamStart);
                streamLine.SetPosition(1, streamEnd);
                streamLine.startColor = paintColor;
                streamLine.endColor = new Color(paintColor.r, paintColor.g, paintColor.b, 0f);
                streamLine.material.color = paintColor;
                streamLine.startWidth = particleRadius * 0.35f * streamSpeed + 0.0005f;
                streamLine.endWidth = particleRadius * 0.04f * streamSpeed + 0.0002f;
            }
        }
        else
        {
            hasPrevPanelPos = false;
            if (streamLine != null)
                streamLine.enabled = false;
        }

        // Render active particles — self-heal if Start() didn't get this far
        if (instanceProps == null) { instanceProps = new MaterialPropertyBlock(); if (mat != null) mat.enableInstancing = true; }
        if (batchColors == null || batchColors.Length == 0) batchColors = new Vector4[1023];
        if (batchMatrices == null || batchMatrices.Length == 0) batchMatrices = new Matrix4x4[1023];
        if (particleMatrices == null || particleMatrices.Length < particleCount)
            particleMatrices = new Matrix4x4[Mathf.Max(particleCount, 1)];
        if (particleColors == null || particleColors.Length < particleCount)
            particleColors = new Vector4[Mathf.Max(particleCount, 1)];
        if (sphereMesh == null) sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (sphereMesh == null) { Debug.LogError("No sphere mesh"); return; }
        int drawn = 0;
        while (drawn < activeCount)
        {
            int count = Mathf.Min(1023, activeCount - drawn);
            System.Array.Copy(particleMatrices, drawn, batchMatrices, 0, count);
            System.Array.Copy(particleColors, drawn, batchColors, 0, count);
            instanceProps.SetVectorArray("_Color", batchColors);
            Graphics.DrawMeshInstanced(sphereMesh, 0, mat, batchMatrices, count, instanceProps);
            drawn += count;
        }

        ApplyPanelConfigBase();
    }

    void ApplyPanelConfigBase()
    {
        if (dripPanel == null) return;
        dripPanel.materialType = panelMaterial;
        dripPanel.woodBaseColor = woodBaseColor;
        dripPanel.woodGrainColor = woodGrainColor;
        dripPanel.grainScale = woodGrainScale;
        dripPanel.absorptionRate = woodAbsorptionRate;
        dripPanel.wetSheen = woodWetSheen;
        dripPanel.clothBaseColor = clothBaseColor;
        dripPanel.clothThreadColor = clothThreadColor;
        dripPanel.weaveScale = clothWeaveScale;
        dripPanel.wickingRate = clothWickingRate;
    }

    void HandleDrain(float dt)
    {
        if (drainRate <= 0.001f) { drainTimer = 0f; return; }

        int undrainedCount = 0;
        for (int i = 0; i < particleCount; i++)
            if (!drained[i]) undrainedCount++;
        if (undrainedCount <= 80) { drainTimer = 0f; return; }

        bool anyOverDrain = false;
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            if (pos[i].y > baseTopY + 0.15f) continue;
            float r = Mathf.Sqrt(pos[i].x * pos[i].x + pos[i].z * pos[i].z);
            if (r < drainHoleR * 1.2f) { anyOverDrain = true; break; }
        }
        if (!anyOverDrain) { drainTimer = 0f; return; }

        drainTimer += dt * drainRate * drainRate * 15f;

        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            float r = Mathf.Sqrt(pos[i].x * pos[i].x + pos[i].z * pos[i].z);
            if (r < drainHoleR && pos[i].y < baseTopY + 0.04f && drainTimer >= 1f)
            {
                drained[i] = true;
                drainTimer = 0f;
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebug || bucketT == null) return;
        Gizmos.color = Color.cyan;
        float hh = (topY - baseTopY) * 0.5f;
        Vector3 center = bucketT.position + bucketT.up * (topY + baseTopY) * 0.5f;
        Gizmos.DrawWireCube(center, new Vector3(innerHalfX * 2f, topY - baseTopY, innerHalfZ * 2f));
    }

    void OnDestroy()
    {
        while (trailPool.Count > 0)
            Destroy(trailPool.Dequeue());
        if (ownsPanel && dripPanel != null)
            Destroy(dripPanel.gameObject);

        if (posBuffer != null) posBuffer.Release();
        if (velBuffer != null) velBuffer.Release();
        if (densBuffer != null) densBuffer.Release();
        if (presBuffer != null) presBuffer.Release();
        if (colorsBuffer != null) colorsBuffer.Release();
        if (drainedBuffer != null) drainedBuffer.Release();
        if (cellCountsBuffer != null) cellCountsBuffer.Release();
        if (cellParticlesBuffer != null) cellParticlesBuffer.Release();
    }

    void ReturnTrail(TrailRenderer tr)
    {
        if (tr == null) return;
        GameObject go = tr.gameObject;
        if (activeTrails.Remove(go) && trailPool.Count < 25)
        {
            go.SetActive(false);
            trailPool.Enqueue(go);
        }
        else
        {
            Destroy(go);
        }
    }

    static Mesh BuildSphereMesh()
    {
        Mesh m = new Mesh();
        float rad = 1f;
        int rings = 8, sectors = 8;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int ri = 0; ri <= rings; ri++)
        {
            float phi = Mathf.PI * ri / rings;
            for (int si = 0; si <= sectors; si++)
            {
                float th = 2f * Mathf.PI * si / sectors;
                verts.Add(new Vector3(
                    rad * Mathf.Sin(phi) * Mathf.Cos(th),
                    rad * Mathf.Cos(phi),
                    rad * Mathf.Sin(phi) * Mathf.Sin(th)));
            }
        }
        for (int ri = 0; ri < rings; ri++)
            for (int si = 0; si < sectors; si++)
            {
                int cur = ri * (sectors + 1) + si;
                int nxt = cur + sectors + 1;
                tris.Add(cur); tris.Add(nxt); tris.Add(cur + 1);
                tris.Add(cur + 1); tris.Add(nxt); tris.Add(nxt + 1);
            }
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle box = new GUIStyle(GUI.skin.box);
        box.fontSize = 14;
        box.normal.textColor = Color.white;
        box.alignment = TextAnchor.UpperLeft;
        box.padding = new RectOffset(6, 6, 4, 4);
        GUI.Box(new Rect(w - 165, 6, 158, 48),
            "Particles: " + particleCount + "\nFPS: " + (1f / Time.smoothDeltaTime).ToString("F0"), box);

        // Material selector
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 13;
        btnStyle.padding = new RectOffset(4, 4, 2, 2);
        float bx = w - 165, by = 62;
        float bw = 158, bh = 26;

        if (GUI.Button(new Rect(bx, by, bw, bh), "Wood", btnStyle))
            panelMaterial = DripPanel.PanelMaterial.Wood;
        if (GUI.Button(new Rect(bx, by + bh + 2, bw, bh), "Cloth", btnStyle))
            panelMaterial = DripPanel.PanelMaterial.Cloth;
        if (GUI.Button(new Rect(bx, by + (bh + 2) * 2, bw, bh), "Ceramic", btnStyle))
            panelMaterial = DripPanel.PanelMaterial.Ceramic;

        // Highlight current selection
        int selIdx = (int)panelMaterial;
        float hx = bx, hy = by + (bh + 2) * selIdx;
        Color orig = GUI.color;
        GUI.color = new Color(0, 0.6f, 1, 0.25f);
        GUI.Box(new Rect(hx, hy, bw, bh), "");
        GUI.color = orig;
    }
}
