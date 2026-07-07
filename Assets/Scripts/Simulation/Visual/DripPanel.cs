using UnityEngine;

public class DripPanel : MonoBehaviour
{
    public enum PanelMaterial { Wood, Cloth, Ceramic }

    [Header("Panel")]
    public Vector2 panelSize = new Vector2(5f, 5f);

    [Header("Paint")]
    public int textureResolution = 256;
    public float splatPixelRadius = 10f;
    public float splatOpacity = 0.85f;
    public Color backgroundColor = Color.black;

    [Header("Material Type")]
    public PanelMaterial materialType = PanelMaterial.Wood;
    private PanelMaterial prevMaterialType = PanelMaterial.Wood;
    private bool built;

    [Header("Wood")]
    public Color woodBaseColor = new Color(0.55f, 0.38f, 0.22f);
    public Color woodGrainColor = new Color(0.35f, 0.22f, 0.12f);
    [Range(0.1f, 5f)]
    public float grainScale = 1.5f;
    [Range(0f, 1f)]
    public float absorptionRate = 0.15f;
    [Range(0f, 0.5f)]
    public float wetSheen = 0.2f;

    [Header("Cloth")]
    public Color clothBaseColor = new Color(0.92f, 0.9f, 0.88f);
    public Color clothThreadColor = new Color(0.8f, 0.78f, 0.75f);
    [Range(0.5f, 8f)]
    public float weaveScale = 3f;
    [Range(0f, 0.5f)]
    public float wickingRate = 0.05f;

    [Header("Ceramic")]
    public Color ceramicBaseColor = new Color(0.95f, 0.95f, 0.92f);
    public Color ceramicGroutColor = new Color(0.7f, 0.7f, 0.7f);
    [Range(0.1f, 2f)]
    public float ceramicTileScale = 1f;
    [Range(0f, 0.5f)]
    public float ceramicGroutWidth = 0.04f;
    [Range(0f, 0.3f)]
    public float ceramicGloss = 0.15f;

    private Texture2D paintTexture;
    private Material panelMaterial;
    private bool textureDirty;
    private Color[] pixels;

    // Per-pixel state (shared between wood and cloth)
    private float[] surfaceAmount;
    private Color[] storedColor;
    private float[] absorbedAmount;
    private Vector2[] ceramicVel;
    private Vector2[] woodVel;
    private float[] tempBuf1, tempBuf2;
    private bool hasActiveLiquid;

    void Start()
    {
        BuildPanel();
    }

    void BuildPanel()
    {
        int res = Mathf.Clamp(textureResolution, 64, 1024);
        paintTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        paintTexture.wrapMode = TextureWrapMode.Clamp;

        int total = res * res;
        pixels = new Color[total];
        surfaceAmount = new float[total];
        storedColor = new Color[total];
        absorbedAmount = new float[total];
        ceramicVel = new Vector2[total];
        woodVel = new Vector2[total];
        tempBuf1 = new float[total];
        tempBuf2 = new float[total];

        FillBackground();

        paintTexture.SetPixels(pixels);
        paintTexture.Apply();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Standard");
        panelMaterial = new Material(shader);
        panelMaterial.SetTexture("_BaseMap", paintTexture);
        panelMaterial.SetTexture("_MainTex", paintTexture);
        panelMaterial.SetColor("_BaseColor", backgroundColor);
        panelMaterial.SetColor("_Color", backgroundColor);
        panelMaterial.SetFloat("_Cull", 0f);
        panelMaterial.SetFloat("_Surface", 0f);
        panelMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        panelMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        panelMaterial.SetInt("_ZWrite", 1);
        panelMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        panelMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

        Mesh mesh = new Mesh();
        mesh.name = "DripPanelMesh";
        Vector3[] verts = new Vector3[4];
        Vector2[] uvs = new Vector2[4];
        int[] tris = new int[6];

        float hw = panelSize.x * 0.5f;
        float hd = panelSize.y * 0.5f;

        verts[0] = new Vector3(-hw, 0, -hd); uvs[0] = new Vector2(0, 0);
        verts[1] = new Vector3(hw, 0, -hd); uvs[1] = new Vector2(1, 0);
        verts[2] = new Vector3(-hw, 0, hd); uvs[2] = new Vector2(0, 1);
        verts[3] = new Vector3(hw, 0, hd); uvs[3] = new Vector2(1, 1);

        tris[0] = 0; tris[1] = 2; tris[2] = 1;
        tris[3] = 1; tris[4] = 2; tris[5] = 3;

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(new Vector3[] {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        });
        mesh.SetTangents(new Vector4[] {
            new Vector4(1,0,0,1), new Vector4(1,0,0,1),
            new Vector4(1,0,0,1), new Vector4(1,0,0,1)
        });
        mesh.RecalculateBounds();

        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = panelMaterial;
        mr.receiveShadows = false;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        prevMaterialType = materialType;
        built = true;
    }

