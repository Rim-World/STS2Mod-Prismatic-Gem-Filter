using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     设置状态的纯函数。持久化只有两份来源：每个分组一个开关，每张牌一个全局豁免状态。
///     <para>
///         分组开关只决定该组是否参与过滤；单卡豁免状态跨组共享。
///         实际过滤池由两者计算：已开启组的成员并集，减去全局豁免牌。
///         规则见 SP-031 至 SP-039 与 <c>4.ADR/016</c>。
///     </para>
/// </summary>
internal static class FilterState
{
    /// <summary>该卡是否在全局豁免名单内。</summary>
    public static bool IsExempt(IReadOnlyCollection<string> exemptions, string entry)
    {
        foreach (var item in exemptions)
        {
            if (string.Equals(item, entry, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>本组有多少张成员处于全局豁免状态。</summary>
    public static int CountExempt(IReadOnlyCollection<string> exemptions, CardGroup group)
    {
        var count = 0;
        foreach (var entry in group.Members)
        {
            if (IsExempt(exemptions, entry))
                count++;
        }

        return count;
    }

    /// <summary>
    ///     改写一张牌的全局豁免状态。豁免等于保留，即不进入实际过滤池。
    ///     返回按标识排序的新清单，不改入参，避免绕过存档。
    /// </summary>
    public static List<string> SetExempt(IReadOnlyCollection<string> source, string entry, bool exempted)
    {
        var set = new HashSet<string>(source, StringComparer.Ordinal);

        if (exempted)
            set.Add(entry);
        else
            set.Remove(entry);

        var result = new List<string>(set.Count);
        result.AddRange(set);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>某分组是否开启。缺键时取该分组的默认值。</summary>
    public static bool IsGroupEnabled(IReadOnlyDictionary<string, bool>? groups, CardGroup group)
    {
        if (groups is not null && groups.TryGetValue(group.Id, out var enabled))
            return enabled;

        return group.DefaultEnabled;
    }

    /// <summary>16 个分组的默认开关值。包含当前版本不可见的分组。</summary>
    public static Dictionary<string, bool> DefaultGroups()
    {
        var result = new Dictionary<string, bool>(CardGroups.All.Length, StringComparer.Ordinal);
        foreach (var group in CardGroups.All)
            result[group.Id] = group.DefaultEnabled;

        return result;
    }

    /// <summary>
    ///     改写一个分组的开关，返回完整的新字典。不改入参，也不改单卡豁免名单。
    /// </summary>
    public static Dictionary<string, bool> SetGroupEnabled(
        IReadOnlyDictionary<string, bool>? source,
        CardGroup group,
        bool enabled)
    {
        var result = DefaultGroups();

        if (source is not null)
        {
            foreach (var pair in source)
                result[pair.Key] = pair.Value;
        }

        result[group.Id] = enabled;
        return result;
    }

    /// <summary>
    ///     按已开启组与全局豁免计算实际过滤池。池只包含当前版本可见分组的成员。
    ///     这是唯一允许生成过滤池的入口；不要在别处增量维护第二份池。
    /// </summary>
    public static HashSet<string> BuildPool(
        IEnumerable<CardGroup> groups,
        IReadOnlySet<string> enabledGroupIds,
        IReadOnlySet<string> exemptions)
    {
        var pool = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            if (!enabledGroupIds.Contains(group.Id))
                continue;

            foreach (var entry in group.Members)
            {
                if (exemptions.Contains(entry))
                    continue;

                pool.Add(entry);
            }
        }

        return pool;
    }

    /// <summary>给定成员集合里有多少张当前位于实际过滤池。</summary>
    public static int CountInPool(IReadOnlyCollection<string> entries, IReadOnlySet<string> pool)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (pool.Contains(entry))
                count++;
        }

        return count;
    }
}
