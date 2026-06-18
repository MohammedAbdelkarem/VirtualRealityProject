using UnityEngine;

public readonly struct GroundContact
{
    public readonly Vector3 LocalPoint;
    public readonly Vector3 WorldPoint;
    public readonly float PenetrationDepth;

    public GroundContact(
        Vector3 localPoint,
        Vector3 worldPoint,
        float penetrationDepth)
    {
        LocalPoint = localPoint;
        WorldPoint = worldPoint;
        PenetrationDepth = penetrationDepth;
    }
}