    public void SetPanelSize(Vector2 newSize)
    {
        panelSize = newSize;
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh mesh = mf.sharedMesh;
        float hw = panelSize.x * 0.5f;
        float hd = panelSize.y * 0.5f;
        Vector3[] verts = new Vector3[] {
            new Vector3(-hw, 0, -hd),
            new Vector3(hw, 0, -hd),
            new Vector3(-hw, 0, hd),
            new Vector3(hw, 0, hd)
        };
        mesh.SetVertices(verts);
        mesh.RecalculateBounds();
    }

    void FillBackground()
    {
        int res = paintTexture.width;
        int total = res * res;

        if (materialType == PanelMaterial.Wood)
        {
            for (int i = 0; i < total; i++)
            {
                int py = i / res;
                int px = i - py * res;
                float u = (float)px / res;
                float v = (float)py / res;
                pixels[i] = SampleWoodGrain(u, v);
            }
        }
        else if (materialType == PanelMaterial.Cloth)
        {
            for (int i = 0; i < total; i++)
            {
                int py = i / res;
                int px = i - py * res;
                float u = (float)px / res;
                float v = (float)py / res;
                pixels[i] = SampleWeave(u, v);
            }
        }
        else if (materialType == PanelMaterial.Ceramic)
        {
            for (int i = 0; i < total; i++)
            {
                int py = i / res;
                int px = i - py * res;
                float u = (float)px / res;
                float v = (float)py / res;
                pixels[i] = SampleTile(u, v);
            }
        }
    }

    void RebuildPixels()
    {
        if (paintTexture == null) return;
        int total = pixels.Length;
        for (int i = 0; i < total; i++)
        {
            surfaceAmount[i] = 0f;
            absorbedAmount[i] = 0f;
        }
        if (ceramicVel != null) System.Array.Clear(ceramicVel, 0, total);
        if (woodVel != null) System.Array.Clear(woodVel, 0, total);
        hasActiveLiquid = false;
        FillBackground();
    }

    Color SampleWoodGrain(float u, float v)
    {
        u *= grainScale;
        v *= grainScale;

        float rings = 0f;
        float noise = Mathf.Sin(u * 3.7f + 1.2f) * 0.3f + Mathf.Sin(v * 5.1f + 0.8f) * 0.2f;
        rings = Mathf.Sin((u + noise) * 12f + Mathf.Sin(v * 8f) * 0.4f) * 0.5f + 0.5f;

        float knot = Mathf.Sin(Mathf.Sqrt((u - 0.3f) * (u - 0.3f) + (v - 0.7f) * (v - 0.7f)) * 30f) * 0.5f + 0.5f;
        if (knot > 0.6f) rings = Mathf.Lerp(rings, knot, 0.4f);

        float grainNoise = Mathf.Sin(u * 47f + v * 31f) * 0.08f + Mathf.Sin(u * 103f + v * 71f) * 0.04f;
        rings += grainNoise;

        return Color.Lerp(woodGrainColor, woodBaseColor, Mathf.Clamp01(rings));
    }

    Color SampleWeave(float u, float v)
    {
        u *= weaveScale;
        v *= weaveScale;

        float warp = Mathf.Abs(Mathf.Sin(u * 3.14159f)) * 0.5f + 0.5f;
        float weft = Mathf.Abs(Mathf.Sin(v * 3.14159f)) * 0.5f + 0.5f;
        float weave = Mathf.Min(warp, weft);
        weave = Mathf.Pow(weave, 0.6f);

        float noise = Mathf.Sin(u * 37f + v * 29f) * 0.03f + Mathf.Sin(u * 71f + v * 53f) * 0.02f;
        weave += noise;

        return Color.Lerp(clothThreadColor, clothBaseColor, Mathf.Clamp01(weave * 1.2f));
    }

