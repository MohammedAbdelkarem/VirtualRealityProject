public static class BucketVisualLayoutModel
{
    public static BucketVisualLayout Calculate(
        float handleHeight,
        float bucketHeight,
        bool createDrainOutlet,
        float drainOutletLength)
    {
        float topY = -handleHeight;
        float bottomY = topY - bucketHeight;
        float outletEndY = createDrainOutlet
            ? bottomY - drainOutletLength
            : bottomY;

        return new BucketVisualLayout(
            topY,
            bottomY,
            outletEndY
        );
    }
}