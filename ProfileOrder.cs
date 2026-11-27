namespace ThereforeLauncher;

public static class ProfileOrder
{
    public static (List<TenantProfile> Profiles, TenantProfile Selected) MoveToGroup(
        IReadOnlyList<TenantProfile> profiles, TenantProfile selected, string? groupId, int slot, Func<TenantProfile, bool> inDestination)
    {
        var source = profiles.ToList().FindIndex(p => ReferenceEquals(p, selected));
        if (source < 0) throw new ArgumentException("Tenant is not in the list.", nameof(selected));
        var members = profiles.Where(inDestination).ToList();
        if (slot < 0 || slot > members.Count) throw new ArgumentOutOfRangeException(nameof(slot));
        var originalIndex = members.FindIndex(p => ReferenceEquals(p, selected));
        if (originalIndex >= 0 && originalIndex < slot) slot--;
        members.RemoveAll(p => ReferenceEquals(p, selected));
        var result = profiles.ToList(); result.RemoveAt(source);
        var replacement = selected with { GroupId = groupId };
        var insertion = slot < members.Count ? result.FindIndex(p => ReferenceEquals(p, members[slot]))
            : members.Count > 0 ? result.FindIndex(p => ReferenceEquals(p, members[^1])) + 1 : result.Count;
        result.Insert(insertion, replacement);
        return (result, replacement);
    }

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
