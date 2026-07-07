using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class TankFluidSimulation : MonoBehaviour
{
    [Header("Tank Dimensions (world units)")]
    public float tankWidth = 0.3f;
    public float tankDepth = 0.3f;
    public float tankHeight = 0.45f;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Particle Settings")]
    public float particleRadius = 0.007f;
    public float particleMass = 0.06f;
    public int substeps = 1;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 25f;
    public float viscosity = 0.08f;
    public float surfaceTension = 0.08f;
    public float gravityAccel = -9.81f;
    [Range(0.9f, 1f)]
    public float velocityDamping = 0.95f;

    [Header("GPU Compute")]
    public ComputeShader computeShader;

    [Header("Fill")]
    [Range(0f, 1f)]
    public float fillLevel = 0.998f;

    [Header("Colors")]
    public Color liquidColor = new Color(0.2f, 0.6f, 1f, 0.7f);
    public float glossiness = 0.85f;

    [Header("Rendering")]
    public Material particleMaterial;

    private Material mat;
    private Mesh sphereMesh;
    private int particleCount;
    private Vector3[] pos, vel;
    private float[] dens, pres;
    private Color[] colors;
    private Matrix4x4[] particleMatrices;
    private Vector4[] particleColors, batchColors;
    private Matrix4x4[] batchMatrices;

    private ComputeBuffer posBuffer, velBuffer, densBuffer, presBuffer, colorsBuffer, drainedBuffer;
    private ComputeBuffer cellCountsBuffer, cellParticlesBuffer;
    private int clearKernel, buildKernel, densityKernel, forcesKernel;
    private int totalCells, gridThreadGroups, particleThreadGroups;
    private Vector3[] gpuPosReadback;
    private AsyncGPUReadbackRequest posReadbackRequest;
    private bool posReadbackPending;

    private const int CELL_MAX = 256;
    private float innerHalfX, innerHalfZ, baseTopY, topY;
    private Vector3 prevPos;
    private Vector3 prevWorldVel;
    private MaterialPropertyBlock instanceProps;
    private Vector3 origPosition;
    private Transform tankVisual;
    private int[] drainNone;
    private bool wasMoving;
    private float squeeze, prevSqueeze;

    void Awake()
    {
        instanceProps = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        origPosition = transform.position;
        tankVisual = transform.Find("TankVisual");
        ComputeBounds();
        GenerateLattice();
        if (particleCount == 0) { enabled = false; return; }
        SetupSimulation();
    }

    void ComputeBounds()
    {
        innerHalfX = tankWidth * 0.5f;
        innerHalfZ = tankDepth * 0.5f;
        baseTopY = -tankHeight * 0.5f;
        topY = tankHeight * 0.5f;
    }

    void GenerateLattice()
    {
        ComputeBounds();
        float spacing = particleRadius * 1.0f;
        float fillBottom = baseTopY + spacing;
        float fillTop = Mathf.Lerp(baseTopY, topY, fillLevel) - spacing;

        List<Vector3> posList = new List<Vector3>();
        List<Color> colList = new List<Color>();

        float halfX = innerHalfX - particleRadius * 1.5f;
        float halfZ = innerHalfZ - particleRadius * 1.5f;

        int nx = Mathf.Max(1, Mathf.RoundToInt(halfX * 2f / spacing));
        int nz = Mathf.Max(1, Mathf.RoundToInt(halfZ * 2f / spacing));

        for (float y = fillBottom; y <= fillTop; y += spacing)
        {
            for (int ix = 0; ix < nx; ix++)
            {
                float px = (ix + 0.5f) * 2f * halfX / nx - halfX;
                for (int iz = 0; iz < nz; iz++)
                {
                    float pz = (iz + 0.5f) * 2f * halfZ / nz - halfZ;
                    posList.Add(new Vector3(px, y, pz));
                    colList.Add(liquidColor);
                }
            }
        }

        if (posList.Count == 0) { posList.Add(Vector3.zero); colList.Add(liquidColor); }

        particleCount = posList.Count;
        pos = new Vector3[particleCount];
        vel = new Vector3[particleCount];
        dens = new float[particleCount];
        pres = new float[particleCount];
        colors = new Color[particleCount];
        particleMatrices = new Matrix4x4[particleCount];
        particleColors = new Vector4[particleCount];
        batchMatrices = new Matrix4x4[1023];
        batchColors = new Vector4[1023];

        prevPos = transform.position;
        prevWorldVel = Vector3.zero;

        for (int i = 0; i < particleCount; i++)
        {
            pos[i] = posList[i];
            colors[i] = colList[i];
        }
    }

    void SetupSimulation()
    {
        Shader shader = null;
        if (particleMaterial != null)
        {
            shader = particleMaterial.shader;
            mat = new Material(particleMaterial);
        }
        else
        {
            shader = Shader.Find("Custom/FluidParticle");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
        }
        if (shader != null && shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 2.0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_ALPHABLEND_ON");
        }
        ApplyMaterialProperties();

        sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (sphereMesh == null) sphereMesh = BuildSphereMesh();

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
            Debug.LogError("TankFluidSimulation: No compute shader found.");
            enabled = false;
            return;
        }

        clearKernel = computeShader.FindKernel("ClearGrid");
        buildKernel = computeShader.FindKernel("BuildGrid");
        densityKernel = computeShader.FindKernel("ComputeDensity");
        forcesKernel = computeShader.FindKernel("ComputeForces");

        float cs = particleRadius * 2.8f;
        Vector3 gridMin = new Vector3(-innerHalfX - 0.05f, baseTopY - 1f, -innerHalfZ - 0.05f) - Vector3.one * cs;
        Vector3 gridMax = new Vector3(innerHalfX + 0.05f, topY + 0.05f, innerHalfZ + 0.05f) + Vector3.one * cs;
        Vector3 gridSize = gridMax - gridMin;
        int gx = Mathf.CeilToInt(gridSize.x / cs);
        int gy = Mathf.CeilToInt(gridSize.y / cs);
        int gz = Mathf.CeilToInt(gridSize.z / cs);
        totalCells = gx * gy * gz;

        computeShader.SetInts("gridRes", gx, gy, gz);
        computeShader.SetInt("gridResXY", gx * gy);
        computeShader.SetVector("gridMin", gridMin);
        computeShader.SetFloat("cellSize", cs);

        posBuffer = new ComputeBuffer(particleCount, 12);
        velBuffer = new ComputeBuffer(particleCount, 12);
        densBuffer = new ComputeBuffer(particleCount, 4);
        presBuffer = new ComputeBuffer(particleCount, 4);
        colorsBuffer = new ComputeBuffer(particleCount, 12);
        drainedBuffer = new ComputeBuffer(particleCount, 4);
        cellCountsBuffer = new ComputeBuffer(totalCells, 4);
        cellParticlesBuffer = new ComputeBuffer(totalCells * CELL_MAX, 4);

        Vector3[] initColors = new Vector3[particleCount];
        for (int i = 0; i < particleCount; i++)
            initColors[i] = new Vector3(colors[i].r, colors[i].g, colors[i].b);
        drainNone = new int[particleCount];

        posBuffer.SetData(pos);
        velBuffer.SetData(vel);
        colorsBuffer.SetData(initColors);
        drainedBuffer.SetData(drainNone);

        int[] emptyCounts = new int[totalCells];
        int[] emptyParticles = new int[totalCells * CELL_MAX];
        cellCountsBuffer.SetData(emptyCounts);
        cellParticlesBuffer.SetData(emptyParticles);

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
        gpuPosReadback = new Vector3[particleCount];
        System.Array.Copy(pos, gpuPosReadback, particleCount);

        computeShader.SetInt("particleCount", particleCount);
        computeShader.SetFloat("particleRadius", particleRadius);
        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasStiffness", gasStiffness);
        computeShader.SetFloat("viscosity", viscosity);
        computeShader.SetFloat("surfaceTension", surfaceTension);
        computeShader.SetFloat("speedCap", speedCap);
        computeShader.SetFloat("colorMixRate", 0f);
        computeShader.SetFloat("baseTopY", baseTopY);
        computeShader.SetFloat("topY", topY);
        computeShader.SetFloat("innerHalfX", innerHalfX);
        computeShader.SetFloat("innerHalfZ", innerHalfZ);
        computeShader.SetFloat("drainHoleR", 0f);

        particleThreadGroups = Mathf.CeilToInt(particleCount / 64f);
        gridThreadGroups = Mathf.CeilToInt(totalCells / 64f);

        PreRelaxGPU();
        ready = true;
    }

    bool ready;
    void PreRelaxGPU()
    {
        if (!Application.isPlaying || computeShader == null) return;
        float h = particleRadius * 2.8f;
        Vector3 g = transform.InverseTransformDirection(new Vector3(0f, gravityAccel, 0f));
        SetShaderConstants(h, g, 0.008f);
        for (int i = 0; i < 20; i++)
        {
            computeShader.Dispatch(clearKernel, gridThreadGroups, 1, 1);
            computeShader.Dispatch(buildKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(forcesKernel, particleThreadGroups, 1, 1);
        }
        posBuffer.GetData(pos);
        System.Array.Copy(pos, gpuPosReadback, particleCount);
    }

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
        squeeze = Mathf.MoveTowards(squeeze, kb.spaceKey.isPressed ? 1f : 0f, Time.deltaTime * 2f);
        wasMoving = h != 0 || v != 0 || squeeze > 0.01f;
        Vector3 move = new Vector3(h, 0f, v) * moveSpeed * Time.deltaTime;
        transform.position += transform.TransformDirection(move);

        innerHalfX = tankWidth * 0.5f;
        innerHalfZ = tankDepth * 0.5f;

        float newBase = -tankHeight * 0.5f + squeeze * tankHeight * 0.075f;
        float newTop = tankHeight * 0.5f + squeeze * tankHeight * 0.15f;
        if (pos != null && posBuffer != null && Mathf.Abs(squeeze - prevSqueeze) > 0.0001f)
        {
            float oldBase = -tankHeight * 0.5f + prevSqueeze * tankHeight * 0.075f;
            float oldTop = tankHeight * 0.5f + prevSqueeze * tankHeight * 0.15f;
            float oldH = oldTop - oldBase;
            if (oldH > 0.0001f)
            {
                for (int i = 0; i < particleCount; i++)
                {
                    float t = (pos[i].y - oldBase) / oldH;
                    pos[i].y = newBase + t * (newTop - newBase);
                }
                posBuffer.SetData(pos);
                System.Array.Copy(pos, gpuPosReadback, particleCount);
            }
        }
        baseTopY = newBase;
        topY = newTop;
        prevSqueeze = squeeze;

        if (tankVisual != null)
        {
            float visH = tankHeight + squeeze * tankHeight * 0.15f;
            tankVisual.localPosition = new Vector3(0f, squeeze * tankHeight * 0.075f, 0f);
            tankVisual.localScale = new Vector3(tankWidth, visH, tankDepth);
        }

        if (kb.rKey.wasPressedThisFrame)
        {
            squeeze = 0f;
            prevSqueeze = 0f;
            transform.position = origPosition;
            if (tankVisual != null)
            {
                tankVisual.localPosition = Vector3.zero;
                tankVisual.localScale = new Vector3(tankWidth, tankHeight, tankDepth);
            }
            System.Array.Clear(drainNone, 0, drainNone.Length);
            drainedBuffer.SetData(drainNone);
            GenerateLattice();
            posBuffer.SetData(pos);
            velBuffer.SetData(vel);
            System.Array.Copy(pos, gpuPosReadback, particleCount);
        }
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

    void LateUpdate()
    {
        if (!ready || !Application.isPlaying) return;

        ApplyMaterialProperties();

        float dt = Mathf.Min(Time.deltaTime, 0.025f) / substeps;
        float h = particleRadius * 2.8f;

        Vector3 worldGravity = new Vector3(0f, gravityAccel, 0f);
        Vector3 worldVel = (transform.position - prevPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 worldAccel = (worldVel - prevWorldVel) / Mathf.Max(Time.deltaTime, 0.0001f);
        prevPos = transform.position;
        prevWorldVel = worldVel;

        Vector3 grav = transform.InverseTransformDirection(worldGravity - worldAccel);

        SetShaderConstants(h, grav, dt);

        for (int s = 0; s < substeps; s++)
        {
            computeShader.Dispatch(clearKernel, gridThreadGroups, 1, 1);
            computeShader.Dispatch(buildKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(densityKernel, particleThreadGroups, 1, 1);
            computeShader.Dispatch(forcesKernel, particleThreadGroups, 1, 1);
        }

        if (posReadbackPending && posReadbackRequest.done)
        {
            if (!posReadbackRequest.hasError)
                posReadbackRequest.GetData<Vector3>().CopyTo(gpuPosReadback);
            posReadbackPending = false;
        }

        int activeCount = 0;
        float pScale = particleRadius * 2.6f;
        Vector3 scl = Vector3.one * pScale;
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 rp = gpuPosReadback[i];
            pos[i] = rp;
            particleMatrices[activeCount] = Matrix4x4.TRS(transform.TransformPoint(rp), transform.rotation, scl);
            particleColors[activeCount] = colors[i];
            activeCount++;
        }

        if (!posReadbackPending && posBuffer != null)
        {
            posReadbackRequest = AsyncGPUReadback.Request(posBuffer);
            posReadbackPending = true;
        }

        if (instanceProps == null || mat == null || sphereMesh == null) return;
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
        computeShader.SetFloat("colorMixRate", 0f);
        computeShader.SetFloat("drainRate", 0f);
        computeShader.SetInt("spillMode", 0);
        computeShader.SetFloat("innerHalfX", innerHalfX);
        computeShader.SetFloat("innerHalfZ", innerHalfZ);
        computeShader.SetFloat("baseTopY", baseTopY);
        computeShader.SetFloat("topY", topY);
    }

    Mesh BuildSphereMesh()
    {
        Mesh m = new Mesh();
        m.name = "TankParticleSphere";
        int divs = 12;
        int vertCount = (divs + 1) * (divs + 1);
        Vector3[] verts = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        int[] tris = new int[divs * divs * 6];
        float phiStep = Mathf.PI / divs;
        float thetaStep = 2f * Mathf.PI / divs;
        int idx = 0, tidx = 0;
        for (int i = 0; i <= divs; i++)
        {
            float phi = i * phiStep;
            for (int j = 0; j <= divs; j++)
            {
                float theta = j * thetaStep;
                verts[idx] = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta));
                uv[idx] = new Vector2((float)j / divs, (float)i / divs);
                if (i < divs && j < divs)
                {
                    int a = idx, b = idx + 1, c = idx + divs + 1, d = idx + divs + 2;
                    tris[tidx++] = a; tris[tidx++] = c; tris[tidx++] = b;
                    tris[tidx++] = b; tris[tidx++] = c; tris[tidx++] = d;
                }
                idx++;
            }
        }
        m.vertices = verts;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    void OnDestroy()
    {
        if (posBuffer != null) posBuffer.Release();
        if (velBuffer != null) velBuffer.Release();
        if (densBuffer != null) densBuffer.Release();
        if (presBuffer != null) presBuffer.Release();
        if (colorsBuffer != null) colorsBuffer.Release();
        if (drainedBuffer != null) drainedBuffer.Release();
        if (cellCountsBuffer != null) cellCountsBuffer.Release();
        if (cellParticlesBuffer != null) cellParticlesBuffer.Release();
    }
}
