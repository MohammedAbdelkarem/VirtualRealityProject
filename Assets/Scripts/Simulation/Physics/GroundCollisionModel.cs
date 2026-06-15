using UnityEngine;

public readonly struct GroundContactInfo
{
    public readonly bool HasContactPoint;
    public readonly Vector3 AverageContactPoint;
    public readonly float LowestPointY;

    public GroundContactInfo(
        bool hasContactPoint,
        Vector3 averageContactPoint,
        float lowestPointY)
    {
        HasContactPoint = hasContactPoint;
        AverageContactPoint = averageContactPoint;
        LowestPointY = lowestPointY;
    }
}

public static class GroundCollisionModel
{
    private const string BucketShellObjectName = "Generated_BucketShell";
    private const float GroundedTolerance = 0.02f;

    public static bool ResolveGroundCollision(
        Transform bucket,
        ref Vector3 linearVelocity,
        ref Vector3 angularVelocity,
        float bucketMass,
        GroundCollisionSettings settings,
        float groundedTime,
        float deltaTime)
    {
        if (bucket == null || settings == null || !settings.EnableGroundCollision)
        {
            return false;
        }

        GroundContactInfo contactInfo =
            CalculateGroundContactInfo(
                bucket,
                settings
            );

        float targetGroundY =
            settings.GroundHeight + settings.ContactSkin;

        bool isGrounded =
            contactInfo.LowestPointY <= targetGroundY + GroundedTolerance;

        bool penetrationWasCorrected =
            CorrectGroundPenetration(
                bucket,
                settings,
                contactInfo
            );

        if (!isGrounded && !penetrationWasCorrected)
        {
            ApplyAirAngularDamping(
                ref angularVelocity,
                settings,
                deltaTime
            );

            return false;
        }

        ApplyGroundLinearResponse(
            ref linearVelocity,
            bucketMass,
            settings,
            deltaTime
        );

        if (settings.EnableAngularImpact && contactInfo.HasContactPoint)
        {
            ApplyImpactAngularImpulse(
                bucket,
                ref linearVelocity,
                ref angularVelocity,
                contactInfo.AverageContactPoint,
                bucketMass,
                settings
            );
        }

        ApplyGroundAngularDamping(
            ref angularVelocity,
            bucketMass,
            settings,
            deltaTime
        );

        ClampAngularVelocity(
            ref angularVelocity,
            settings.MaxAngularSpeed
        );

        if (ShouldApplySideRestAssist(
                groundedTime,
                linearVelocity,
                angularVelocity,
                settings))
        {
            ApplyTaperedSideRestAssist(
                bucket,
                bucketMass,
                settings,
                deltaTime
            );

            GroundContactInfo correctedContactInfo =
                CalculateGroundContactInfo(
                    bucket,
                    settings
                );

            CorrectGroundPenetration(
                bucket,
                settings,
                correctedContactInfo
            );
        }

        return true;
    }

    public static void ApplyAngularVelocity(
        Transform bucket,
        Vector3 angularVelocity,
        float deltaTime)
    {
        if (bucket == null)
        {
            return;
        }

        float angularSpeed =
            angularVelocity.magnitude;

        if (angularSpeed <= 0.0001f)
        {
            return;
        }

        Vector3 axis =
            angularVelocity / angularSpeed;

        float angleDegrees =
            angularSpeed * Mathf.Rad2Deg * deltaTime;

        bucket.rotation =
            Quaternion.AngleAxis(angleDegrees, axis) *
            bucket.rotation;
    }

    private static GroundContactInfo CalculateGroundContactInfo(
        Transform bucket,
        GroundCollisionSettings settings)
    {
        MeshFilter shellMeshFilter =
            FindBucketShellMeshFilter(bucket);

        if (shellMeshFilter == null || shellMeshFilter.sharedMesh == null)
        {
            Vector3 fallbackContact =
                bucket.position +
                Vector3.down * settings.BucketBottomOffset;

            return new GroundContactInfo(
                true,
                fallbackContact,
                fallbackContact.y
            );
        }

        Vector3[] vertices =
            shellMeshFilter.sharedMesh.vertices;

        float lowestY =
            float.PositiveInfinity;

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 worldPoint =
                shellMeshFilter.transform.TransformPoint(
                    vertices[vertexIndex]
                );

            if (worldPoint.y < lowestY)
            {
                lowestY = worldPoint.y;
            }
        }

