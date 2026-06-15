using UnityEngine;

[System.Serializable]
public class RopeLoadSettings
{
    [Header("Mass / Rope Strength")]
    [SerializeField] private bool useMassEffects = true;
    [SerializeField] private bool ropeCanTear = true;
    [SerializeField] private float ropeStretchStiffness = 1500.0f;
    [SerializeField] private float maxRopeStretch = 0.35f;
    [SerializeField] private float maxRopeTension = 350.0f;
    [SerializeField] private bool useMassBasedDamping = true;
    [SerializeField] private float airDragDamping = 0.45f;

    public bool UseMassEffects => useMassEffects;
    public bool RopeCanTear => ropeCanTear;
    public float RopeStretchStiffness => ropeStretchStiffness;
    public float MaxRopeStretch => maxRopeStretch;
    public float MaxRopeTension => maxRopeTension;
    public bool UseMassBasedDamping => useMassBasedDamping;
    public float AirDragDamping => airDragDamping;
}