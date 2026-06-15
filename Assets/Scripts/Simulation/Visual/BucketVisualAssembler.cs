using UnityEngine;

public readonly struct BucketVisualBuildRequest
{
    public readonly Transform ParentTransform;
    public readonly bool ApplicationIsPlaying;

    public readonly float TopRadius;
    public readonly float BottomRadius;
    public readonly float Height;
    public readonly float WallThickness;
    public readonly int Segments;

    public readonly bool CreateBottomBase;
    public readonly float DrainHoleRadius;
    public readonly bool CreateDrainOutlet;
    public readonly float DrainOutletOuterRadius;
    public readonly float DrainOutletLength;

    public readonly float HandleHeight;
    public readonly float HandleAttachWidthFactor;
    public readonly float HandleWidth;

    public readonly Color BucketColor;
    public readonly Color InnerBucketColor;
    public readonly Color RimColor;
    public readonly Color HandleColor;
    public readonly Color DrainOutletColor;

    public readonly string ShellObjectName;
    public readonly string HandleObjectName;
    public readonly string RopeAttachmentPointName;
    public readonly string RopeKnotObjectName;
    public readonly string DrainOutletObjectName;
    public readonly string LiquidExitPointName;

    public BucketVisualBuildRequest(
        Transform parentTransform,
        bool applicationIsPlaying,
        float topRadius,
        float bottomRadius,
        float height,
        float wallThickness,
        int segments,
        bool createBottomBase,
        float drainHoleRadius,
        bool createDrainOutlet,
        float drainOutletOuterRadius,
        float drainOutletLength,
        float handleHeight,
        float handleAttachWidthFactor,
        float handleWidth,
        Color bucketColor,
        Color innerBucketColor,
        Color rimColor,
        Color handleColor,
        Color drainOutletColor,
        string shellObjectName,
        string handleObjectName,
        string ropeAttachmentPointName,
        string ropeKnotObjectName,
        string drainOutletObjectName,
        string liquidExitPointName)
    {
        ParentTransform = parentTransform;
        ApplicationIsPlaying = applicationIsPlaying;

        TopRadius = topRadius;
        BottomRadius = bottomRadius;
        Height = height;
        WallThickness = wallThickness;
        Segments = segments;

        CreateBottomBase = createBottomBase;
        DrainHoleRadius = drainHoleRadius;
        CreateDrainOutlet = createDrainOutlet;
        DrainOutletOuterRadius = drainOutletOuterRadius;
        DrainOutletLength = drainOutletLength;

        HandleHeight = handleHeight;
        HandleAttachWidthFactor = handleAttachWidthFactor;
        HandleWidth = handleWidth;

        BucketColor = bucketColor;
        InnerBucketColor = innerBucketColor;
        RimColor = rimColor;
        HandleColor = handleColor;
        DrainOutletColor = drainOutletColor;

        ShellObjectName = shellObjectName;
        HandleObjectName = handleObjectName;
        RopeAttachmentPointName = ropeAttachmentPointName;
        RopeKnotObjectName = ropeKnotObjectName;
        DrainOutletObjectName = drainOutletObjectName;
        LiquidExitPointName = liquidExitPointName;
    }
}

public readonly struct BucketVisualBuildResult
{
    public readonly Transform RopeAttachmentPoint;
    public readonly Transform LiquidExitPoint;

    public BucketVisualBuildResult(
        Transform ropeAttachmentPoint,
        Transform liquidExitPoint)
    {
        RopeAttachmentPoint = ropeAttachmentPoint;
        LiquidExitPoint = liquidExitPoint;
    }
}

