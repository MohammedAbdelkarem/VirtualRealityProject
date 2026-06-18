using UnityEngine;

public class RopePbdSolver
{
    private Vector3[] currentPositions;
    private Vector3[] previousPositions;
    private float segmentLength;
    private bool isInitialized;

    public bool IsInitialized => isInitialized;
    public Vector3[] CurrentPositions => currentPositions;

    public void Initialize(
        int pointCount,
        float ropeLength,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        int safePointCount =
            Mathf.Max(2, pointCount);

        currentPositions =
            new Vector3[safePointCount];

        previousPositions =
            new Vector3[safePointCount];

        segmentLength =
            RopeLengthUtility.CalculateSegmentLength(
                Mathf.Max(0.001f, ropeLength),
                safePointCount
            );

        Vector3 direction =
            endPoint - startPoint;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction =
                Vector3.down;
        }
        else
        {
            direction.Normalize();
        }

        for (int i = 0; i < safePointCount; i++)
        {
            Vector3 point =
                startPoint +
                direction *
                segmentLength *
                i;

            currentPositions[i] =
                point;

            previousPositions[i] =
                point;
        }

        ApplyFixedEndpoints(
            startPoint,
            endPoint
        );

        isInitialized =
            true;
    }

    public void SetSegmentLength(float newSegmentLength)
    {
        segmentLength =
            Mathf.Max(
                0.001f,
                newSegmentLength
            );
    }

    public void Simulate(
        float deltaTime,
        int constraintIterations,
        float gravity,
        float gravityMultiplier,
        float verletDamping,
        Vector3 startPoint,
        Vector3 endPoint,
        GroundCollisionSettings groundCollisionSettings)
    {
        if (!isInitialized ||
            currentPositions == null ||
            previousPositions == null)
        {
            return;
        }

        ApplyVerlet(
            deltaTime,
            gravity,
            gravityMultiplier,
            verletDamping
        );

        ApplyFixedEndpoints(
            startPoint,
            endPoint
        );

        int safeIterations =
            Mathf.Max(
                1,
                constraintIterations
            );

        for (int iteration = 0; iteration < safeIterations; iteration++)
        {
            ApplyDistanceConstraints(
                startPoint,
                endPoint
            );

            ApplyGroundConstraintsToRope(
                groundCollisionSettings
            );
        }

        ApplyFixedEndpoints(
            startPoint,
            endPoint
        );
    }

    public void Simulate(
        float deltaTime,
        int constraintIterations,
        float gravity,
        float gravityMultiplier,
        float verletDamping,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        Simulate(
            deltaTime,
            constraintIterations,
            gravity,
            gravityMultiplier,
            verletDamping,
            startPoint,
            endPoint,
            null
        );
    }

    private void ApplyVerlet(
        float deltaTime,
        float gravity,
        float gravityMultiplier,
        float verletDamping)
    {
        Vector3 gravityAcceleration =
            Vector3.down *
            gravity *
            gravityMultiplier;

        for (int i = 1; i < currentPositions.Length - 1; i++)
        {
            Vector3 current =
                currentPositions[i];

            Vector3 previous =
                previousPositions[i];

            Vector3 velocity =
                (current - previous) *
                verletDamping;

            previousPositions[i] =
                current;

            currentPositions[i] =
                current +
                velocity +
                gravityAcceleration *
                deltaTime *
                deltaTime;
        }
    }

    private void ApplyDistanceConstraints(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        ApplyFixedEndpoints(
            startPoint,
            endPoint
        );

        for (int i = 0; i < currentPositions.Length - 1; i++)
        {
            Vector3 pointA =
                currentPositions[i];

            Vector3 pointB =
                currentPositions[i + 1];

            Vector3 delta =
                pointB - pointA;

            float currentDistance =
                delta.magnitude;

            if (currentDistance <= 0.0001f)
            {
                continue;
            }

            float difference =
                (currentDistance - segmentLength) /
                currentDistance;

            Vector3 correction =
                delta * difference;

            bool pointAIsFixed =
                i == 0;

            bool pointBIsFixed =
                i + 1 == currentPositions.Length - 1;

            if (pointAIsFixed && pointBIsFixed)
            {
                continue;
            }

            if (pointAIsFixed)
            {
                currentPositions[i + 1] -=
                    correction;
            }
            else if (pointBIsFixed)
            {
                currentPositions[i] +=
                    correction;
            }
            else
            {
                currentPositions[i] +=
                    correction * 0.5f;

                currentPositions[i + 1] -=
                    correction * 0.5f;
            }
        }

        ApplyFixedEndpoints(
            startPoint,
            endPoint
        );
    }

    private void ApplyGroundConstraintsToRope(
        GroundCollisionSettings settings)
    {
        if (settings == null ||
            !settings.EnableGroundCollision)
        {
            return;
        }

        float minimumY =
            settings.GroundHeight +
            settings.ContactSkin +
            settings.RopeGroundRadius;

        for (int i = 1; i < currentPositions.Length - 1; i++)
        {
            if (currentPositions[i].y >= minimumY)
            {
                continue;
            }

            Vector3 current =
                currentPositions[i];

            Vector3 previous =
                previousPositions[i];

            Vector3 velocity =
                current - previous;

            current.y =
                minimumY;

            if (velocity.y < 0.0f)
            {
                velocity.y = 0.0f;
            }

            velocity.x *=
                settings.RopeGroundFriction;

            velocity.z *=
                settings.RopeGroundFriction;

            currentPositions[i] =
                current;

            previousPositions[i] =
                current - velocity;
        }
    }

    private void ApplyFixedEndpoints(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (currentPositions == null ||
            previousPositions == null ||
            currentPositions.Length < 2)
        {
            return;
        }

        int lastIndex =
            currentPositions.Length - 1;

        currentPositions[0] =
            startPoint;

        currentPositions[lastIndex] =
            endPoint;

        previousPositions[0] =
            startPoint;

        previousPositions[lastIndex] =
            endPoint;
    }
}