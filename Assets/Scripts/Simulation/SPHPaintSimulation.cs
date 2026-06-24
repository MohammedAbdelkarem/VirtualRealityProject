using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SPHPaintSimulation : MonoBehaviour
{
    [Header("Particle Settings")]
    public float particleRadius = 0.018f;
    public float particleMass = 0.06f;
    public int substeps = 1;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 12f;
    public float viscosity = 0.15f;
    public float surfaceTension = 1.5f;
    public float gravityAccel = -9.81f;
    public int relaxSteps = 12;

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
    public float drainRate = 0.8f;
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
    private Transform[] particles;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock mpb, panelPB;
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

    private class PanelDroplet
    {
        public GameObject go;
        public Vector3 localPos;
        public Color color;
    }
    private List<PanelDroplet> panelDrops = new List<PanelDroplet>();

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

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetFloat("_Smoothness", glossiness);
        mat.SetFloat("_Metallic", metallic);

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

        float scale = particleRadius * 2f;
        mpb = new MaterialPropertyBlock();
        panelPB = new MaterialPropertyBlock();

        for (int i = 0; i < particleCount; i++)
        {
            GameObject go = new GameObject();
            go.transform.SetParent(bucketT, false);
            go.transform.localPosition = pos[i];
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            renderers[i] = go.AddComponent<MeshRenderer>();
            renderers[i].sharedMaterial = mat;
            renderers[i].receiveShadows = false;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            particles[i] = go.transform;
        }

        PreRelax();
        ready = true;
    }

    void GenerateLattice()
    {
        float spacing = particleRadius * 2.8f;
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
        particles = new Transform[particleCount];
        renderers = new MeshRenderer[particleCount];
        presDivRhoSq = new float[particleCount];
        mvOverDens = new float[particleCount];
        mOverDens = new float[particleCount];

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
        for (int i = 0; i < particleCount; i++)
        {
            if (drained[i]) continue;
            particles[i].localPosition = pos[i];
            mpb.SetColor("_Color", colors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }

        // Drain
        float frameDt = Mathf.Min(Time.deltaTime, 0.025f);
        if (drainActive) HandleDrain(frameDt);
        UpdateDrops(frameDt);
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

                float hMinusR = h - d;
                float spiky = spikyConst * hMinusR * hMinusR;
                float viscLap = viscConst * hMinusR;

                fPress += dir * m * (presDivRhoSq[i] + presDivRhoSq[j]) * spiky;
                fVisc += (vel[j] - vel[i]) * mvOverDens[j] * viscLap;
            }

            Vector3 accel = fPress + fVisc + grav;
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

                drop.go = particles[i].gameObject;
                drop.go.transform.SetParent(null, true);
                drop.go.transform.position = drop.worldPos;
                drop.go.transform.localScale = Vector3.one * (particleRadius * 1.5f);

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
                tr.startWidth = drainHoleR * 0.6f;
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

    const int MAX_PANEL_DROPS = 500;

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
                drop.worldPos.y = panelY;

                if (drop.worldVel.y < -0.3f && drop.go.transform.localScale.x >= particleRadius * 1.0f)
                {
                    dripPanel.DrawSplat(drop.worldPos, drop.color);
                    drop.worldVel.y = -drop.worldVel.y * 0.3f;
                    drop.worldVel.x *= 0.7f;
                    drop.worldVel.z *= 0.7f;
                    drop.worldPos.y = panelY + 0.002f;
                    drop.go.transform.position = drop.worldPos;
                    if (drop.trail != null)
                        drop.trail.transform.position = drop.worldPos;
                    continue;
                }

                if (drop.worldVel.y < -0.3f)
                {
                    drop.worldVel.y = -drop.worldVel.y * 0.3f;
                    drop.worldVel.x *= 0.7f;
                    drop.worldVel.z *= 0.7f;
                    drop.worldPos.y = panelY + 0.002f;
                    drop.go.transform.position = drop.worldPos;
                    if (drop.trail != null)
                        drop.trail.transform.position = drop.worldPos;
                    continue;
                }

                dripPanel.DrawSplat(drop.worldPos, drop.color);

                Vector3 local = dripPanel.transform.InverseTransformPoint(drop.worldPos);
                local.y = 0.003f;
                bool merged = false;
                float mergeSq = drainHoleR * 0.9f;
                mergeSq *= mergeSq;
                int checkStart = Mathf.Max(0, panelDrops.Count - 20);
                for (int pi = panelDrops.Count - 1; pi >= checkStart; pi--)
                {
                    var pd = panelDrops[pi];
                    if ((pd.localPos - local).sqrMagnitude < mergeSq)
                    {
                        pd.color = Color.Lerp(pd.color, drop.color, 0.5f);
                        MeshRenderer mr = pd.go.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            panelPB.SetColor("_Color", pd.color);
                            panelPB.SetColor("_BaseColor", pd.color);
                            mr.SetPropertyBlock(panelPB);
                        }
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    drop.go.transform.SetParent(dripPanel.transform, true);
                    drop.go.transform.localPosition = local;
                    MeshRenderer mr = drop.go.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        panelPB.SetColor("_Color", drop.color);
                        panelPB.SetColor("_BaseColor", drop.color);
                        mr.SetPropertyBlock(panelPB);
                    }

                    if (panelDrops.Count >= MAX_PANEL_DROPS)
                    {
                        PanelDroplet old = panelDrops[0];
                        if (old.go != null) Destroy(old.go);
                        panelDrops.RemoveAt(0);
                    }
                    panelDrops.Add(new PanelDroplet
                    {
                        go = drop.go,
                        localPos = local,
                        color = drop.color
                    });
                }
                else
                {
                    if (drop.go != null) Destroy(drop.go);
                }

                ReturnTrail(drop.trail);
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
        if (particles != null)
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null)
                    Destroy(particles[i].gameObject);
        foreach (var d in drops)
            CleanupDrop(d);
        foreach (var pd in panelDrops)
            if (pd.go != null) Destroy(pd.go);
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
