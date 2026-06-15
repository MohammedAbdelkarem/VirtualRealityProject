using UnityEngine;

[System.Serializable]
public class RopePbdSettings
{
    [Header("Advanced Rope - Verlet / PBD")]
    [SerializeField] private int ropePointCount = 28;
    [SerializeField] private int constraintIterations = 10;
    [SerializeField] private float ropeGravityMultiplier = 0.18f;
    [SerializeField] private float ropeVerletDamping = 0.992f;

    public int RopePointCount => Mathf.Max(2, ropePointCount);
    public int ConstraintIterations => constraintIterations;
    public float RopeGravityMultiplier => ropeGravityMultiplier;
    public float RopeVerletDamping => ropeVerletDamping;
}