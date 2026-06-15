using UnityEngine;

[System.Serializable]
public class RopeVisualSettings
{
    [Header("Rope Visual Shape")]
    [SerializeField] private float ropeWidth = 0.012f;
    [SerializeField] private Color ropeColor = new Color(0.36f, 0.22f, 0.10f, 1.0f);
    [SerializeField] private int ropeCornerSmoothness = 8;
    [SerializeField] private int ropeCapSmoothness = 8;

    public float RopeWidth => ropeWidth;
    public Color RopeColor => ropeColor;
    public int RopeCornerSmoothness => ropeCornerSmoothness;
    public int RopeCapSmoothness => ropeCapSmoothness;
}