    Color SampleTile(float u, float v)
    {
        float s = ceramicTileScale;
        float g = ceramicGroutWidth * s;
        float tileU = u * s, tileV = v * s;
        float fu = tileU - Mathf.Floor(tileU);
        float fv = tileV - Mathf.Floor(tileV);
        bool grout = fu < g || fu > 1f - g || fv < g || fv > 1f - g;
        if (grout)
            return ceramicGroutColor;
        return ceramicBaseColor;
    }

    void Update()
    {
        if (built && materialType != prevMaterialType)
        {
            prevMaterialType = materialType;
            RebuildPixels();
            textureDirty = true;
        }

        if (!textureDirty && !hasActiveLiquid) return;

        if (materialType == PanelMaterial.Wood)
            SimulateWood();
        else if (materialType == PanelMaterial.Cloth)
            SimulateCloth();
        else if (materialType == PanelMaterial.Ceramic)
            SimulateCeramic();

        paintTexture.SetPixels(pixels);
        paintTexture.Apply(false, false);
        textureDirty = false;

        ScanActiveLiquid();
    }

    void ScanActiveLiquid()
    {
        hasActiveLiquid = false;
        int total = pixels.Length;
        if (materialType == PanelMaterial.Cloth)
        {
            for (int i = 0; i < total; i++)
                if (absorbedAmount[i] > 0.002f) { hasActiveLiquid = true; break; }
        }
        else
        {
            for (int i = 0; i < total; i++)
                if (surfaceAmount[i] > 0.001f || absorbedAmount[i] > 0.001f)
                { hasActiveLiquid = true; break; }
        }
    }

    struct SWEParams
    {
        public float g, st, advRate, visc, evap;
        public float flowX, flowY;
        public bool applySt;
    }

