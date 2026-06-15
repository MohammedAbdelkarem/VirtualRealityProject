using UnityEngine;

public class RopePbdSolver
{
    private Vector3[] currentPositions;
    private Vector3[] previousPositions;

    private int pointCount;
    private float segmentLength;

    public Vector3[] CurrentPositions => currentPositions;
    public int PointCount => pointCount;
    public bool IsInitialized => currentPositions != null && previousPositions != null;

    public void Initialize(
        int requestedPointCount,
        float ropeLength,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        pointCount = Mathf.Max(2, requestedPointCount);
        segmentLength = ropeLength / Mathf.Max(1, pointCount - 1);

        currentPositions = new Vector3[pointCount];
        previousPositions = new Vector3[pointCount];

        Vector3 direction = endPoint - startPoint;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.down;
        }
        else
        {
            direction.Normalize();
        }

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pointPosition = startPoint + direction * (segmentLength * i);
            currentPositions[i] = pointPosition;
            previousPositions[i] = pointPosition;
        }

        SnapEndpoints(startPoint, endPoint);
    }

    public void SetSegmentLength(float newSegmentLength)
    {
        segmentLength = Mathf.Max(0.001f, newSegmentLength);
    }

    public void Simulate(
        float deltaTime,
        int constraintIterations,
        float gravity,
        float ropeGravityMultiplier,
        float ropeVerletDamping,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (!IsInitialized)
        {
            return;
        }

        ApplyVerlet(
            deltaTime,
            gravity,
            ropeGravityMultiplier,
            ropeVerletDamping,
            startPoint,
            endPoint
        );

        int safeIterations = Mathf.Max(1, constraintIterations);

        for (int iteration = 0; iteration < safeIterations; iteration++)
        {
            ApplyDistanceConstraints(startPoint, endPoint);
        }

        SnapEndpoints(startPoint, endPoint);
    }

    private void ApplyVerlet(
        float deltaTime,
        float gravity,
        float ropeGravityMultiplier,
        float ropeVerletDamping,
        Vector3 startPoint,
        Vector3 endPoint)
    {
        Vector3 gravityAcceleration = Vector3.down * gravity * ropeGravityMultiplier;
        float safeDamping = Mathf.Clamp(ropeVerletDamping, 0.0f, 1.0f);

        for (int i = 1; i < pointCount - 1; i++)
        {
            Vector3 currentPosition = currentPositions[i];
            Vector3 previousPosition = previousPositions[i];

            Vector3 velocity = (currentPosition - previousPosition) * safeDamping;

            previousPositions[i] = currentPosition;
            currentPositions[i] =
                currentPosition + velocity + gravityAcceleration * deltaTime * deltaTime;
        }

        SnapEndpoints(startPoint, endPoint);
    }

    private void ApplyDistanceConstraints(Vector3 startPoint, Vector3 endPoint)
    {
        SnapEndpoints(startPoint, endPoint);

        for (int i = 0; i < pointCount - 1; i++)
        {
            Vector3 pointA = currentPositions[i];
            Vector3 pointB = currentPositions[i + 1];

            Vector3 delta = pointB - pointA;
            float currentDistance = delta.magnitude;

            if (currentDistance <= 0.0001f)
            {
                continue;
            }

            float difference = (currentDistance - segmentLength) / currentDistance;
            Vector3 correction = delta * difference;

            bool pointAIsFixed = i == 0;
            bool pointBIsFixed = i + 1 == pointCount - 1;

            if (pointAIsFixed && pointBIsFixed)
            {
                continue;
            }

            if (pointAIsFixed)
            {
                currentPositions[i + 1] -= correction;
            }
            else if (pointBIsFixed)
            {
                currentPositions[i] += correction;
            }
            else
            {
                currentPositions[i] += correction * 0.5f;
                currentPositions[i + 1] -= correction * 0.5f;
            }
        }

        SnapEndpoints(startPoint, endPoint);
    }

    private void SnapEndpoints(Vector3 startPoint, Vector3 endPoint)
    {
        if (!IsInitialized)
        {
            return;
        }

        currentPositions[0] = startPoint;
        currentPositions[pointCount - 1] = endPoint;

        previousPositions[0] = startPoint;
        previousPositions[pointCount - 1] = endPoint;
    }
}