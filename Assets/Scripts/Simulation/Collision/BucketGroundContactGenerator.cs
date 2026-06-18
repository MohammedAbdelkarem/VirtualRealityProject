using System.Collections.Generic;
using UnityEngine;

public static class BucketGroundContactGenerator
{
    public static GroundContactManifold Generate(
        Transform bucket,
        List<Vector3> localProxyPoints,
        GroundCollisionSettings settings)
    {
        GroundContactManifold manifold =
            new GroundContactManifold();

        if (bucket == null ||
            localProxyPoints == null ||
            settings == null ||
            !settings.EnableGroundCollision)
        {
            return manifold;
        }

        float solidSurfaceY =
            settings.GroundHeight + settings.ContactSkin;

        for (int i = 0; i < localProxyPoints.Count; i++)
        {
            Vector3 localPoint =
                localProxyPoints[i];

            Vector3 worldPoint =
                bucket.TransformPoint(localPoint);

            float signedDistance =
                worldPoint.y - solidSurfaceY;

            if (signedDistance > settings.ContactTolerance)
            {
                continue;
            }

            float penetrationDepth =
                Mathf.Max(0.0f, -signedDistance);

            manifold.Add(
                new GroundContact(
                    localPoint,
                    worldPoint,
                    penetrationDepth
                )
            );
        }

        manifold.KeepDeepestContacts(
            settings.MaxContactCount
        );

        return manifold;
    }

    public static bool ResolveKinematicSolidGround(
        Transform bucket,
        GroundCollisionSettings settings)
    {
        if (bucket == null ||
            settings == null ||
            !settings.EnableGroundCollision)
        {
            return false;
        }

        List<Vector3> proxyPoints =
            BucketCollisionProxy.BuildLocalProxyPoints(
                bucket,
                settings
            );

        GroundContactManifold manifold =
            Generate(
                bucket,
                proxyPoints,
                settings
            );

        if (manifold.Count == 0)
        {
            return false;
        }

        float maxPenetration =
            manifold.GetMaxPenetrationDepth();

        if (maxPenetration <= settings.PositionCorrectionSlop)
        {
            return false;
        }

        bucket.position +=
            Vector3.up *
            maxPenetration *
            settings.PositionCorrectionPercent;

        return true;
    }
}