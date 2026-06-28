using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class SimulationUIController : MonoBehaviour
{
    public SPHPaintSimulation sim;
    public AdvancedBucketRopeSimulation ropeSim;

    private Slider gravitySlider, massSlider, dampingSlider, ropeSlider, phiVelSlider, thetaSlider, drainSlider;

    void Start()
    {
        if (sim == null) sim = FindFirstObjectByType<SPHPaintSimulation>();
        if (ropeSim == null) ropeSim = FindFirstObjectByType<AdvancedBucketRopeSimulation>();

        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("SimulationCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        GameObject panelGO = new GameObject("SliderPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0, 0.5f);
        panelRt.anchorMax = new Vector2(0, 0.5f);
        panelRt.pivot = new Vector2(0, 0.5f);
        panelRt.sizeDelta = new Vector2(200, 320);
        panelRt.anchoredPosition = new Vector2(10, 0);

        // Slider definitions: label, min, max, start value, callback
        var defs = new (string label, float min, float max, float start, System.Action<float> onSet)[]
        {
            ("Gravity", 1f, 20f, 9.81f, v => { if (ropeSim != null) ropeSim.SetGravity(v); }),
            ("Phi Speed", 0f, 8f, 2f, v => SetPhiVel(v)),
            ("Theta °", 10f, 80f, 45f, v => SetTheta(v)),
            ("Bucket Mass", 0.1f, 5f, 1f, v => { if (ropeSim != null) ropeSim.SetBucketMass(v); }),
            ("Damping", 0f, 0.5f, 0.01f, v => { if (ropeSim != null) ropeSim.SetDampingPerSecond(v); }),
            ("Rope Length", 0.5f, 5f, 2.2f, v => { if (ropeSim != null) ropeSim.SetRopeLength(v); }),
            ("Drain Rate", 0f, 3f, 1.5f, v => { sim.drainRate = v; }),
        };

        float yOff = 0;
        float step = 42f;

        // Title
        CreateLabel(panelRt, "Controls", new Vector2(100, 300), 16);

        foreach (var d in defs)
        {
            yOff -= step;
            CreateSlider(panelRt, d.label, d.min, d.max, d.start, d.onSet, new Vector2(100, yOff));
        }
    }

    void CreateLabel(RectTransform parent, string text, Vector2 pos, int fontSize = 14)
    {
        GameObject go = new GameObject(text + "_Label");
        go.transform.SetParent(parent, false);
        Text txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 24);
        rt.anchoredPosition = pos;
    }

    void CreateSlider(RectTransform parent, string label, float min, float max, float start, System.Action<float> onSet, Vector2 pos)
    {
        // Label
        GameObject lblGo = new GameObject(label + "_Label");
        lblGo.transform.SetParent(parent, false);
        Text lbl = lblGo.AddComponent<Text>();
        lbl.text = label;
        lbl.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (lbl.font == null) lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize = 12;
        lbl.color = Color.white;
        lbl.alignment = TextAnchor.MiddleLeft;
        RectTransform lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.sizeDelta = new Vector2(90, 20);
        lblRt.anchoredPosition = pos + new Vector2(-95, 0);

        // Slider
        GameObject slGO = new GameObject(label + "_Slider");
        slGO.transform.SetParent(parent, false);
        Slider sl = slGO.AddComponent<Slider>();
        sl.minValue = min;
        sl.maxValue = max;
        sl.value = start;
        sl.direction = Slider.Direction.LeftToRight;
        sl.wholeNumbers = false;

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(slGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.25f);
        bgRt.anchorMax = new Vector2(1, 0.75f);
        bgRt.sizeDelta = new Vector2(0, 0);

        // Fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(slGO.transform, false);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.6f, 1f, 1f);
        RectTransform fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0.25f);
        fillRt.anchorMax = new Vector2(0, 0.75f);
        fillRt.sizeDelta = new Vector2(0, 0);
        sl.fillRect = fillRt;

        // Handle
        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(slGO.transform, false);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRt = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(16, 16);
        sl.handleRect = handleRt;
        sl.targetGraphic = handleImg;

        sl.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(onSet));

        RectTransform slRt = slGO.GetComponent<RectTransform>();
        slRt.sizeDelta = new Vector2(90, 20);
        slRt.anchoredPosition = pos + new Vector2(95, 0);
    }

    void SetPhiVel(float v)
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

    void SetTheta(float v)
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
