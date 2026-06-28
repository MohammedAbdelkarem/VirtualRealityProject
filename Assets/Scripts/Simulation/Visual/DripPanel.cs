using UnityEngine;
using UnityEngine.Rendering;

public class DripPanel : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(5f, 5f);

    [Header("Paint")]
    public int textureResolution = 256;
    public float splatPixelRadius = 10f;
    public float splatOpacity = 0.85f;
    public Color backgroundColor = Color.black;

    private Texture2D paintTexture;
    private Material panelMaterial;
    private bool textureDirty;
    private Color[] pixels;

    void Start()
    {
        BuildPanel();
        BuildStudioSetup();
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

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int px = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
            int py = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
            if (px >= 0 && px < res && py >= 0 && py < res)
                BlendPixel(py * res + px, color, splatOpacity);
        }
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

        float sizeMult = 1f + speed * 0.08f;
        if (sizeMult > 1.5f) sizeMult = 1.5f;

        float baseR = r * sizeMult;
        float rx = baseR * stretch;
        float ry = baseR;

        Vector2 velDir2 = new Vector2(localV.x, localV.z).normalized;
        if (horizSpeed < 0.01f) velDir2 = Vector2.up;

        float cosA = velDir2.x, sinA = velDir2.y;

        // Pick shape based on speed + randomness
        int shape;
        float roll = Hash21(cx * 7 + 1, cy * 13 + 3);
        if (speed < 0.5f)
            shape = roll < 0.5f ? 0 : 1;
        else if (speed < 1.5f)
            shape = roll < 0.3f ? 0 : (roll < 0.6f ? 1 : 2);
        else
            shape = roll < 0.2f ? 1 : (roll < 0.5f ? 2 : (roll < 0.75f ? 3 : 4));

        // Draw main splat
        DrawShape(cx, cy, rx, ry, cosA, sinA, color, splatOpacity, shape);

        // Cluster shape: overlapping smaller shapes
        if (shape == 4 && speed > 1f)
        {
            int sub = Mathf.RoundToInt(2 + speed * 0.5f);
            if (sub > 5) sub = 5;
            for (int i = 0; i < sub; i++)
            {
                float offA = Hash21(cx + i * 7, cy + i * 13) * 6.28f;
                float offD = Random.Range(0.2f, 0.5f) * baseR;
                int scx = cx + Mathf.RoundToInt(Mathf.Cos(offA) * offD);
                int scy = cy + Mathf.RoundToInt(Mathf.Sin(offA) * offD);
                if (scx >= 0 && scx < res && scy >= 0 && scy < res)
                {
                    float sr = baseR * Random.Range(0.25f, 0.5f);
                    float s = Hash21(scx, scy);
                    int subShape = s < 0.5f ? 0 : 2;
                    DrawShape(scx, scy, sr, sr * 0.8f, cosA, sinA, color, splatOpacity * 0.6f, subShape);
                }
            }
        }

        // Splatter dots at leading edge
        if (horizSpeed > 0.3f)
        {
            int dotCount = Mathf.RoundToInt(2 + speed);
            if (dotCount > 8) dotCount = 8;
            for (int i = 0; i < dotCount; i++)
            {
                float t = Random.Range(0.2f, 0.4f);
                float spread = Random.Range(-0.35f, 0.35f);
                float dx = velDir2.x * (rx * t) + velDir2.y * (ry * spread);
                float dy = velDir2.y * (rx * t) - velDir2.x * (ry * spread);
                int sx = Mathf.RoundToInt(cx + dx);
                int sy = Mathf.RoundToInt(cy + dy);
                if (sx >= 0 && sx < res && sy >= 0 && sy < res)
                {
                    float dotR = r * Random.Range(0.12f, 0.25f) * sizeMult;
                    int dotShape = Hash21(sx, sy) < 0.5f ? 0 : 2;
                    DrawShape(sx, sy, dotR, dotR * 0.7f, cosA, sinA, color, splatOpacity * 0.6f, dotShape);
                }
            }
        }

        textureDirty = true;
    }

    void BuildStudioSetup()
    {
        float margin = 8f;
        float floorY = -0.01f;
        float size = Mathf.Max(panelSize.x, panelSize.y) + margin;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // Floor
        GameObject floor = new GameObject("StudioFloor");
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(0f, floorY, 0f);
        MeshRenderer floorMr = floor.AddComponent<MeshRenderer>();
        MeshFilter floorMf = floor.AddComponent<MeshFilter>();

        Mesh floorMesh = new Mesh();
        floorMesh.name = "StudioFloorMesh";
        Vector3[] fVerts = new Vector3[4];
        Vector2[] fUVs = new Vector2[4];
        int[] fTris = new int[6];
        float hs = size * 0.5f;
        fVerts[0] = new Vector3(-hs, 0, -hs); fUVs[0] = new Vector2(0, 0);
        fVerts[1] = new Vector3(hs, 0, -hs); fUVs[1] = new Vector2(size, 0);
        fVerts[2] = new Vector3(-hs, 0, hs); fUVs[2] = new Vector2(0, size);
        fVerts[3] = new Vector3(hs, 0, hs); fUVs[3] = new Vector2(size, size);
        fTris[0] = 0; fTris[1] = 2; fTris[2] = 1;
        fTris[3] = 1; fTris[4] = 2; fTris[5] = 3;
        floorMesh.SetVertices(fVerts);
        floorMesh.SetUVs(0, fUVs);
        floorMesh.SetTriangles(fTris, 0);
        floorMesh.SetNormals(new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        floorMesh.SetTangents(new Vector4[] {
            new Vector4(1,0,0,1), new Vector4(1,0,0,1),
            new Vector4(1,0,0,1), new Vector4(1,0,0,1)
        });
        floorMesh.RecalculateBounds();
        floorMf.sharedMesh = floorMesh;

        Material floorMat = new Material(litShader);
        floorMat.SetColor("_Color", new Color(0.18f, 0.18f, 0.19f));
        floorMat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.19f));
        floorMat.SetFloat("_Smoothness", 0.15f);
        floorMat.SetFloat("_Metallic", 0.0f);
        floorMat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        floorMr.sharedMaterial = floorMat;
        floorMr.receiveShadows = true;
        floorMr.shadowCastingMode = ShadowCastingMode.On;

        // Grid overlay on floor
        Shader gridShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (gridShader == null) gridShader = Shader.Find("Unlit/Transparent");
        if (gridShader == null) gridShader = litShader;
        Material gridMat = new Material(gridShader);
        gridMat.SetColor("_Color", new Color(1f, 1f, 1f, 0.06f));
        gridMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.06f));
        if (!gridShader.name.Contains("Unlit"))
        {
            gridMat.SetFloat("_Surface", 1f);
            gridMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        gridMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        gridMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        gridMat.SetInt("_ZWrite", 0);
        gridMat.renderQueue = 3000;
        if (gridMat.HasFloat("_Cull")) gridMat.SetFloat("_Cull", 0f);

        GameObject grid = new GameObject("StudioGrid");
        grid.transform.SetParent(floor.transform, false);
        grid.transform.localPosition = new Vector3(0f, 0.001f, 0f);
        MeshRenderer gridMr = grid.AddComponent<MeshRenderer>();
        MeshFilter gridMf = grid.AddComponent<MeshFilter>();
        gridMf.sharedMesh = floorMesh;
        gridMr.sharedMaterial = gridMat;
        gridMr.receiveShadows = false;
        gridMr.shadowCastingMode = ShadowCastingMode.Off;

        // Back wall
        float wallHeight = 6f;
        float wallDepth = -hs;
        GameObject wall = new GameObject("StudioBackWall");
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, wallDepth);
        MeshRenderer wallMr = wall.AddComponent<MeshRenderer>();
        MeshFilter wallMf = wall.AddComponent<MeshFilter>();

        Mesh wallMesh = new Mesh();
        wallMesh.name = "StudioWallMesh";
        Vector3[] wVerts = new Vector3[4];
        Vector2[] wUVs = new Vector2[4];
        int[] wTris = new int[6];
        float ww = hs * 2f;
        wVerts[0] = new Vector3(-ww * 0.5f, -wallHeight * 0.5f, 0); wUVs[0] = new Vector2(0, 0);
        wVerts[1] = new Vector3(ww * 0.5f, -wallHeight * 0.5f, 0); wUVs[1] = new Vector2(1, 0);
        wVerts[2] = new Vector3(-ww * 0.5f, wallHeight * 0.5f, 0); wUVs[2] = new Vector2(0, 1);
        wVerts[3] = new Vector3(ww * 0.5f, wallHeight * 0.5f, 0); wUVs[3] = new Vector2(1, 1);
        wTris[0] = 0; wTris[1] = 2; wTris[2] = 1;
        wTris[3] = 1; wTris[4] = 2; wTris[5] = 3;
        wallMesh.SetVertices(wVerts);
        wallMesh.SetUVs(0, wUVs);
        wallMesh.SetTriangles(wTris, 0);
        wallMesh.SetNormals(new Vector3[] {
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
        });
        wallMesh.SetTangents(new Vector4[] {
            new Vector4(1,0,0,1), new Vector4(1,0,0,1),
            new Vector4(1,0,0,1), new Vector4(1,0,0,1)
        });
        wallMesh.RecalculateBounds();
        wallMf.sharedMesh = wallMesh;

        Material wallMat = new Material(litShader);
        wallMat.SetColor("_Color", new Color(0.85f, 0.85f, 0.87f));
        wallMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.87f));
        wallMat.SetFloat("_Smoothness", 0.1f);
        wallMat.SetFloat("_Metallic", 0.0f);
        wallMr.sharedMaterial = wallMat;
        wallMr.receiveShadows = true;
        wallMr.shadowCastingMode = ShadowCastingMode.On;

        // Side walls (two small wings)
        float sideDepth = 1.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject sw = new GameObject("StudioSideWall_" + side);
            sw.transform.SetParent(transform, false);
            MeshRenderer swMr = sw.AddComponent<MeshRenderer>();
            MeshFilter swMf = sw.AddComponent<MeshFilter>();

            Mesh swMesh = new Mesh();
            swMesh.name = "StudioSideWallMesh";
            Vector3[] sv = new Vector3[4];
            Vector2[] su = new Vector2[4];
            int[] st = new int[6];
            float sx = side * ww * 0.5f;
            sv[0] = new Vector3(0, -wallHeight * 0.5f, -sideDepth); su[0] = new Vector2(0, 0);
            sv[1] = new Vector3(0, -wallHeight * 0.5f, 0); su[1] = new Vector2(1, 0);
            sv[2] = new Vector3(0, wallHeight * 0.5f, -sideDepth); su[2] = new Vector2(0, 1);
            sv[3] = new Vector3(0, wallHeight * 0.5f, 0); su[3] = new Vector2(1, 1);
            st[0] = 0; st[1] = 2; st[2] = 1;
            st[3] = 1; st[4] = 2; st[5] = 3;
            swMesh.SetVertices(sv);
            swMesh.SetUVs(0, su);
            swMesh.SetTriangles(st, 0);
            swMesh.SetNormals(new Vector3[] {
                -Vector3.right * side, -Vector3.right * side,
                -Vector3.right * side, -Vector3.right * side
            });
            swMesh.SetTangents(new Vector4[] {
                new Vector4(0,0,1,1), new Vector4(0,0,1,1),
                new Vector4(0,0,1,1), new Vector4(0,0,1,1)
            });
            swMesh.RecalculateBounds();
            swMf.sharedMesh = swMesh;
            swMr.sharedMaterial = wallMat;
            swMr.receiveShadows = true;
            swMr.shadowCastingMode = ShadowCastingMode.On;

            sw.transform.localPosition = new Vector3(sx, wallHeight * 0.5f, wallDepth + sideDepth * 0.5f);
        }
    }
}