        if (float.IsPositiveInfinity(lowestY))
        {
            return new GroundContactInfo(
                false,
                bucket.position,
                bucket.position.y
            );
        }

        Vector3 contactSum =
            Vector3.zero;

        int contactCount =
            0;

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 worldPoint =
                shellMeshFilter.transform.TransformPoint(
                    vertices[vertexIndex]
                );

            if (worldPoint.y <= lowestY + settings.ContactPatchTolerance)
            {
                contactSum += worldPoint;
                contactCount++;
            }
        }

        if (contactCount <= 0)
        {
            return new GroundContactInfo(
                false,
                bucket.position,
                lowestY
            );
        }

        return new GroundContactInfo(
            true,
            contactSum / contactCount,
            lowestY
        );
    }

    private static bool CorrectGroundPenetration(
        Transform bucket,
        GroundCollisionSettings settings,
        GroundContactInfo contactInfo)
    {
        float targetGroundY =
            settings.GroundHeight + settings.ContactSkin;

        if (contactInfo.LowestPointY >= targetGroundY)
        {
            return false;
        }

        float penetrationDepth =
            targetGroundY - contactInfo.LowestPointY;

        bucket.position +=
            Vector3.up * penetrationDepth;

        return true;
    }

    private static void ApplyGroundLinearResponse(
        ref Vector3 linearVelocity,
        float bucketMass,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float massRatio =
            CalculateMassRatio(
                bucketMass,
                settings
            );

        float effectiveBounciness =
            settings.Bounciness /
            (1.0f + massRatio * settings.MassBounceResistance);

        if (linearVelocity.y < 0.0f)
        {
            linearVelocity.y =
                -linearVelocity.y * effectiveBounciness;
        }

        float effectiveFriction =
            settings.GroundFrictionPerSecond +
            massRatio * settings.HeavyMassExtraFriction;

        float frictionFactor =
            Mathf.Exp(-effectiveFriction * deltaTime);

        linearVelocity.x *= frictionFactor;
        linearVelocity.z *= frictionFactor;

        if (Mathf.Abs(linearVelocity.y) <= settings.StopVelocityThreshold)
        {
            linearVelocity.y = 0.0f;
        }

        Vector2 horizontalVelocity =
            new Vector2(
                linearVelocity.x,
                linearVelocity.z
            );

        if (horizontalVelocity.magnitude <= settings.StopVelocityThreshold)
        {
            linearVelocity.x = 0.0f;
            linearVelocity.z = 0.0f;
        }
    }

    private static void ApplyImpactAngularImpulse(
        Transform bucket,
        ref Vector3 linearVelocity,
        ref Vector3 angularVelocity,
        Vector3 contactPoint,
        float bucketMass,
        GroundCollisionSettings settings)
    {
        if (linearVelocity.y >= -settings.StopVelocityThreshold)
        {
            return;
        }

        float massRatio =
            CalculateMassRatio(
                bucketMass,
                settings
            );

        float effectiveBounciness =
            settings.Bounciness /
            (1.0f + massRatio * settings.MassBounceResistance);

        float normalImpulseMagnitude =
            Mathf.Abs(linearVelocity.y) *
            Mathf.Max(0.001f, bucketMass) *
            (1.0f + effectiveBounciness);

        Vector3 normalImpulse =
            Vector3.up * normalImpulseMagnitude;

        Vector3 leverArm =
            contactPoint - bucket.position;

        Vector3 torqueImpulse =
            Vector3.Cross(
                leverArm,
                normalImpulse
            );

        float inertia =
            EstimateBucketInertia(
                bucket,
                bucketMass
            );

        angularVelocity +=
            torqueImpulse *
            settings.ImpactAngularFactor /
            Mathf.Max(0.001f, inertia);
    }

    private static void ApplyAirAngularDamping(
        ref Vector3 angularVelocity,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float dampingFactor =
            Mathf.Exp(-settings.AirAngularDampingPerSecond * deltaTime);

        angularVelocity *= dampingFactor;
    }

    private static void ApplyGroundAngularDamping(
        ref Vector3 angularVelocity,
        float bucketMass,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float massRatio =
            CalculateMassRatio(
                bucketMass,
                settings
            );

        float effectiveDamping =
            settings.GroundAngularDampingPerSecond /
            (1.0f + massRatio * settings.MassRotationResistance);

        float dampingFactor =
            Mathf.Exp(-effectiveDamping * deltaTime);

        angularVelocity *= dampingFactor;

        if (angularVelocity.magnitude <= settings.StopVelocityThreshold)
        {
            angularVelocity = Vector3.zero;
        }
    }

    private static bool ShouldApplySideRestAssist(
        float groundedTime,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        GroundCollisionSettings settings)
    {
        if (!settings.EnableSideRestSettling ||
            !settings.ForceBucketAxisHorizontalOnGround)
        {
            return false;
        }

        if (groundedTime < settings.SideRestAssistDelay)
        {
            return false;
        }

        bool linearIsSmall =
            linearVelocity.magnitude <=
            settings.StopVelocityThreshold * 2.0f;

        bool angularIsSmall =
            angularVelocity.magnitude <=
            settings.StopVelocityThreshold * 2.5f;

        return linearIsSmall && angularIsSmall;
    }

    private static void ApplyTaperedSideRestAssist(
        Transform bucket,
        float bucketMass,
        GroundCollisionSettings settings,
        float deltaTime)
    {
        float topRadius;
        float bottomRadius;
        float bucketHeight;

        bool hasGeometry =
            TryGetBucketShellGeometry(
                bucket,
                out topRadius,
                out bottomRadius,
                out bucketHeight
            );

        if (!hasGeometry)
        {
            return;
        }

        Vector3 horizontalAxisDirection =
            Vector3.ProjectOnPlane(
                bucket.up,
                Vector3.up
            );

        if (horizontalAxisDirection.sqrMagnitude <= 0.0001f)
        {
            horizontalAxisDirection =
                Vector3.ProjectOnPlane(
                    bucket.forward,
                    Vector3.up
                );
        }

        if (horizontalAxisDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        horizontalAxisDirection.Normalize();

        float radiusDifference =
            topRadius - bottomRadius;

        float axisVerticalComponent =
            radiusDifference /
            Mathf.Sqrt(
                bucketHeight * bucketHeight +
                radiusDifference * radiusDifference
            );

        axisVerticalComponent =
            Mathf.Clamp(
                axisVerticalComponent,
                -0.5f,
                0.5f
            );

        float horizontalComponent =
            Mathf.Sqrt(
                Mathf.Max(
                    0.0f,
                    1.0f -
                    axisVerticalComponent *
                    axisVerticalComponent
                )
            );

        Vector3 targetBucketAxis =
            horizontalAxisDirection * horizontalComponent +
            Vector3.up * axisVerticalComponent;

        targetBucketAxis.Normalize();

        Vector3 targetRadialDown =
            Vector3.ProjectOnPlane(
                Vector3.down,
                targetBucketAxis
            );

        if (targetRadialDown.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        targetRadialDown.Normalize();

        Vector3 targetRight =
            targetRadialDown;

        Vector3 targetUp =
            targetBucketAxis;

        Vector3 targetForward =
            Vector3.Cross(
                targetRight,
                targetUp
            );

        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        targetForward.Normalize();

        Quaternion targetRotation =
            Quaternion.LookRotation(
                targetForward,
                targetUp
            );

        float massRatio =
            CalculateMassRatio(
                bucketMass,
                settings
            );

        float effectiveSpeed =
            settings.SideRestRotationSpeed /
            (1.0f + massRatio * settings.MassRotationResistance);

        float blend =
            1.0f -
            Mathf.Exp(-effectiveSpeed * deltaTime);

        bucket.rotation =
            Quaternion.Slerp(
                bucket.rotation,
                targetRotation,
                blend
            );
    }

    private static float EstimateBucketInertia(
        Transform bucket,
        float bucketMass)
    {
        Bounds bounds =
            CalculateBucketBounds(bucket);

        float sizeSquared =
            bounds.size.sqrMagnitude;

        return Mathf.Max(
            0.001f,
            bucketMass * sizeSquared / 12.0f
        );
    }

    private static Bounds CalculateBucketBounds(Transform bucket)
    {
        MeshFilter[] meshFilters =
            bucket.GetComponentsInChildren<MeshFilter>();

        bool hasBounds =
            false;

        Bounds combinedBounds =
            new Bounds(bucket.position, Vector3.one);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter =
                meshFilters[i];

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshBounds =
                meshFilter.sharedMesh.bounds;

            Vector3 worldCenter =
                meshFilter.transform.TransformPoint(
                    meshBounds.center
                );

            Vector3 worldSize =
                Vector3.Scale(
                    meshBounds.size,
                    meshFilter.transform.lossyScale
                );

            Bounds worldBounds =
                new Bounds(
                    worldCenter,
                    worldSize
                );

            if (!hasBounds)
            {
                combinedBounds = worldBounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(worldBounds);
            }
        }

        return combinedBounds;
    }

    private static bool TryGetBucketShellGeometry(
        Transform bucket,
        out float topRadius,
        out float bottomRadius,
        out float bucketHeight)
    {
        topRadius = 0.0f;
        bottomRadius = 0.0f;
        bucketHeight = 0.0f;

        MeshFilter shellMeshFilter =
            FindBucketShellMeshFilter(bucket);

        if (shellMeshFilter == null || shellMeshFilter.sharedMesh == null)
        {
            return false;
        }

        Vector3[] vertices =
            shellMeshFilter.sharedMesh.vertices;

        if (vertices == null || vertices.Length == 0)
        {
            return false;
        }

        float minLocalY =
            float.PositiveInfinity;

        float maxLocalY =
            float.NegativeInfinity;

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 bucketLocalPoint =
                bucket.InverseTransformPoint(
                    shellMeshFilter.transform.TransformPoint(
                        vertices[vertexIndex]
                    )
                );

            minLocalY =
                Mathf.Min(
                    minLocalY,
                    bucketLocalPoint.y
                );

            maxLocalY =
                Mathf.Max(
                    maxLocalY,
                    bucketLocalPoint.y
                );
        }

        bucketHeight =
            maxLocalY - minLocalY;

        if (bucketHeight <= 0.001f)
        {
            return false;
        }

        float yTolerance =
            Mathf.Max(
                0.002f,
                bucketHeight * 0.04f
            );

        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 bucketLocalPoint =
                bucket.InverseTransformPoint(
                    shellMeshFilter.transform.TransformPoint(
                        vertices[vertexIndex]
                    )
                );

            float radius =
                new Vector2(
                    bucketLocalPoint.x,
                    bucketLocalPoint.z
                ).magnitude;

            if (Mathf.Abs(bucketLocalPoint.y - maxLocalY) <= yTolerance)
            {
                topRadius =
                    Mathf.Max(
                        topRadius,
                        radius
                    );
            }

            if (Mathf.Abs(bucketLocalPoint.y - minLocalY) <= yTolerance)
            {
                bottomRadius =
                    Mathf.Max(
                        bottomRadius,
                        radius
                    );
            }
        }

        return topRadius > 0.001f &&
               bottomRadius > 0.001f &&
               bucketHeight > 0.001f;
    }

    private static MeshFilter FindBucketShellMeshFilter(Transform bucket)
    {
        MeshFilter[] meshFilters =
            bucket.GetComponentsInChildren<MeshFilter>();

        for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
        {
            MeshFilter meshFilter =
                meshFilters[filterIndex];

            if (meshFilter == null)
            {
                continue;
            }

            if (meshFilter.name == BucketShellObjectName)
            {
                return meshFilter;
            }
        }

        for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
        {
            MeshFilter meshFilter =
                meshFilters[filterIndex];

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (meshFilter.sharedMesh.name.Contains("Bucket"))
            {
                return meshFilter;
            }
        }

        return null;
    }

    private static void ClampAngularVelocity(
        ref Vector3 angularVelocity,
        float maxAngularSpeed)
    {
        if (maxAngularSpeed <= 0.0f)
        {
            angularVelocity = Vector3.zero;
            return;
        }

        if (angularVelocity.magnitude > maxAngularSpeed)
        {
            angularVelocity =
                angularVelocity.normalized *
                maxAngularSpeed;
        }
    }

    private static float CalculateMassRatio(
        float bucketMass,
        GroundCollisionSettings settings)
    {
        float rawRatio =
            Mathf.Max(0.001f, bucketMass) /
            settings.ReferenceMass;

        return Mathf.Clamp(
            rawRatio,
            1.0f,
            settings.MaxMassForCollisionResponse
        );
    }
}