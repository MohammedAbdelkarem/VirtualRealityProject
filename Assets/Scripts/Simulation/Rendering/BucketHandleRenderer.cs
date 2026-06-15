using UnityEngine;

public static class BucketHandleRenderer
{
    public static Vector3 Setup(
        LineRenderer handleRenderer,
        float topRadius,
        float handleHeight,
        float handleAttachWidthFactor,
        float handleWidth,
        Color handleColor)
    {
        handleRenderer.useWorldSpace = false;
        handleRenderer.positionCount = 21;
        handleRenderer.startWidth = handleWidth;
        handleRenderer.endWidth = handleWidth;
        handleRenderer.numCornerVertices = 10;
        handleRenderer.numCapVertices = 10;
        handleRenderer.material = MaterialFactory.CreateLineMaterial(handleColor);
        handleRenderer.startColor = handleColor;
        handleRenderer.endColor = handleColor;

        float rimY = -handleHeight;
        float attachX = topRadius * handleAttachWidthFactor;

        // Slight insertion into bucket wall.
        float endpointY = rimY - 0.012f;

        // Apex height of handle.
        float apexY = rimY + handleHeight * 0.72f;

        float z = 0.0f;

        Vector3 apexPosition = new Vector3(0.0f, apexY, z);

        for (int i = 0; i < handleRenderer.positionCount; i++)
        {
            float t = i / (float)(handleRenderer.positionCount - 1);

            float x = Mathf.Lerp(-attachX, attachX, t);

            float arch =
                1.0f -
                Mathf.Pow((t - 0.5f) * 2.0f, 2.0f);

            float y = Mathf.Lerp(endpointY, apexY, arch);

            handleRenderer.SetPosition(i, new Vector3(x, y, z));
        }

        return apexPosition;
    }
}