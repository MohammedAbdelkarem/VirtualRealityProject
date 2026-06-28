using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

[ExecuteAlways]
public class SimulationUIController : MonoBehaviour
{
    public SPHPaintSimulation sim;
    public AdvancedBucketRopeSimulation ropeSim;

    void Awake()
    {
        Transform existing = transform.Find("SimCanvas");
        if (existing != null) DestroyImmediate(existing.gameObject);
        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("SimCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("SliderPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform pRt = panelGO.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(1, 1);
        pRt.anchorMax = new Vector2(1, 1);
        pRt.pivot = new Vector2(1, 1);
        pRt.sizeDelta = new Vector2(180, 240);
        pRt.anchoredPosition = new Vector2(-100, -150);

        var defs = new (string label, float min, float max, float start, System.Action<float> onSet)[]
        {
            ("Gravity", 1f, 20f, 9.81f, v => { var s = FindFirstObjectByType<SPHPaintSimulation>(); var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) r.SetGravity(v); if (s != null) s.gravityAccel = -v; }),
            ("Phi Speed", 0f, 8f, 2f, v => { var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) SetPhiVel(r, v); }),
            ("Theta \u00b0", 10f, 80f, 45f, v => { var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) SetTheta(r, v); }),
            ("Bucket Mass", 0.1f, 5f, 1f, v => { var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) r.SetBucketMass(v); }),
            ("Damping", 0f, 0.5f, 0.01f, v => { var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) r.SetDampingPerSecond(v); }),
            ("Rope Length", 0.5f, 5f, 2.2f, v => { var r = FindFirstObjectByType<AdvancedBucketRopeSimulation>(); if (r != null) r.SetRopeLength(v); }),
            ("Drain Rate", 0f, 3f, 1.5f, v => { var s = FindFirstObjectByType<SPHPaintSimulation>(); if (s != null) s.drainRate = v; }),
        };

        AddTitle(pRt, "Controls");
        float y = -30;
        foreach (var d in defs)
        {
            AddSliderRow(pRt, d.label, d.min, d.max, d.start, d.onSet, y);
            y -= 28;
        }
    }

    void AddTitle(RectTransform parent, string text)
    {
        GameObject go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 22);
        rt.anchoredPosition = new Vector2(0, -4);
    }

    void AddSliderRow(RectTransform parent, string label, float min, float max, float start, System.Action<float> onSet, float y)
    {
        GameObject lblGo = new GameObject("lbl_" + label);
        lblGo.transform.SetParent(parent, false);
        Text lbl = lblGo.AddComponent<Text>();
        lbl.text = label;
        lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize = 9;
        lbl.color = Color.white;
        lbl.alignment = TextAnchor.MiddleLeft;
        RectTransform lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0, 1);
        lblRt.anchorMax = new Vector2(0, 1);
        lblRt.pivot = new Vector2(0, 1);
        lblRt.sizeDelta = new Vector2(72, 20);
        lblRt.anchoredPosition = new Vector2(6, y);

        GameObject slGO = new GameObject("sld_" + label);
        slGO.transform.SetParent(parent, false);
        Slider sl = slGO.AddComponent<Slider>();
        sl.minValue = min;
        sl.maxValue = max;
        sl.value = start;
        sl.direction = Slider.Direction.LeftToRight;
        sl.wholeNumbers = false;

        Image bgImg = MakeSliderBG(slGO, "Background", new Color(0.2f, 0.2f, 0.2f, 1));
        slGO.AddComponent<Image>();

        Image fillImg = MakeSliderBG(slGO, "Fill", new Color(0.3f, 0.6f, 1f, 1));
        RectTransform fillRt = fillImg.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0);
        fillRt.anchorMax = new Vector2(0, 1);
        fillRt.sizeDelta = new Vector2(0, 0);
        sl.fillRect = fillRt;

        Image handleImg = MakeSliderBG(slGO, "Handle", Color.white);
        RectTransform handleRt = handleImg.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(10, 10);
        sl.handleRect = handleRt;
        sl.targetGraphic = handleImg;

        sl.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(onSet));

        RectTransform slRt = slGO.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0, 1);
        slRt.anchorMax = new Vector2(0, 1);
        slRt.pivot = new Vector2(0, 1);
        slRt.sizeDelta = new Vector2(80, 16);
        slRt.anchoredPosition = new Vector2(88, y + 2);
    }

    Image MakeSliderBG(GameObject parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(0, 0);
        return img;
    }

    void SetPhiVel(AdvancedBucketRopeSimulation ropeSim, float v)
    {
        if (ropeSim == null) return;
        var pendField = typeof(AdvancedBucketRopeSimulation)
            .GetField("pendulumModel", BindingFlags.NonPublic | BindingFlags.Instance);
        if (pendField == null) return;
        var pendModel = pendField.GetValue(ropeSim);
        var stateField = pendModel.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        if (stateField == null) return;
        object state = stateField.GetValue(pendModel);
        var phiVField = state.GetType().GetField("phiVelocity", BindingFlags.Public | BindingFlags.Instance);
        if (phiVField != null) phiVField.SetValue(state, v);
        stateField.SetValue(pendModel, state);
    }

    void SetTheta(AdvancedBucketRopeSimulation ropeSim, float v)
    {
        if (ropeSim == null) return;
        float thetaRad = v * Mathf.Deg2Rad;
        var pendField = typeof(AdvancedBucketRopeSimulation)
            .GetField("pendulumModel", BindingFlags.NonPublic | BindingFlags.Instance);
        if (pendField == null) return;
        var pendModel = pendField.GetValue(ropeSim);
        var stateField = pendModel.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        if (stateField == null) return;
        object state = stateField.GetValue(pendModel);
        var thetaField = state.GetType().GetField("theta", BindingFlags.Public | BindingFlags.Instance);
        if (thetaField != null) thetaField.SetValue(state, thetaRad);
        stateField.SetValue(pendModel, state);
    }
}
