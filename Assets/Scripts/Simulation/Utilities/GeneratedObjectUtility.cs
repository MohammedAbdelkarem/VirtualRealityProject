using UnityEngine;

public static class GeneratedObjectUtility
{
    public static GameObject CreateChild(Transform parent, string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child;
    }

    public static Transform CreateOrUpdatePoint(
        Transform parent,
        string objectName,
        Vector3 localPosition)
    {
        Transform pointTransform = parent.Find(objectName);

        if (pointTransform == null)
        {
            GameObject pointObject = new GameObject(objectName);
            pointTransform = pointObject.transform;
            pointTransform.SetParent(parent, false);
        }

        pointTransform.localPosition = localPosition;
        pointTransform.localRotation = Quaternion.identity;
        pointTransform.localScale = Vector3.one;

        return pointTransform;
    }

    public static void ClearGeneratedVisualChildren(
        Transform parent,
        bool applicationIsPlaying,
        params string[] generatedNames)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            if (!NameExists(child.name, generatedNames))
            {
                continue;
            }

            if (applicationIsPlaying)
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static bool NameExists(string objectName, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (objectName == names[i])
            {
                return true;
            }
        }

        return false;
    }
}