    void SimulateShallowWater(float[] h, Vector2[] vel, SWEParams p, float dt, ref bool active)
    {
        int res = paintTexture.width;
        int total = res * res;
        int substeps = Mathf.Max(1, Mathf.RoundToInt(dt / 0.005f));
        float subDt = dt / substeps;
        float[] hNew = tempBuf1;
        float[] lap = p.applySt ? tempBuf2 : null;

        for (int step = 0; step < substeps; step++)
        {
            System.Array.Copy(h, hNew, total);
            if (p.applySt)
            {
                for (int i = 0; i < total; i++)
                {
                    if (h[i] < 0.0001f) { lap[i] = 0; continue; }
                    int py = i / res, px = i - py * res;
                    float l = 0f; int n = 0;
                    if (px > 0) { l += h[i - 1]; n++; }
                    if (px < res - 1) { l += h[i + 1]; n++; }
                    if (py > 0) { l += h[i - res]; n++; }
                    if (py < res - 1) { l += h[i + res]; n++; }
                    lap[i] = l - n * h[i];
                }
            }
            for (int i = 0; i < total; i++)
            {
                if (h[i] < 0.0001f) continue;
                int py = i / res, px = i - py * res;
                Vector2 v = vel[i];
                float dH_dx = 0, dH_dy = 0;
                if (px > 0 && px < res - 1)
                {
                    dH_dx = (h[i + 1] - h[i - 1]) * 0.5f;
                    dH_dy = (py > 0 && py < res - 1) ? (h[i + res] - h[i - res]) * 0.5f
                        : (py == 0) ? (h[i + res] - h[i]) : (h[i] - h[i - res]);
                }
                else
                {
                    if (px == 0) dH_dx = h[i + 1] - h[i]; else dH_dx = h[i] - h[i - 1];
                    if (py > 0 && py < res - 1) dH_dy = (h[i + res] - h[i - res]) * 0.5f;
                    else if (py == 0) dH_dy = h[i + res] - h[i]; else dH_dy = h[i] - h[i - res];
                }
                v -= new Vector2(dH_dx * p.flowX, dH_dy * p.flowY) * (p.g * subDt);
                if (p.applySt && lap != null)
                {
                    float dLap_dx = 0, dLap_dy = 0;
                    if (px > 0) dLap_dx += lap[i] - lap[i - 1];
                    if (px < res - 1) dLap_dx += lap[i + 1] - lap[i];
                    if (py > 0) dLap_dy += lap[i] - lap[i - res];
                    if (py < res - 1) dLap_dy += lap[i + res] - lap[i];
                    v += new Vector2(dLap_dx, dLap_dy) * (p.st * subDt);
                }
                float avgUx = 0, avgUy = 0;
                int cn = 0;
                if (px > 0) { avgUx += vel[i - 1].x; avgUy += vel[i - 1].y; cn++; }
                if (px < res - 1) { avgUx += vel[i + 1].x; avgUy += vel[i + 1].y; cn++; }
                if (py > 0) { avgUx += vel[i - res].x; avgUy += vel[i - res].y; cn++; }
                if (py < res - 1) { avgUx += vel[i + res].x; avgUy += vel[i + res].y; cn++; }
                if (cn > 0) { avgUx /= cn; avgUy /= cn; }
                v.x += (avgUx - v.x) * p.advRate;
                v.y += (avgUy - v.y) * p.advRate;
                v *= Mathf.Max(0f, 1f - p.visc * subDt);
                vel[i] = v;
            }
            for (int i = 0; i < total; i++)
            {
                if (h[i] < 0.0001f) continue;
                int py = i / res, px = i - py * res;
                float lf = 0, rf = 0, bf = 0, tf = 0;
                if (px < res - 1)
                {
                    float uf = (vel[i].x * p.flowX + vel[i + 1].x * p.flowX) * 0.5f;
                    rf = uf * ((uf > 0) ? h[i] : h[i + 1]) * subDt;
                }
                if (px > 0)
                {
                    float uf = (vel[i - 1].x * p.flowX + vel[i].x * p.flowX) * 0.5f;
                    lf = uf * ((uf > 0) ? h[i - 1] : h[i]) * subDt;
                }
                if (py < res - 1)
                {
                    float vf = (vel[i].y * p.flowY + vel[i + res].y * p.flowY) * 0.5f;
                    tf = vf * ((vf > 0) ? h[i] : h[i + res]) * subDt;
                }
                if (py > 0)
                {
                    float vf = (vel[i - res].y * p.flowY + vel[i].y * p.flowY) * 0.5f;
                    bf = vf * ((vf > 0) ? h[i - res] : h[i]) * subDt;
                }
                hNew[i] = h[i] - (rf - lf + tf - bf);
                if (hNew[i] < 0.001f) hNew[i] = 0f;
            }
            System.Array.Copy(hNew, h, total);
        }
        for (int i = 0; i < total; i++)
        {
            if (h[i] > 0.001f)
            {
                h[i] *= p.evap;
                if (h[i] > 0.001f) active = true; else h[i] = 0f;
            }
        }
    }

