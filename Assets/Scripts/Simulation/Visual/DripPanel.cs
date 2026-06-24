using UnityEngine;

public class DripPanel : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(8f, 8f);

    [Header("Paint")]
    public int textureResolution = 512;
    public float splatPixelRadius = 30f;
    public float splatOpacity = 0.9f;
    public Color backgroundColor = Color.black;

    private Texture2D paintTexture;
    private Material panelMaterial;
    private bool textureDirty;

    void Start()
    {
        BuildPanel();
    }

    void Update()
    {
        if (textureDirty)
        {
            paintTexture.Apply(false, false);
            textureDirty = false;
        }
    }

    void BuildPanel()
    {
        paintTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
        paintTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] clear = new Color[textureResolution * textureResolution];
        for (int i = 0; i < clear.Length; i++) clear[i] = backgroundColor;
        paintTexture.SetPixels(clear);
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
        if (paintTexture == null) return;

        Vector3 local = transform.InverseTransformPoint(worldPos);
        float u = local.x / panelSize.x + 0.5f;
        float v = local.z / panelSize.y + 0.5f;

        if (u < 0f || u > 1f || v < 0f || v > 1f) return;

        int cx = Mathf.RoundToInt(u * textureResolution);
        int cy = Mathf.RoundToInt(v * textureResolution);
        int r = Mathf.RoundToInt(splatPixelRadius);

        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(textureResolution - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(textureResolution - 1, cy + r);

        float r2 = r * r;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist2 = dx * dx + dy * dy;
                if (dist2 <= r2)
                {
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dist2) / r) * splatOpacity;
                    Color existing = paintTexture.GetPixel(px, py);
                    paintTexture.SetPixel(px, py, Color.Lerp(existing, color, alpha));
                }
            }
        }

        textureDirty = true;
    }
}
