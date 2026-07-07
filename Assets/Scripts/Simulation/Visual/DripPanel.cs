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

        if (textureDirty)
        {
            if (materialType == PanelMaterial.Wood)
                SimulateWood();
            else if (materialType == PanelMaterial.Cloth)
                SimulateCloth();
            else if (materialType == PanelMaterial.Ceramic)
                SimulateCeramic();

            paintTexture.SetPixels(pixels);
            paintTexture.Apply(false, false);
            textureDirty = false;
        }
    }

    void SimulateWood()
    {
        int res = paintTexture.width;
        int total = res * res;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        for (int i = 0; i < total; i++)
        {
            int py = i / res;
            int px = i - py * res;

            if (surfaceAmount[i] > 0.001f)
            {
                float transfer = surfaceAmount[i] * 0.8f * dt;
                absorbedAmount[i] = Mathf.Min(1f, absorbedAmount[i] + transfer);
                surfaceAmount[i] -= transfer;
                if (surfaceAmount[i] < 0.001f) surfaceAmount[i] = 0f;
            }

            Color wood = SampleWoodGrain((float)px / res, (float)py / res);

            if (absorbedAmount[i] > 0.005f)
            {
                float a = Mathf.Min(1f, absorbedAmount[i] * 1.5f);
                Color stain = Color.Lerp(storedColor[i], Color.black, 0.15f);
                wood = Color.Lerp(wood, stain, a);
                float darken = 1f - a * 0.15f;
                wood.r *= darken; wood.g *= darken; wood.b *= darken;
            }

            if (surfaceAmount[i] > 0.001f)
            {
                float t = Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, surfaceAmount[i] * 2f));
                wood = Color.Lerp(wood, storedColor[i], t);
                float wet = surfaceAmount[i] * wetSheen;
                wood.r = Mathf.Min(1f, wood.r + wet);
                wood.g = Mathf.Min(1f, wood.g + wet);
                wood.b = Mathf.Min(1f, wood.b + wet);
            }

            pixels[i] = wood;
        }
    }

    void SimulateCloth()
    {
        int res = paintTexture.width;
        int total = res * res;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        // Wicking: spread dye to neighbors
        float[] next = new float[total];
        System.Array.Copy(absorbedAmount, next, total);

        float spread = wickingRate * dt;
        for (int i = 0; i < total; i++)
        {
            if (absorbedAmount[i] <= 0.002f) continue;
            int py = i / res;
            int px = i - py * res;

            float send = spread * absorbedAmount[i];
            if (px > 0) next[i - 1] = Mathf.Min(2f, next[i - 1] + send);
            if (px < res - 1) next[i + 1] = Mathf.Min(2f, next[i + 1] + send);
            if (py > 0) next[i - res] = Mathf.Min(2f, next[i - res] + send);
            if (py < res - 1) next[i + res] = Mathf.Min(2f, next[i + res] + send);
        }
        System.Array.Copy(next, absorbedAmount, total);

        // Composite final pixels
        for (int i = 0; i < total; i++)
        {
            int py = i / res;
            int px = i - py * res;
            float u = (float)px / res;
            float v = (float)py / res;
            Color cloth = SampleWeave(u, v);

            if (absorbedAmount[i] > 0.001f)
            {
                float t = Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, absorbedAmount[i] * 3f));
                Color paintColor = storedColor[i];
                paintColor.a = 1f;
                cloth = Color.Lerp(cloth, paintColor, t);
            }

            pixels[i] = cloth;
        }
    }

    void SimulateCeramic()
    {
        int res = paintTexture.width;
        int total = res * res;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        for (int i = 0; i < total; i++)
        {
            int py = i / res;
            int px = i - py * res;
            float u = (float)px / res;
            float v = (float)py / res;

            Color ceramic = SampleTile(u, v);

            if (surfaceAmount[i] > 0.001f)
            {
                float t = Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, surfaceAmount[i] * 3f));
                ceramic = Color.Lerp(ceramic, storedColor[i], t);
                float wet = surfaceAmount[i] * ceramicGloss;
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
                        PaintWood(row + px, color, coverage);
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
                        PaintCloth(row + px, color, coverage);
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
                        PaintCeramic(row + px, color, coverage);
                }
            }
        }

        textureDirty = true;
    }
}
