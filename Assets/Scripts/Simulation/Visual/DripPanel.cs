using UnityEngine;

public class DripPanel : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(8f, 8f);

    [Header("Paint")]
    public int textureResolution = 256;
    public float splatPixelRadius = 20f;
    public float splatOpacity = 0.85f;
    public Color backgroundColor = Color.black;

    private Texture2D paintTexture;
    private Material panelMaterial;
    private bool textureDirty;
    private Color[] pixels;

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

    public void DrawSplat(Vector3 worldPos, Color color)
    {
        DrawSplat(worldPos, color, Vector3.zero);
    }

    void DrawEllipse(int cx, int cy, float rx, float ry, float cosA, float sinA, Color color, float opacity)
    {
        int res = paintTexture.width;
        int bb = Mathf.CeilToInt(Mathf.Max(rx, ry));
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
                float d2 = (ex * ex) / rx2 + (ey * ey) / ry2;

                if (d2 <= 1f)
                {
                    float t = (1f - d2) * opacity;
                    if (t > 1f) t = 1f;
                    else if (t < 0f) t = 0f;
                    int idx = row + px;
                    Color c = pixels[idx];
                    pixels[idx] = new Color(
                        c.r + (color.r - c.r) * t,
                        c.g + (color.g - c.g) * t,
                        c.b + (color.b - c.b) * t,
                        1f);
                }
            }
        }
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

        float stretch = 1f + horizSpeed / Mathf.Max(vertSpeed, 0.1f) * 3f;
        if (stretch > 6f) stretch = 6f;

        float sizeMult = 1f + speed * 0.2f;
        if (sizeMult > 3f) sizeMult = 3f;

        float baseR = r * sizeMult;
        float rx = baseR * stretch;
        float ry = baseR;

        Vector2 velDir2 = new Vector2(localV.x, localV.z).normalized;
        if (horizSpeed < 0.01f) velDir2 = Vector2.up;

        float cosA = velDir2.x, sinA = velDir2.y;

        // Main ellipse
        DrawEllipse(cx, cy, rx, ry, cosA, sinA, color, splatOpacity);

        // Splatter: small dots at leading edge
        if (horizSpeed > 0.3f)
        {
            int dotCount = Mathf.RoundToInt(3 + speed * 1.5f);
            if (dotCount > 12) dotCount = 12;
            for (int i = 0; i < dotCount; i++)
            {
                float t = Random.Range(0.18f, 0.35f);
                float spread = Random.Range(-0.3f, 0.3f);
                float dx = velDir2.x * (rx * t) + velDir2.y * (ry * spread);
                float dy = velDir2.y * (rx * t) - velDir2.x * (ry * spread);
                int sx = Mathf.RoundToInt(cx + dx);
                int sy = Mathf.RoundToInt(cy + dy);
                if (sx >= 0 && sx < res && sy >= 0 && sy < res)
                {
                    float dotR = r * Random.Range(0.15f, 0.3f) * sizeMult;
                    DrawEllipse(sx, sy, dotR, dotR * 0.7f, cosA, sinA, color, splatOpacity * 0.7f);
                }
            }
        }

        textureDirty = true;
    }
}