public static class BucketVisualAssembler
{
    public static BucketVisualBuildResult Build(BucketVisualBuildRequest request)
    {
        ClearGeneratedVisualChildren(request);

        BucketVisualLayout layout =
            BucketVisualLayoutModel.Calculate(
                request.HandleHeight,
                request.Height,
                request.CreateDrainOutlet,
                request.DrainOutletLength
            );

        CreateBucketShell(request);

        Vector3 handleApexLocalPosition =
            CreateBucketHandle(request);

        Transform ropeAttachmentPoint =
            GeneratedObjectUtility.CreateOrUpdatePoint(
                request.ParentTransform,
                request.RopeAttachmentPointName,
                handleApexLocalPosition
            );

        CreateRopeKnotVisual(
            request,
            handleApexLocalPosition
        );

        if (request.CreateDrainOutlet)
        {
            CreateDrainOutlet(
                request,
                layout.BottomY,
                layout.OutletEndY
            );
        }

        Transform liquidExitPoint =
            GeneratedObjectUtility.CreateOrUpdatePoint(
                request.ParentTransform,
                request.LiquidExitPointName,
                new Vector3(0.0f, layout.OutletEndY - 0.02f, 0.0f)
            );

        return new BucketVisualBuildResult(
            ropeAttachmentPoint,
            liquidExitPoint
        );
    }

    private static void CreateBucketShell(BucketVisualBuildRequest request)
    {
        GameObject shellObject =
            GeneratedObjectUtility.CreateChild(
                request.ParentTransform,
                request.ShellObjectName
            );

        MeshFilter meshFilter =
            shellObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer =
            shellObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh =
            BucketMeshGenerator.Build(
                request.TopRadius,
                request.BottomRadius,
                request.Height,
                request.WallThickness,
                request.Segments,
                request.HandleHeight,
                request.CreateBottomBase,
                request.DrainHoleRadius
            );

        Material[] materials = new Material[4];
        materials[0] = MaterialFactory.CreateLitMaterial(request.BucketColor);
        materials[1] = MaterialFactory.CreateLitMaterial(request.InnerBucketColor);
        materials[2] = MaterialFactory.CreateLitMaterial(request.RimColor);
        materials[3] = MaterialFactory.CreateLitMaterial(request.DrainOutletColor);

        meshRenderer.sharedMaterials = materials;
    }

    private static Vector3 CreateBucketHandle(BucketVisualBuildRequest request)
    {
        GameObject handleObject =
            GeneratedObjectUtility.CreateChild(
                request.ParentTransform,
                request.HandleObjectName
            );

        LineRenderer handleRenderer =
            handleObject.AddComponent<LineRenderer>();

        return BucketHandleRenderer.Setup(
            handleRenderer,
            request.TopRadius,
            request.HandleHeight,
            request.HandleAttachWidthFactor,
            request.HandleWidth,
            request.HandleColor
        );
    }

    private static void CreateDrainOutlet(
        BucketVisualBuildRequest request,
        float topY,
        float bottomY)
    {
        GameObject outletObject =
            GeneratedObjectUtility.CreateChild(
                request.ParentTransform,
                request.DrainOutletObjectName
            );

        MeshFilter outletMeshFilter =
            outletObject.AddComponent<MeshFilter>();

        MeshRenderer outletMeshRenderer =
            outletObject.AddComponent<MeshRenderer>();

        outletMeshFilter.sharedMesh =
            DrainOutletMeshGenerator.Build(
                request.DrainHoleRadius,
                request.DrainOutletOuterRadius,
                request.Segments,
                topY,
                bottomY
            );

        outletMeshRenderer.sharedMaterial =
            MaterialFactory.CreateLitMaterial(request.DrainOutletColor);
    }

    private static void CreateRopeKnotVisual(
        BucketVisualBuildRequest request,
        Vector3 localPosition)
    {
        GameObject knotObject =
            GeneratedObjectUtility.CreateChild(
                request.ParentTransform,
                request.RopeKnotObjectName
            );

        knotObject.transform.localPosition = localPosition;

        MeshFilter meshFilter =
            knotObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer =
            knotObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh =
            PrimitiveMeshGenerator.BuildSphere(
                request.HandleWidth * 1.25f,
                10,
                16
            );

        meshRenderer.sharedMaterial =
            MaterialFactory.CreateLitMaterial(request.HandleColor);
    }

    private static void ClearGeneratedVisualChildren(
        BucketVisualBuildRequest request)
    {
        GeneratedObjectUtility.ClearGeneratedVisualChildren(
            request.ParentTransform,
            request.ApplicationIsPlaying,
            request.ShellObjectName,
            request.HandleObjectName,
            request.RopeKnotObjectName,
            request.DrainOutletObjectName
        );
    }
}