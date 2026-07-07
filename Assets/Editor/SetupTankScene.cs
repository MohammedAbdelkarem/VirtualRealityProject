using UnityEngine;
using UnityEditor;
using System.IO;

public class SetupTankScene
{
    [MenuItem("Tools/Setup Tank Scene")]
    static void Setup()
    {
        string matPath = "Assets/Materials/TankGlass.mat";
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (glassMat == null)
        {
            Shader std = Shader.Find("Standard");
            glassMat = new Material(std);
            glassMat.name = "TankGlass";
            glassMat.SetFloat("_Mode", 2.0f);
            glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMat.SetInt("_ZWrite", 0);
            glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            glassMat.EnableKeyword("_ALPHABLEND_ON");
            glassMat.color = new Color(0.7f, 0.85f, 1f, 0.25f);
            glassMat.SetFloat("_Metallic", 0.1f);
            glassMat.SetFloat("_Glossiness", 0.9f);
            AssetDatabase.CreateAsset(glassMat, matPath);
            Debug.Log("Created TankGlass material at " + matPath);
        }

        GameObject tankObj = GameObject.Find("Tank");
        if (tankObj == null)
        {
            tankObj = new GameObject("Tank");
            tankObj.transform.position = Vector3.zero;
            Debug.Log("Created Tank GameObject");
        }

        TankFluidSimulation sim = tankObj.GetComponent<TankFluidSimulation>();
        if (sim == null) sim = tankObj.AddComponent<TankFluidSimulation>();

        string[] computeGuids = AssetDatabase.FindAssets("SPHSimulation t:ComputeShader");
        if (computeGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(computeGuids[0]);
            ComputeShader cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            if (cs != null) sim.computeShader = cs;
        }

        sim.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FluidMat.mat");

        GameObject tankVisual = GameObject.Find("TankVisual");
        if (tankVisual == null)
        {
            tankVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tankVisual.name = "TankVisual";
            tankVisual.transform.SetParent(tankObj.transform, false);
            tankVisual.transform.localPosition = Vector3.zero;
            tankVisual.transform.localScale = new Vector3(0.3f, 0.45f, 0.3f);
        }
        else
        {
            tankVisual.transform.SetParent(tankObj.transform, true);
        }

        MeshRenderer mr = tankVisual.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = glassMat;

        Debug.Log("Tank scene setup complete. Open lequid.unity and press Play.");
    }
}
