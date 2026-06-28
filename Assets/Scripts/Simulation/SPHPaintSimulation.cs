using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SPHPaintSimulation : MonoBehaviour
{
    [Header("Particle Settings")]
    public float particleRadius = 0.018f;
    public float particleMass = 0.06f;
    public int substeps = 2;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 25f;
    public float viscosity = 0.15f;
    public float surfaceTension = 1.5f;
    public float gravityAccel = -9.81f;
    public int relaxSteps = 25;

    [Header("Fill")]
    [Range(0f, 1f)]
    public float fillLevel = 1f;

    [Header("Colors")]
    public Color[] colorPalette = new Color[] {
        new Color(0.85f, 0.15f, 0.1f),
        new Color(0.1f, 0.4f, 0.85f),
        new Color(0.9f, 0.8f, 0.1f),
        new Color(0.2f, 0.85f, 0.3f)
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

    public bool showDebug;

    private float topY, baseTopY, innerTopR, innerBottomR;
    private float wallSlope, drainHoleR, minY, maxY;
    private Vector3[] pos, vel;
    private float[] dens, pres;
    private Color[] colors;
    private bool[] drained;
    private Transform bucketT;
    private int particleCount;
    private bool ready;
    private Material mat, trailMat;
    private Mesh sphereMesh;
    private MaterialPropertyBlock mpb, panelPB;
    private Matrix4x4[] particleMatrices;
    private Vector4[] particleColors;
    private MaterialPropertyBlock instanceProps;
    private float drainTimer;
    private bool ownsPanel;
    private SpatialHash3D spatial;
    private List<int> neighborScratch = new List<int>();
    private float[] presDivRhoSq, mvOverDens, mOverDens;
    private Queue<GameObject> trailPool = new Queue<GameObject>();
    private List<GameObject> activeTrails = new List<GameObject>();

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
        innerTopR = b.TopRadius - b.WallThickness;
        innerBottomR = b.BottomRadius - b.WallThickness;
        float hh = b.HandleHeight, h = b.Height, wt = b.WallThickness;
        topY = -hh - wt;
        baseTopY = -hh - h + wt;
        wallSlope = (innerTopR - innerBottomR) / (topY - baseTopY);
        drainHoleR = b.DrainHoleRadius + 0.01f;
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

        var ropeSim = FindFirstObjectByType<AdvancedBucketRopeSimulation>();
        if (ropeSim != null)
        {
            ropeSim.SetInitialThetaDegrees(35f);
            ropeSim.SetInitialPhiVelocity(1.5f);
            ropeSim.SetRopeLength(2f);
            ropeSim.SetRopeGravityMultiplier(0f);
            ropeSim.SetConstraintIterations(40);
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

        Shader shader = Shader.Find("Custom/FluidParticle");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetFloat("_Smoothness", glossiness);
        mat.SetFloat("_Opacity", 0.55f);
        mat.SetFloat("_FresnelPower", 2.5f);
        mat.SetColor("_SpecGloss", new Color(0.95f, 0.98f, 1f));

        Shader trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (trailShader == null) trailShader = Shader.Find("Unlit/Transparent");
        trailMat = new Material(trailShader);
        if (trailShader.name.Contains("Universal"))
            trailMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        trailMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        trailMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        trailMat.SetInt("_ZWrite", 0);
        trailMat.renderQueue = 3000;

        sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (sphereMesh == null) sphereMesh = BuildSphereMesh();

        spatial = new SpatialHash3D(particleRadius * 4f);

        mpb = new MaterialPropertyBlock();
        panelPB = new MaterialPropertyBlock();
        instanceProps = new MaterialPropertyBlock();
        mat.enableInstancing = true;

        PreRelax();
        ready = true;
    }

    void GenerateLattice()
    {
        float spacing = particleRadius * 2.0f;
        float fillTop = Mathf.Lerp(baseTopY, topY, fillLevel);
        if (colorPalette.Length == 0) colorPalette = new Color[] { Color.red };

        List<Vector3> posList = new List<Vector3>();
        List<Color> colList = new List<Color>();

        float topGap = (fillLevel > 0.99f) ? spacing * 0.15f : spacing;
        for (float y = baseTopY + spacing; y <= fillTop - topGap; y += spacing)
        {
            float t = Mathf.Clamp01((y - baseTopY) / (topY - baseTopY));
            float maxR = Mathf.Lerp(innerBottomR, innerTopR, t) - particleRadius * 1.5f;
            if (maxR < spacing * 0.5f) continue;

            int rings = Mathf.Max(1, Mathf.RoundToInt(maxR / spacing));
            for (int ring = 0; ring < rings; ring++)
            {
                float r = (ring + 0.5f) * maxR / rings;
                if (r < drainHoleR && y < baseTopY + spacing * 5f) continue;
                int perRing = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * r / spacing));
                for (int i = 0; i < perRing; i++)
                {
                    float a = (float)i / perRing * Mathf.PI * 2f;
                    posList.Add(new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)));
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
        presDivRhoSq = new float[particleCount];
        mvOverDens = new float[particleCount];
        mOverDens = new float[particleCount];
        particleMatrices = new Matrix4x4[particleCount];
        particleColors = new Vector4[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            pos[i] = posList[i];
            colors[i] = colList[i];
        }
    }

    void PreRelax()
    {
        float g = gravityAccel;
        gravityAccel = 0f;
        float dt = 0.003f;
        for (int s = 0; s < relaxSteps; s++)
        {
            float damp = 1f - 0.6f * s / relaxSteps;
            for (int i = 0; i < particleCount; i++) vel[i] *= damp;
            SimStep(dt);
        }
        for (int i = 0; i < particleCount; i++) vel[i] = Vector3.zero;
        gravityAccel = g;
    }

    void LateUpdate()
    {
        if (!ready) return;

        float dt = Mathf.Min(Time.deltaTime, 0.025f) / substeps;
        for (int s = 0; s < substeps; s++) SimStep(dt);
        ClampAllInside();

        // Render active particles
        int activeCount = 0;
        float pScale = particleRadius * 1.6f;
        Vector3 scl = Vector3.one * pScale;
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            particleMatrices[activeCount] = Matrix4x4.TRS(bucketT.TransformPoint(pos[i]), bucketT.rotation, scl);
            particleColors[activeCount] = colors[i];
            activeCount++;
        }
        instanceProps.SetVectorArray("_Color", particleColors);
        Graphics.DrawMeshInstanced(sphereMesh, 0, mat, particleMatrices, activeCount, instanceProps);

        // Paint on panel with liquid from inside bucket
        if (dripPanel != null)
        {
            Vector3 panelPos = bucketT.position;
            panelPos.y = dripPanel.transform.position.y + 0.001f;

            // Pick a random undrained particle for multi-color path
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

            dripPanel.PaintDot(panelPos, paintColor);

            // Splash effect: small dots around the main path
            if (Random.value < 0.4f)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector3 off = new Vector3(
                        Random.Range(-0.3f, 0.3f),
                        0f,
                        Random.Range(-0.3f, 0.3f)) * 0.05f;
                    dripPanel.PaintDot(panelPos + off, paintColor);
                }
            }
        }
    }

    void SimStep(float dt)
    {
        int n = particleCount;
        float h = particleRadius * 4f;
        float h2 = h * h;
        float m = particleMass;
        float wConst = W_CONST(h);
        float spikyConst = SPIKY_CONST(h);
        float viscConst = VISC_CONST(h);
        float wZero = wConst * h2 * h2 * h2;

        spatial.Clear();
        for (int i = 0; i < n; i++)
            if (!drained[i])
                spatial.Insert(i, pos[i]);

        for (int i = 0; i < n; i++)
        {
            if (drained[i]) continue;
            dens[i] = 0f;
            neighborScratch.Clear();
            spatial.Query(pos[i], neighborScratch);
            foreach (int j in neighborScratch)
            {
                if (drained[j]) continue;
                Vector3 d = pos[j] - pos[i];
                float d2 = d.sqrMagnitude;
                if (d2 < h2 && d2 > 1e-10f)
                {
                    float hdiff = h2 - d2;
                    dens[i] += m * wConst * hdiff * hdiff * hdiff;
                }
            }
            dens[i] = Mathf.Max(dens[i], 0.1f);
            pres[i] = gasStiffness * (dens[i] - restDensity);
        }

        // Precompute per-particle constants for force pass
        for (int i = 0; i < n; i++)
        {
            if (drained[i]) continue;
            float den2 = dens[i] * dens[i];
            presDivRhoSq[i] = pres[i] / den2;
            mvOverDens[i] = m * viscosity / dens[i];
            mOverDens[i] = m / dens[i];
        }

        Vector3 grav = bucketT.InverseTransformDirection(new Vector3(0f, gravityAccel, 0f));

        for (int i = 0; i < n; i++)
        {
            if (drained[i]) continue;
            Vector3 fPress = Vector3.zero;
            Vector3 fVisc = Vector3.zero;
            Vector3 fSurf = Vector3.zero;
            Vector3 colorNorm = Vector3.zero;

            Color mixedCol = colors[i] * wZero;
            float wSum = wZero;

            neighborScratch.Clear();
            spatial.Query(pos[i], neighborScratch);
            foreach (int j in neighborScratch)
            {
                if (i == j || drained[j]) continue;
                Vector3 rij = pos[j] - pos[i];
                float d2 = rij.sqrMagnitude;
                if (d2 > h2 || d2 < 1e-10f) continue;

                float d = Mathf.Sqrt(d2);
                Vector3 dir = rij / d;

                float hdiff = h2 - d2;
                float w = wConst * hdiff * hdiff * hdiff;
                float hMinusR = h - d;
                float spiky = spikyConst * hMinusR * hMinusR;
                float viscLap = viscConst * hMinusR;

                fPress += dir * m * (presDivRhoSq[i] + presDivRhoSq[j]) * spiky;
                fVisc += (vel[j] - vel[i]) * mvOverDens[j] * viscLap;

                float cohStrength = surfaceTension * w * mOverDens[j];
                fSurf -= dir * cohStrength / Mathf.Max(d, 0.001f);

                colorNorm += dir * mOverDens[j] * spiky;
                mixedCol += colors[j] * m * w;
                wSum += m * w;
            }

            if (wSum > 1e-10f)
            {
                mixedCol /= wSum;
                float t = 1f - Mathf.Exp(-colorMixRate * dt);
                colors[i] = Color.Lerp(colors[i], mixedCol, t);
            }

            if (colorNorm.magnitude > 0.01f)
            {
                float surfMag = fSurf.magnitude;
                if (surfMag > 0.01f)
                    fSurf = fSurf.normalized * Mathf.Min(surfMag, 5f);
            }
            else fSurf = Vector3.zero;

            Vector3 accel = fPress + fVisc + fSurf + grav;
            vel[i] += accel * dt;

            float spd = vel[i].magnitude;
            if (spd > speedCap) vel[i] *= speedCap / spd;

            pos[i] += vel[i] * dt;
        }

        SoftEnforce();
    }

    void SoftEnforce()
    {
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            float r = Mathf.Sqrt(pos[i].x * pos[i].x + pos[i].z * pos[i].z);
            bool atDrain = r < drainHoleR && drainActive;

            if (pos[i].y < baseTopY && !atDrain)
            {
                pos[i].y = baseTopY;
                vel[i].y = Mathf.Abs(vel[i].y) * 0.5f;
            }
            else if (pos[i].y > topY)
            {
                pos[i].y = topY;
                vel[i].y = -Mathf.Abs(vel[i].y) * 0.5f;
            }

            float cy = Mathf.Max(pos[i].y, baseTopY);
            float t = Mathf.Clamp01((cy - baseTopY) / (topY - baseTopY));
            float maxR = Mathf.Lerp(innerBottomR, innerTopR, t) - particleRadius;

            if (r > maxR && r > 0.0001f)
            {
                Vector2 d = new Vector2(pos[i].x, pos[i].z) / r;
                pos[i].x = d.x * maxR;
                pos[i].z = d.y * maxR;
                Vector3 n = new Vector3(-d.x, wallSlope, -d.y).normalized;
                float vn = Vector3.Dot(vel[i], n);
                if (vn < 0f) vel[i] -= vn * n * 1.5f;
            }
        }
    }

    void ClampAllInside()
    {
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            Vector3 p = pos[i];
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            bool atDrain = r < drainHoleR && p.y < baseTopY + 0.06f;
            bool fixed_ = false;

            if (p.y < minY && !atDrain) { p.y = minY; vel[i].y = 0f; fixed_ = true; }
            else if (p.y > maxY) { p.y = maxY; vel[i].y = 0f; fixed_ = true; }

            float cy = Mathf.Max(p.y, baseTopY);
            float t = Mathf.Clamp01((cy - baseTopY) / (topY - baseTopY));
            float safeR = Mathf.Lerp(innerBottomR, innerTopR, t) - particleRadius * 1.5f;
            if (safeR < 0.01f) safeR = 0.01f;

            if (r > safeR && r > 0.001f)
            {
                p.x *= safeR / r;
                p.z *= safeR / r;
                vel[i].x *= 0.3f; vel[i].z *= 0.3f;
                fixed_ = true;
            }

            if (atDrain && !drainActive)
            {
                p.y = baseTopY + 0.05f;
                vel[i].y = Mathf.Max(vel[i].y, 0.1f);
                fixed_ = true;
            }

            if (fixed_) pos[i] = p;
        }
    }

    void HandleDrain(float dt)
    {
        drainTimer += dt * drainRate * 8f;

        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            float r = Mathf.Sqrt(pos[i].x * pos[i].x + pos[i].z * pos[i].z);
            if (r < drainHoleR && pos[i].y < baseTopY + 0.03f && drainTimer >= 1f)
            {
                drained[i] = true;
                drainTimer = 0f;

                FallingDrop drop = new FallingDrop();
                drop.worldPos = bucketT.TransformPoint(pos[i]);
                Vector3 worldV = bucketT.TransformVector(vel[i]);
                float horizFactor = 0.35f;
                Vector3 dropVel = new Vector3(worldV.x * horizFactor, worldV.y, worldV.z * horizFactor);
                dropVel.y = Mathf.Min(dropVel.y, 0f);
                float maxDropSpeed = 4f;
                if (dropVel.magnitude > maxDropSpeed)
                    dropVel = dropVel.normalized * maxDropSpeed;
                drop.worldVel = dropVel;
                drop.color = colors[i];

                GameObject dropGo = new GameObject("Drop");
                dropGo.transform.position = drop.worldPos;
                dropGo.transform.localScale = Vector3.one * (particleRadius * 0.6f);
                dropGo.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                MeshRenderer dr = dropGo.AddComponent<MeshRenderer>();
                dr.sharedMaterial = mat;
                dr.receiveShadows = false;
                dr.shadowCastingMode = ShadowCastingMode.Off;
                mpb.SetColor("_Color", colors[i]);
                dr.SetPropertyBlock(mpb);
                drop.go = dropGo;

                GameObject trailGo;
                TrailRenderer tr;
                if (trailPool.Count > 0)
                {
                    trailGo = trailPool.Dequeue();
                    tr = trailGo.GetComponent<TrailRenderer>();
                    trailGo.SetActive(true);
                }
                else
                {
                    trailGo = new GameObject("Trail");
                    tr = trailGo.AddComponent<TrailRenderer>();
                    tr.sharedMaterial = trailMat;
                    tr.shadowCastingMode = ShadowCastingMode.Off;
                    tr.receiveShadows = false;
                }
                trailGo.transform.SetParent(null, false);
                trailGo.transform.position = drop.worldPos;
                tr.time = trailDuration;
                tr.startWidth = drainHoleR * 0.25f;
                tr.endWidth = 0.001f;
                panelPB.SetColor("_BaseColor", colors[i]);
                panelPB.SetColor("_Color", colors[i]);
                tr.SetPropertyBlock(panelPB);
                tr.Clear();

                drop.trail = tr;
                drop.life = trailDuration + 0.5f;
                activeTrails.Add(trailGo);
                drops.Add(drop);
            }
        }
    }

    void UpdateDrops(float dt)
    {
        Vector3 worldGrav = new Vector3(0f, gravityAccel, 0f);
        float panelY = (dripPanel != null) ? dripPanel.transform.position.y : float.MinValue;

        for (int d = drops.Count - 1; d >= 0; d--)
        {
            FallingDrop drop = drops[d];
            drop.life -= dt;

            drop.worldVel += worldGrav * dt;
            drop.worldPos += drop.worldVel * dt;

            bool hitPanel = drop.worldPos.y <= panelY;
            if (hitPanel)
            {
                dripPanel.DrawSplat(drop.worldPos, drop.color, drop.worldVel);
                SpawnSplash(drop);
                CleanupDrop(drop);
                drops.RemoveAt(d);
                continue;
            }

            drop.go.transform.position = drop.worldPos;
            if (drop.trail != null)
                drop.trail.transform.position = drop.worldPos;

            if (drop.life <= 0f || drop.worldPos.y < destroyHeight)
            {
                CleanupDrop(drop);
                drops.RemoveAt(d);
            }
        }
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

    static float W_CONST(float h)
    {
        float h2 = h * h;
        float h9 = h2 * h2 * h2 * h2 * h;
        return 315f / (64f * Mathf.PI * h9);
    }

    static float SPIKY_CONST(float h)
    {
        float h6 = h * h * h * h * h * h;
        return -45f / (Mathf.PI * h6);
    }

    static float VISC_CONST(float h)
    {
        float h6 = h * h * h * h * h * h;
        return 45f / (Mathf.PI * h6);
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

    void OnDrawGizmosSelected()
    {
        if (!showDebug || bucketT == null) return;
        Gizmos.color = Color.cyan;
        Vector3 c = bucketT.position;
        Gizmos.DrawWireSphere(c + bucketT.up * topY, innerTopR);
        Gizmos.DrawWireSphere(c + bucketT.up * baseTopY, innerBottomR);
    }

    void OnDestroy()
    {
        foreach (var d in drops)
            CleanupDrop(d);
        while (trailPool.Count > 0)
            Destroy(trailPool.Dequeue());
        if (ownsPanel && dripPanel != null)
            Destroy(dripPanel.gameObject);
    }

    private class SpatialHash3D
    {
        private float cellSize;
        private Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
        private List<Vector3Int> activeKeys = new List<Vector3Int>();

        public SpatialHash3D(float cellSize)
        {
            this.cellSize = cellSize;
        }

        public void Clear()
        {
            foreach (var k in activeKeys)
            {
                if (cells.TryGetValue(k, out var list))
                    list.Clear();
            }
            activeKeys.Clear();
        }

        public void Insert(int index, Vector3 pos)
        {
            var key = CellKey(pos);
            if (!cells.TryGetValue(key, out var list))
            {
                list = new List<int>();
                cells[key] = list;
            }
            if (list.Count == 0) activeKeys.Add(key);
            list.Add(index);
        }

        public void Query(Vector3 pos, List<int> results)
        {
            var center = CellKey(pos);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var key = new Vector3Int(center.x + dx, center.y + dy, center.z + dz);
                if (cells.TryGetValue(key, out var list))
                    results.AddRange(list);
            }
        }

        private Vector3Int CellKey(Vector3 pos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(pos.x / cellSize),
                Mathf.FloorToInt(pos.y / cellSize),
                Mathf.FloorToInt(pos.z / cellSize)
            );
        }
    }
}
