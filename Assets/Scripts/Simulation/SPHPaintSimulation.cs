using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SPHPaintSimulation : MonoBehaviour
{
    [Header("Particle Settings")]
    public float particleRadius = 0.018f;
    public float particleMass = 0.06f;
    public int substeps = 3;
    public float speedCap = 15f;

    [Header("SPH Fluid")]
    public float restDensity = 1000f;
    public float gasStiffness = 12f;
    public float viscosity = 0.15f;
    public float surfaceTension = 1.5f;
    public float gravityAccel = -9.81f;
    public int relaxSteps = 30;

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
    public float drainRate = 3f;
    [Range(0.1f, 3f)]
    public float trailDuration = 1.5f;
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
    private MaterialPropertyBlock mpb;
    private float drainTimer;
    private bool ownsPanel;

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
            dripPanel = FindObjectOfType<DripPanel>();
        if (dripPanel == null)
        {
            GameObject panelGO = new GameObject("DripPanel");
            panelGO.transform.position = new Vector3(0f, 0.02f, 0f);
            panelGO.transform.rotation = Quaternion.identity;
            dripPanel = panelGO.AddComponent<DripPanel>();
            ownsPanel = true;
        }

        var ropeSim = FindObjectOfType<AdvancedBucketRopeSimulation>();
        if (ropeSim != null)
        {
            ropeSim.SetInitialThetaDegrees(35f);
            ropeSim.SetInitialPhiVelocity(1.5f);
            ropeSim.SetRopeLength(2f);
            ropeSim.SetRopeGravityMultiplier(0f);
            ropeSim.SetConstraintIterations(40);
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

        float scale = particleRadius * 2f;
        mpb = new MaterialPropertyBlock();

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
        float spacing = particleRadius * 1.7f;
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

        for (int i = 0; i < n; i++)
        {
            if (drained[i]) continue;
            dens[i] = 0f;
            for (int j = 0; j < n; j++)
            {
                if (drained[j]) continue;
                Vector3 d = pos[j] - pos[i];
                float d2 = d.sqrMagnitude;
                if (d2 < h2 && d2 > 1e-10f)
                    dens[i] += m * W_poly6(h, Mathf.Sqrt(d2));
            }
            dens[i] = Mathf.Max(dens[i], 0.1f);
            pres[i] = gasStiffness * (dens[i] - restDensity);
        }

        Vector3 grav = bucketT.InverseTransformDirection(new Vector3(0f, gravityAccel, 0f));

        for (int i = 0; i < n; i++)
        {
            if (drained[i]) continue;
            Vector3 fPress = Vector3.zero;
            Vector3 fVisc = Vector3.zero;
            Vector3 fSurf = Vector3.zero;
            Vector3 colorNorm = Vector3.zero;
            float rho_i2 = dens[i] * dens[i];

            Color mixedCol = colors[i] * W_poly6(h, 0f);
            float wSum = W_poly6(h, 0f);

            for (int j = 0; j < n; j++)
            {
                if (i == j || drained[j]) continue;
                Vector3 rij = pos[j] - pos[i];
                float d2 = rij.sqrMagnitude;
                if (d2 > h2 || d2 < 1e-10f) continue;

                float d = Mathf.Sqrt(d2);
                Vector3 dir = rij / d;

                float w = W_poly6(h, d);
                float spiky = dW_spiky(h, d);
                float viscLap = laplace_viscosity(h, d);

                fPress += dir * m * (pres[i] / rho_i2 + pres[j] / (dens[j] * dens[j])) * spiky;
                fVisc += (vel[j] - vel[i]) * m * viscosity / dens[j] * viscLap;

                float cohStrength = surfaceTension * w * m / dens[j];
                fSurf -= dir * cohStrength / Mathf.Max(d, 0.001f);

                colorNorm += dir * m / dens[j] * spiky;
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
                drop.worldVel = new Vector3(worldV.x * horizFactor, worldV.y, worldV.z * horizFactor);
                drop.color = colors[i];

                drop.go = particles[i].gameObject;
                drop.go.transform.SetParent(null, true);
                drop.go.transform.position = drop.worldPos;
                drop.go.transform.localScale = Vector3.one * (particleRadius * 1.5f);

                GameObject trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(null, false);
                trailGo.transform.position = drop.worldPos;

                TrailRenderer tr = trailGo.AddComponent<TrailRenderer>();
                tr.time = trailDuration;
                tr.startWidth = drainHoleR * 0.6f;
                tr.endWidth = 0.001f;
                tr.material = Instantiate(trailMat);
                tr.material.color = colors[i];
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.receiveShadows = false;

                drop.trail = tr;
                drop.life = trailDuration + 0.5f;
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

            drop.go.transform.position = drop.worldPos;
            drop.trail.transform.position = drop.worldPos;

            // Check panel impact
            if (drop.worldPos.y <= panelY)
            {
                dripPanel.DrawSplat(drop.worldPos, drop.color);

                // Keep sphere on panel as a visible droplet
                Vector3 local = dripPanel.transform.InverseTransformPoint(drop.worldPos);
                drop.go.transform.SetParent(dripPanel.transform, true);
                drop.go.transform.localPosition = new Vector3(local.x, 0.003f, local.z);
                var mr = drop.go.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = drop.color;

                // Destroy trail (its path is no longer needed)
                if (drop.trail != null) Destroy(drop.trail.gameObject);

                drops.RemoveAt(d);
                continue;
            }

            if (drop.life <= 0f || drop.worldPos.y < destroyHeight)
            {
                CleanupDrop(drop);
                drops.RemoveAt(d);
            }
        }
    }

    void CleanupDrop(FallingDrop drop)
    {
        if (drop.trail != null) Destroy(drop.trail.gameObject);
        if (drop.go != null) Destroy(drop.go);
    }

    static float W_poly6(float h, float r)
    {
        float h2 = h * h, h9 = h2 * h2 * h2 * h2 * h;
        return 315f / (64f * Mathf.PI * h9) * Mathf.Pow(h2 - r * r, 3f);
    }

    static float dW_spiky(float h, float r)
    {
        float h6 = h * h * h * h * h * h;
        return -45f / (Mathf.PI * h6) * (h - r) * (h - r);
    }

    static float laplace_viscosity(float h, float r)
    {
        float h6 = h * h * h * h * h * h;
        return 45f / (Mathf.PI * h6) * (h - r);
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
        if (ownsPanel && dripPanel != null)
            Destroy(dripPanel.gameObject);
    }
}
