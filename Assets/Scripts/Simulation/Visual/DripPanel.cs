using UnityEngine;

public class DripPanel : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(8f, 8f);

    [Header("Paint")]
    public int textureResolution = 256;
    public float splatPixelRadius = 10f;
    public float splatOpacity = 0.85f;
    public Color backgroundColor = Color.black;

    private Texture2D paintTexture;
    private Material panelMaterial;
    private bool textureDirty;
    private Color[] pixels;
    private int prevHitX = -1, prevHitY = -1;

    void Start()
    {
        BuildPanel();
    }

    void Update()
    {
        if (textureDirty)
        {
            paintTexture.SetPixels(pixels);
            paintTexture.Apply(false, false);
            textureDirty = false;
        }
    }

    void BuildPanel()
    {
        int res = Mathf.Clamp(textureResolution, 64, 1024);
        paintTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        paintTexture.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color[res * res];
        Color bg = backgroundColor;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
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
    }

    static float Hash21(int a, int b)
    {
        uint h = (uint)(a * 127 ^ b * 311);
        h = h * 1103515245 + 12345;
        return (h & 0x7fffffff) / (float)0x7fffffff;
    }

    void BlendPixel(int idx, Color color, float t)
    {
        if (t <= 0f) return;
        if (t > 1f) t = 1f;
        Color c = pixels[idx];
        pixels[idx] = new Color(
            c.r + (color.r - c.r) * t,
            c.g + (color.g - c.g) * t,
            c.b + (color.b - c.b) * t,
            1f);
    }

    float EllipseDist(float ex, float ey, float rx2, float ry2)
    {
        return (ex * ex) / rx2 + (ey * ey) / ry2;
    }

    void DrawShape(int cx, int cy, float rx, float ry, float cosA, float sinA, Color color, float opacity, int shapeSeed)
    {
        int res = paintTexture.width;
        int bb = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 2;
        int minX = Mathf.Max(0, cx - bb);
        int maxX = Mathf.Min(res - 1, cx + bb);
        int minY = Mathf.Max(0, cy - bb);
        int maxY = Mathf.Min(res - 1, cy + bb);
        float rx2 = rx * rx;
        float ry2 = ry * ry;

        for (int py = minY; py <= maxY; py++)
        {
            int row = py * res;
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float ex = cosA * dx + sinA * dy;
                float ey = -sinA * dx + cosA * dy;
                float d2 = EllipseDist(ex, ey, rx2, ry2);

                float coverage = 0f;
                int seed = shapeSeed ^ (px * 631 ^ py * 977);

                if (d2 <= 1f)
                {
                    coverage = (1f - d2) * opacity;

                    // shape-specific edge distortion
                    float angle = Mathf.Atan2(ey, ex);
                    float h2 = Hash21(px, py);

                    switch (shapeSeed % 5)
                    {
                        case 0: // circle - smooth, no distortion
                            break;

                        case 1: // oval - already handled by rx/ry ellipse
                            break;

                        case 2: // blob - wobbly edge noise
                        {
                            float noise = 1f + 0.25f * Mathf.Sin(angle * 5f + h2 * 6.28f)
                                           + 0.15f * Mathf.Sin(angle * 13f + 1.7f);
                            float edge = EllipseDist(ex * noise, ey * noise, rx2, ry2);
                            if (edge > 1f) coverage = 0f;
                            else coverage = (1f - edge) * opacity;
                            break;
                        }

                        case 3: // starburst - spikes
                        {
                            float spikes = 1f + 0.5f * Mathf.Max(0f, Mathf.Cos(angle * 7f + h2 * 0.5f))
                                          + 0.2f * Mathf.Max(0f, Mathf.Cos(angle * 13f + 0.9f));
                            float edge = EllipseDist(ex * spikes, ey * spikes, rx2, ry2);
                            if (edge > 1f) coverage = 0f;
                            else coverage = (1f - edge) * opacity * 0.85f;
                            break;
                        }

                        case 4: // splat - irregular, like paint splash
                        {
                            float jitter = 1f + 0.2f * (Hash21(seed, 0) - 0.5f)
                                           + 0.15f * Mathf.Sin(angle * 11f + h2 * 3f);
                            float edge = EllipseDist(ex * jitter, ey * jitter, rx2, ry2);
                            if (edge > 1f) coverage = 0f;
                            else coverage = (1f - edge) * opacity;
                            break;
                        }
                    }
                }

                // Random speckles near the perimeter (spatter)
                if (d2 > 0.6f && d2 < 1.8f && coverage < 0.01f)
                {
                    float h = Hash21(seed, 99);
                    if (h < 0.08f)
                    {
                        coverage = opacity * (0.2f + 0.3f * h);
                    }
                }

                if (coverage > 0f)
                    BlendPixel(row + px, color, coverage);
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
        float horizSpeed = new Vector2(localV.x, localV.z).magnitude;
        float vertSpeed = Mathf.Abs(localV.y);

        float stretch = 1f + horizSpeed / Mathf.Max(vertSpeed, 0.1f) * 2f;
        if (stretch > 4f) stretch = 4f;

        float sizeMult = 1f + speed * 0.12f;
        if (sizeMult > 1.8f) sizeMult = 1.8f;

        float baseR = r * sizeMult;
        float rx = baseR * stretch;
        float ry = baseR;

        Vector2 velDir2 = new Vector2(localV.x, localV.z).normalized;
        if (horizSpeed < 0.01f) velDir2 = Vector2.up;

        float cosA = velDir2.x, sinA = velDir2.y;

        // Draw stroke trail from previous hit — forms continuous liquid line
        if (prevHitX >= 0 && prevHitY >= 0)
        {
            int dx = cx - prevHitX, dy = cy - prevHitY;
            int dist = Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy));
            if (dist > 1 && dist < 120)
            {
                // Ellipse orientated along the path direction
                Vector2 pathDir = new Vector2(dx, dy).normalized;
                float pCosA = pathDir.x, pSinA = pathDir.y;

                float trailLen = Mathf.Min(1f, dist / 12f);
                float trailRx = r * (1f + trailLen * 2f);
                float trailRy = r * 0.5f;

                int steps = Mathf.Min(dist / 2 + 1, 30);
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + 1f) / (steps + 1f);
                    int sx = Mathf.RoundToInt(prevHitX + dx * t);
                    int sy = Mathf.RoundToInt(prevHitY + dy * t);
                    float fade = 1f - t * 0.4f;
                    DrawShape(sx, sy, trailRx * fade, trailRy * fade, pCosA, pSinA, color, splatOpacity * fade, 1);
                }
            }
        }
        prevHitX = cx; prevHitY = cy;

        // Main splat (small — the trail makes the visual)
        DrawShape(cx, cy, r * 0.5f, r * 0.5f, 1, 0, color, splatOpacity, 0);

        textureDirty = true;
    }
}
