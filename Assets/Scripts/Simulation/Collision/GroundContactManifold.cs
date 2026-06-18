using System.Collections.Generic;

public class GroundContactManifold
{
    private readonly List<GroundContact> contacts =
        new List<GroundContact>();

    public IReadOnlyList<GroundContact> Contacts => contacts;
    public int Count => contacts.Count;

    public void Clear()
    {
        contacts.Clear();
    }

    public void Add(GroundContact contact)
    {
        contacts.Add(contact);
    }

    public float GetMaxPenetrationDepth()
    {
        float maxPenetration = 0.0f;

        for (int i = 0; i < contacts.Count; i++)
        {
            if (contacts[i].PenetrationDepth > maxPenetration)
            {
                maxPenetration = contacts[i].PenetrationDepth;
            }
        }

        return maxPenetration;
    }

    public void KeepDeepestContacts(int maxCount)
    {
        contacts.Sort(
            (a, b) => b.PenetrationDepth.CompareTo(a.PenetrationDepth)
        );

        if (contacts.Count <= maxCount)
        {
            return;
        }

        contacts.RemoveRange(
            maxCount,
            contacts.Count - maxCount
        );
    }
}