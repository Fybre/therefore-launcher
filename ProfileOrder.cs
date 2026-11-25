namespace ThereforeLauncher;

public static class ProfileOrder
{
    // Slot is an insertion boundary in the original list: 0 is first, Count is last.
    public static List<T> Move<T>(IReadOnlyList<T> items, int source, int slot)
    {
        if (source < 0 || source >= items.Count) throw new ArgumentOutOfRangeException(nameof(source));
        if (slot < 0 || slot > items.Count) throw new ArgumentOutOfRangeException(nameof(slot));
        var result = items.ToList();
        var item = result[source]; result.RemoveAt(source);
        result.Insert(slot > source ? slot - 1 : slot, item);
        return result;
    }
}