    void SimulateWood()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.02f);
        int res = paintTexture.width;
        int total = res * res;
        bool active = false;

        SWEParams p;
        p.g = 0.6f; p.st = 0.02f; p.advRate = 0.2f;
        p.visc = 3.5f; p.flowX = 1.5f; p.flowY = 0.5f;
        p.applySt = true; p.evap = 1f;
        SimulateShallowWater(surfaceAmount, woodVel, p, dt, ref active);

        float absRate = absorptionRate * 3f;
        for (int i = 0; i < total; i++)
        {
            float h = surfaceAmount[i];
            if (h <= 0.001f) continue;
            int py = i / res, px = i - py * res;
            float u = (float)px / res;
            float gf = Mathf.Lerp(0.5f, 1.8f, Mathf.Abs(u - 0.5f) * 2f);
            float tr = Mathf.Min(h, h * absRate * dt * gf);
            absorbedAmount[i] = Mathf.Min(1f, absorbedAmount[i] + tr);
            surfaceAmount[i] -= tr;
            if (surfaceAmount[i] < 0.001f) surfaceAmount[i] = 0f; else active = true;
        }

        float diffRate = absorptionRate * 2f * dt;
        if (diffRate > 0.0001f)
        {
            float[] next = tempBuf2;
            System.Array.Copy(absorbedAmount, next, total);
            for (int i = 0; i < total; i++)
            {
                float a = absorbedAmount[i];
                if (a <= 0.005f) continue;
                int py = i / res, px = i - py * res;
                float sx = diffRate * a * 1.5f, sy = diffRate * a * 0.5f;
                if (px > 0) next[i - 1] = Mathf.Min(1f, next[i - 1] + sx);
                if (px < res - 1) next[i + 1] = Mathf.Min(1f, next[i + 1] + sx);
                if (py > 0) next[i - res] = Mathf.Min(1f, next[i - res] + sy);
                if (py < res - 1) next[i + res] = Mathf.Min(1f, next[i + res] + sy);
                next[i] -= (sx * 2f + sy * 2f);
                if (next[i] < 0.001f) next[i] = 0f; else active = true;
            }
            System.Array.Copy(next, absorbedAmount, total);
        }
        hasActiveLiquid = active;

        for (int i = 0; i < total; i++)
        {
            int py = i / res, px = i - py * res;
            float u = (float)px / res;
            Color wood = SampleWoodGrain(u, (float)py / res);
            float h = surfaceAmount[i], a = absorbedAmount[i];
            if (a > 0.005f)
            {
                float aa = Mathf.Min(1f, a * 2f);
                Color stain = Color.Lerp(storedColor[i], Color.black, 0.15f);
                wood = Color.Lerp(wood, stain, aa);
                float dk = 1f - aa * 0.15f;
                wood.r *= dk; wood.g *= dk; wood.b *= dk;
            }
            if (h > 0.001f)
            {
                float t = Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, h * 2f));
                wood = Color.Lerp(wood, storedColor[i], t);
                float wet = h * wetSheen;
                wood.r = Mathf.Min(1f, wood.r + wet);
                wood.g = Mathf.Min(1f, wood.g + wet);
                wood.b = Mathf.Min(1f, wood.b + wet);
            }
            pixels[i] = wood;
        }
    }

    void SimulateCloth()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.02f);
        int res = paintTexture.width;
        int total = res * res;
        bool active = false;

        float diffRate = wickingRate * dt;
        if (diffRate > 0.0001f)
        {
            float[] next = tempBuf1;
            System.Array.Copy(absorbedAmount, next, total);
            for (int i = 0; i < total; i++)
            {
                float a = absorbedAmount[i];
                if (a <= 0.002f) continue;
                int py = i / res, px = i - py * res;
                float fl = 0, fr = 0, fb = 0, ft = 0;
                if (px > 0) { float flux = diffRate * (a - absorbedAmount[i - 1]) * 0.5f; fl = Mathf.Max(0, flux); }
                if (px < res - 1) { float flux = diffRate * (a - absorbedAmount[i + 1]) * 0.5f; fr = Mathf.Max(0, flux); }
                if (py > 0) { float flux = diffRate * (a - absorbedAmount[i - res]) * 0.5f; fb = Mathf.Max(0, flux); }
                if (py < res - 1) { float flux = diffRate * (a - absorbedAmount[i + res]) * 0.5f; ft = Mathf.Max(0, flux); }
                float totalOut = fl + fr + fb + ft;
                float rem = a - totalOut;
                if (rem > 0.001f)
                {
                    next[i] = rem;
                    if (px > 0) next[i - 1] = Mathf.Min(2f, next[i - 1] + fl);
                    if (px < res - 1) next[i + 1] = Mathf.Min(2f, next[i + 1] + fr);
                    if (py > 0) next[i - res] = Mathf.Min(2f, next[i - res] + fb);
                    if (py < res - 1) next[i + res] = Mathf.Min(2f, next[i + res] + ft);
                }
            }
            for (int i = 0; i < total; i++)
            {
                if (next[i] > 0.002f) active = true;
            }
            System.Array.Copy(next, absorbedAmount, total);
        }
        hasActiveLiquid = active;

        for (int i = 0; i < total; i++)
        {
            int py = i / res, px = i - py * res;
            float u = (float)px / res;
            float v = (float)py / res;
            Color cloth = SampleWeave(u, v);
            float a = absorbedAmount[i];
            if (a > 0.001f)
            {
                float t = Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, a * 3f));
                Color pc = storedColor[i]; pc.a = 1f;
                cloth = Color.Lerp(cloth, pc, t);
            }
            pixels[i] = cloth;
        }
    }

    void SimulateCeramic()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.02f);
        bool active = false;
        SWEParams p;
        p.g = 1.0f; p.st = ceramicGloss * 0.3f; p.advRate = 0.3f;
        p.visc = 2.5f; p.flowX = 1f; p.flowY = 1f;
        p.applySt = true;
        p.evap = Mathf.Max(0f, 1f - 0.005f * dt);
        SimulateShallowWater(surfaceAmount, ceramicVel, p, dt, ref active);
        hasActiveLiquid = active;

        int res = paintTexture.width;
        int total = res * res;
        for (int i = 0; i < total; i++)
        {
            int py = i / res, px = i - py * res;
            Color ceramic = SampleTile((float)px / res, (float)py / res);
            float h = surfaceAmount[i];
            if (h > 0.001f)
            {
                float t = Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, h * 3f));
                ceramic = Color.Lerp(ceramic, storedColor[i], t);
                float wet = h * ceramicGloss;
                ceramic.r = Mathf.Min(1f, ceramic.r + wet);
                ceramic.g = Mathf.Min(1f, ceramic.g + wet);
                ceramic.b = Mathf.Min(1f, ceramic.b + wet);
            }
            pixels[i] = ceramic;
        }
    }

    void PaintWood(int idx, Color color, float coverage)
    {
        if (coverage <= 0f || surfaceAmount == null) return;
        float existing = surfaceAmount[idx];
        float added = coverage;
        if (existing + added > 1f) added = 1f - existing;
        surfaceAmount[idx] = existing + added;
        float totalAmt = surfaceAmount[idx];
        storedColor[idx] = Color.Lerp(storedColor[idx], color, added / Mathf.Max(totalAmt, 0.001f));
    }

    void PaintCeramic(int idx, Color color, float coverage)
    {
        if (coverage <= 0f || surfaceAmount == null) return;
        float existing = surfaceAmount[idx];
        float added = coverage;
        if (existing + added > 1f) added = 1f - existing;
        surfaceAmount[idx] = existing + added;
        float totalAmt = surfaceAmount[idx];
        storedColor[idx] = Color.Lerp(storedColor[idx], color, added / Mathf.Max(totalAmt, 0.001f));
    }

    void PaintCloth(int idx, Color color, float coverage)
    {
        if (coverage <= 0f || absorbedAmount == null) return;
        float existing = absorbedAmount[idx];
        float added = coverage * 1.5f;
        if (existing + added > 2f) added = 2f - existing;
        absorbedAmount[idx] = existing + added;
        float totalAmt = absorbedAmount[idx];
        storedColor[idx] = Color.Lerp(storedColor[idx], color, added / Mathf.Max(totalAmt, 0.001f));
    }

    static float Hash21(int a, int b)
    {
        uint h = (uint)(a * 127 ^ b * 311);
        h = h * 1103515245 + 12345;
        return (h & 0x7fffffff) / (float)0x7fffffff;
    }

    void BlendPixel(int idx, Color color, float t)
    {
        if (materialType == PanelMaterial.Wood) { PaintWood(idx, color, t); return; }
        if (materialType == PanelMaterial.Cloth) { PaintCloth(idx, color, t); return; }
        if (materialType == PanelMaterial.Ceramic) { PaintCeramic(idx, color, t); return; }
    }

    public void PaintDot(Vector3 worldPos, Color color, float radiusMul = 1f)
    {
        if (pixels == null) return;
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float u = local.x / panelSize.x + 0.5f;
        float v = local.z / panelSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;
        int res = paintTexture.width;
        int cx = Mathf.RoundToInt(u * res);
        int cy = Mathf.RoundToInt(v * res);
        int r = Mathf.RoundToInt(splatPixelRadius * radiusMul);
        if (r < 1) r = 1;
        BlendCircle(cx, cy, r, color);
        textureDirty = true;
    }

    public void PaintLine(Vector3 worldPosA, Vector3 worldPosB, Color color, float radiusMul = 1f)
    {
        if (pixels == null) return;
        Vector3 localA = transform.InverseTransformPoint(worldPosA);
        Vector3 localB = transform.InverseTransformPoint(worldPosB);
        float u1 = localA.x / panelSize.x + 0.5f;
        float v1 = localA.z / panelSize.y + 0.5f;
        float u2 = localB.x / panelSize.x + 0.5f;
        float v2 = localB.z / panelSize.y + 0.5f;
        if ((u1 < 0f || u1 > 1f || v1 < 0f || v1 > 1f) &&
            (u2 < 0f || u2 > 1f || v2 < 0f || v2 > 1f)) return;
        int res = paintTexture.width;
        int x1 = Mathf.RoundToInt(u1 * res);
        int y1 = Mathf.RoundToInt(v1 * res);
        int x2 = Mathf.RoundToInt(u2 * res);
        int y2 = Mathf.RoundToInt(v2 * res);
        int r = Mathf.RoundToInt(splatPixelRadius * radiusMul);
        if (r < 1) r = 1;

        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int steps = Mathf.Max(dx, dy);
        if (steps == 0) { BlendCircle(x1, y1, r, color); textureDirty = true; return; }

        int stepSize = Mathf.Max(1, r / 2);
        for (int i = 0; i <= steps; i += stepSize)
        {
            float t = (float)i / steps;
            int px = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
            int py = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
            if (px >= 0 && px < res && py >= 0 && py < res)
                BlendCircle(px, py, r, color);
        }
        BlendCircle(x2, y2, r, color);
        textureDirty = true;
    }

    private void BlendCircle(int cx, int cy, int r, Color color)
    {
        int res = paintTexture.width;
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(res - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(res - 1, cy + r);
        float r2 = r * r;
        for (int py = minY; py <= maxY; py++)
        {
            int row = py * res;
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx, dy = py - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 <= r2)
                {
                    float t = (1f - d2 / r2) * splatOpacity;
                    if (t > 1f) t = 1f;
                    BlendPixel(row + px, color, t);
                }
            }
        }
    }

    public void DrawSplat(Vector3 worldPos, Color color)
    {
        DrawSplat(worldPos, color, Vector3.zero);
    }

    public void DrawSplat(Vector3 worldPos, Color color, Vector3 velocity)
    {
        if (pixels == null) return;

        if (materialType == PanelMaterial.Wood)
        {
            DrawWoodSplat(worldPos, color, velocity);
            return;
        }
        if (materialType == PanelMaterial.Cloth)
        {
            DrawClothSplat(worldPos, color, velocity);
            return;
        }
        if (materialType == PanelMaterial.Ceramic)
        {
            DrawCeramicSplat(worldPos, color, velocity);
            return;
        }
    }

    void DrawWoodSplat(Vector3 worldPos, Color color, Vector3 velocity)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float u = local.x / panelSize.x + 0.5f;
        float v = local.z / panelSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;

        int res = paintTexture.width;
        int cx = Mathf.RoundToInt(u * res);
        int cy = Mathf.RoundToInt(v * res);
        int r = Mathf.RoundToInt(splatPixelRadius);

        Vector3 localV = transform.InverseTransformDirection(velocity);
        float speed = localV.magnitude;

        float sizeMult = 1f + speed * 0.06f;
        if (sizeMult > 1.3f) sizeMult = 1.3f;

        float baseR = r * sizeMult;

        int minX = Mathf.Max(0, cx - Mathf.CeilToInt(baseR) - 2);
        int maxX = Mathf.Min(res - 1, cx + Mathf.CeilToInt(baseR) + 2);
        int minY = Mathf.Max(0, cy - Mathf.CeilToInt(baseR) - 2);
        int maxY = Mathf.Min(res - 1, cy + Mathf.CeilToInt(baseR) + 2);

        for (int py = minY; py <= maxY; py++)
        {
            int row = py * res;
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = dist / baseR;

                if (norm < 1.2f)
                {
                    float coverage;
                    if (norm < 1f)
                    {
                        coverage = (1f - norm * norm) * 0.95f;
                        float angle = Mathf.Atan2(dy, dx);
                        float edgeNoise = 1f + 0.3f * Mathf.Sin(angle * 5f + Hash21(px, py) * 3f);
                        if (norm * edgeNoise > 1f) coverage *= 0.5f;
                    }
                    else
                    {
                        float h = Hash21(px ^ cy * 7, py ^ cx * 13);
                        if (h < 0.15f)
                            coverage = 0.7f;
                        else coverage = 0f;
                    }

                    if (coverage > 0f)
                    {
                        PaintWood(row + px, color, coverage);
                        if (speed > 0.5f && woodVel != null && dist > 0.5f)
                        {
                            float impactMag = Mathf.Min(speed * 0.15f, 3f);
                            float velMag = impactMag * (1f - norm) * coverage;
                            woodVel[row + px] += new Vector2(dx / dist * velMag, dy / dist * velMag);
                        }
                    }
                }
            }
        }

        textureDirty = true;
    }

    void DrawClothSplat(Vector3 worldPos, Color color, Vector3 velocity)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float u = local.x / panelSize.x + 0.5f;
        float v = local.z / panelSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;

        int res = paintTexture.width;
        int cx = Mathf.RoundToInt(u * res);
        int cy = Mathf.RoundToInt(v * res);
        int r = Mathf.RoundToInt(splatPixelRadius);

        Vector3 localV = transform.InverseTransformDirection(velocity);
        float speed = localV.magnitude;

        float sizeMult = 1f + speed * 0.1f;
        if (sizeMult > 1.8f) sizeMult = 1.8f;
        float baseR = r * sizeMult;

        // Visible blot on fabric
        int bb = Mathf.CeilToInt(baseR * 1.5f) + 2;
        int minX = Mathf.Max(0, cx - bb);
        int maxX = Mathf.Min(res - 1, cx + bb);
        int minY = Mathf.Max(0, cy - bb);
        int maxY = Mathf.Min(res - 1, cy + bb);

        for (int py = minY; py <= maxY; py++)
        {
            int row = py * res;
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = dist / baseR;

                if (norm < 1.2f)
                {
                    float coverage;
                    if (norm < 1f)
                        coverage = (1f - norm * norm) * 0.95f;
                    else
                        coverage = Mathf.Max(0f, (1.2f - norm) / 0.2f) * 0.3f;

                    if (coverage > 0f)
                    {
                        PaintCloth(row + px, color, coverage);
                        if (speed > 1f && norm > 0.4f && norm < 1f)
                        {
                            float splash = coverage * Mathf.Min(speed * 0.08f, 0.6f);
                            PaintCloth(row + px, color, splash);
                        }
                    }
                }
            }
        }

        textureDirty = true;
    }

    void DrawCeramicSplat(Vector3 worldPos, Color color, Vector3 velocity)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float u = local.x / panelSize.x + 0.5f;
        float v = local.z / panelSize.y + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;

        int res = paintTexture.width;
        int cx = Mathf.RoundToInt(u * res);
        int cy = Mathf.RoundToInt(v * res);
        int r = Mathf.RoundToInt(splatPixelRadius);

        Vector3 localV = transform.InverseTransformDirection(velocity);
        float speed = localV.magnitude;

        float sizeMult = 1f + speed * 0.1f;
        if (sizeMult > 1.8f) sizeMult = 1.8f;
        float baseR = r * sizeMult;

        int bb = Mathf.CeilToInt(baseR * 1.5f) + 2;
        int minX = Mathf.Max(0, cx - bb);
        int maxX = Mathf.Min(res - 1, cx + bb);
        int minY = Mathf.Max(0, cy - bb);
        int maxY = Mathf.Min(res - 1, cy + bb);

        for (int py = minY; py <= maxY; py++)
        {
            int row = py * res;
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float norm = dist / baseR;

                if (norm < 1.2f)
                {
                    float coverage;
                    if (norm < 1f)
                        coverage = (1f - norm * norm) * 0.95f;
                    else
                        coverage = Mathf.Max(0f, (1.2f - norm) / 0.2f) * 0.3f;

                    if (coverage > 0f)
                    {
                        PaintCeramic(row + px, color, coverage);
                        if (speed > 0.5f && ceramicVel != null && dist > 0.5f)
                        {
                            float impactMag = Mathf.Min(speed * 0.2f, 4f);
                            float velMag = impactMag * (1f - norm) * coverage;
                            ceramicVel[row + px] += new Vector2(dx / dist * velMag, dy / dist * velMag);
                        }
                    }
                }
            }
        }

        textureDirty = true;
    }
}
