using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class SPHPaintSimulation : MonoBehaviour
{
    [Header("Particle Settings")]
    public float particleRadius = 0.006f;
    public float particleMass = 0.06f;
    public int substeps = 2;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 25f;
    public float viscosity = 0.15f;
    public float surfaceTension = 1.5f;
    public float gravityAccel = -9.81f;
    public int relaxSteps = 20;

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

    [Header("Drain")]
    public bool drainActive = true;
    [Range(0f, 3f)]
    public float drainRate = 1.5f;
    [Range(0.1f, 3f)]
    public float trailDuration = 1f;
    public float destroyHeight = -3f;
    public DripPanel dripPanel;

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
    private Vector3[] gpuPosReadback, gpuColReadback;
    private int[] gpuDrainedReadback, gpuSyncDrained;
    private const int CELL_MAX = 256;

    private AdvancedBucketRopeSimulation ropeSimCached;
    private Mesh sphereMesh;
    private MaterialPropertyBlock mpb, panelPB;
    private Matrix4x4[] particleMatrices;
    private Vector4[] particleColors;
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

    private class FallingDrop
    {
        public GameObject go;
        public Vector3 worldPos;
        public Vector3 worldVel;
        public TrailRenderer trail;
        public float life;
        public Color color;
    }
    private List<FallingDrop> drops = new List<FallingDrop>();

    void Start()
    {
        RealisticBucketVisualBuilder b = GetComponentInParent<RealisticBucketVisualBuilder>();
        if (b == null) { enabled = false; return; }

        bucketT = b.transform;
        innerHalfX = 0.18f;
        innerHalfZ = 0.18f;
        float hh = b.HandleHeight, h = b.Height, wt = b.WallThickness;
        topY = -hh - wt;
        baseTopY = -hh - h + wt;
        drainHoleR = b.DrainHoleRadius + 0.01f;

        if (!Application.isPlaying) return;

        lastBucketPos = bucketT.position;
        minY = baseTopY + particleRadius * 1.5f;
        maxY = topY - 0.005f;

        GenerateLattice();
        if (particleCount == 0) { enabled = false; return; }

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

        ropeSimCached = FindFirstObjectByType<AdvancedBucketRopeSimulation>();
        if (ropeSimCached != null)
        {
            ropeSimCached.SetInitialThetaDegrees(45f);
            ropeSimCached.SetInitialPhiVelocity(2f);
            ropeSimCached.SetRopeLength(2.2f);
            ropeSimCached.SetRopeGravityMultiplier(0f);
            ropeSimCached.SetConstraintIterations(10);
            ropeSimCached.SetDampingPerSecond(0.01f);
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
            mat = particleMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Custom/FluidParticle");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            if (shader.name == "Standard")
            {
                mat.SetFloat("_Mode", 2.0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
            mat.SetFloat("_Smoothness", glossiness);
            mat.SetColor("_SpecGloss", new Color(0.95f, 0.98f, 1f));
            if (shader.name == "Custom/FluidParticle")
            {
                mat.SetFloat("_Opacity", 0.55f);
                mat.SetFloat("_FresnelPower", 2.5f);
            }
        }
        mat.enableInstancing = true;

        Shader trailShader = Shader.Find("Unlit/Transparent");
        if (trailShader == null) trailShader = Shader.Find("Universal Render Pipeline/Unlit");
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

        // Setup grid dimensions
        float cs = particleRadius * 4f;
        Vector3 gridMin = new Vector3(-innerHalfX - 0.05f, baseTopY - 1f, -innerHalfZ - 0.05f) - Vector3.one * cs;
        Vector3 gridMax = new Vector3(innerHalfX + 0.05f, topY + 0.05f, innerHalfZ + 0.05f) + Vector3.one * cs;
        Vector3 gridSize = gridMax - gridMin;
        int gx = Mathf.CeilToInt(gridSize.x / cs);
        int gy = Mathf.CeilToInt(gridSize.y / cs);
        int gz = Mathf.CeilToInt(gridSize.z / cs);
        totalCells = gx * gy * gz;

        computeShader.SetInts("gridRes", gx, gy, gz);
        computeShader.SetVector("gridMin", gridMin);
        computeShader.SetFloat("cellSize", cs);

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
        gpuDrainedReadback = new int[particleCount];
        gpuSyncDrained = new int[particleCount];

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
        float spacing = particleRadius * 1.0f;
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

        if (posList.Count == 0) { posList.Add(Vector3.zero); colList.Add(colorPalette[0]); }

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

    void PreRelaxGPU()
    {
        float g = gravityAccel;
        gravityAccel = 0f;
        float savedDrainRate = drainRate;
        drainRate = 0f;
        Vector3 grav = Vector3.zero;
        float dt = 0.003f;
        float h = particleRadius * 4f;

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
        computeShader.SetFloat("drainRate", drainRate);
    }

    private float lissajousTime;
    private Vector3 prevPanelPos;
    private bool hasPrevPanelPos;

    void LateUpdate()
    {
        if (!ready) return;

        float dt = Mathf.Min(Time.deltaTime, 0.025f) / substeps;
        float h = particleRadius * 4f;
        Vector3 grav = bucketT.InverseTransformDirection(new Vector3(0f, gravityAccel, 0f));

        SetShaderConstants(h, grav, dt);

        for (int s = 0; s < substeps; s++)
        {
            computeShader.Dispatch(clearKernel, gridThreadGroups, 1, 1);
            computeShader.Dispatch(buildKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(forcesKernel, particleThreadGroups, 1, 1);
        }

        // Read back from GPU
        posBuffer.GetData(gpuPosReadback);

        colorsBuffer.GetData(gpuColReadback);
        drainedBuffer.GetData(gpuDrainedReadback);

        for (int i = 0; i < particleCount; i++)
        {
            pos[i] = gpuPosReadback[i];
            colors[i] = new Color(gpuColReadback[i].x, gpuColReadback[i].y, gpuColReadback[i].z, 1f);
            drained[i] = gpuDrainedReadback[i] != 0;
        }

        if (drainActive) HandleDrain(dt);

        // Sync drained back to GPU (CPU may have changed it)
        for (int i = 0; i < particleCount; i++)
            gpuSyncDrained[i] = drained[i] ? 1 : 0;
        drainedBuffer.SetData(gpuSyncDrained);

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
            int undrainedCount = 0;
            for (int i = 0; i < particleCount; i++)
                if (!drained[i]) undrainedCount++;
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

        // Render active particles
        int activeCount = 0;
        float pScale = particleRadius * 0.65f;
        Vector3 scl = Vector3.one * pScale;
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            particleMatrices[activeCount] = Matrix4x4.TRS(bucketT.TransformPoint(pos[i]), bucketT.rotation, scl);
            particleColors[activeCount] = colors[i];
            activeCount++;
        }
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
        foreach (var d in drops)
            CleanupDrop(d);
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

    void SpawnSplash(FallingDrop drop)
    {
        float speed = drop.worldVel.magnitude;
        float spread = drainHoleR * (0.3f + speed * 0.1f);
        Vector3 velDir = drop.worldVel.normalized;
        int count = Mathf.RoundToInt(4 + speed * 2f);
        for (int k = 0; k < count; k++)
        {
            float angle = Random.Range(-1.2f, 1.2f);
            float dist = Random.Range(0.2f, 1f) * spread;
            Vector3 dir = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0) * velDir;
            Vector3 off = new Vector3(dir.x * dist, 0f, dir.z * dist) * 0.5f;
            dripPanel.DrawSplat(drop.worldPos + off, drop.color, drop.worldVel * 0.3f);
        }
    }

    void CleanupDrop(FallingDrop drop)
    {
        ReturnTrail(drop.trail);
        if (drop.go != null) Destroy(drop.go);
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
}
