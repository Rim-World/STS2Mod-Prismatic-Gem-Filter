using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     卡牌筛选状态的纯函数。唯一状态是「已排除的卡牌标识集合」。
///     <para>
///         分组开关与单卡豁免都是这份集合的投影：分组开关读「至少一张成员被排除」，
///         写「全组排除」或「全组保留」；单卡开关写「排除」或「保留」。
///         规则见 SP-031 至 SP-036 与 <c>4.ADR/015</c>。
///     </para>
/// </summary>
internal static class FilterState
{
    /// <summary>该卡是否在排除集合内。</summary>
    public static bool IsFiltered(IReadOnlyCollection<string> filtered, string entry)
    {
        foreach (var item in filtered)
        {
            if (string.Equals(item, entry, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>本组是否至少有一张成员被排除。分组开关的显示值由此派生。</summary>
    public static bool IsGroupFiltered(IReadOnlyCollection<string> filtered, CardGroup group)
    {
        foreach (var entry in group.Members)
        {
            if (IsFiltered(filtered, entry))
                return true;
        }

        return false;
    }

    /// <summary>本组当前被排除的成员数，供设置页的 x/y 说明使用。</summary>
    public static int CountFiltered(IReadOnlyCollection<string> filtered, CardGroup group)
    {
        var count = 0;
        foreach (var entry in group.Members)
        {
            if (IsFiltered(filtered, entry))
                count++;
        }

        return count;
    }

    /// <summary>
    ///     改写一张卡的豁免状态。豁免等于保留，即从排除集合移除。
    ///     返回新清单，不改入参，避免绕过存档。
    /// </summary>
    public static List<string> SetExempt(IReadOnlyCollection<string> source, string entry, bool exempted)
    {
        var copy = new List<string>(source);
        if (exempted)
        {
            copy.RemoveAll(item => string.Equals(item, entry, StringComparison.Ordinal));
        }
        else if (!IsFiltered(copy, entry))
        {
            copy.Add(entry);
        }

        return copy;
    }

    /// <summary>
    ///     批量改写一个分组。打开时把全部成员加入排除集合，关闭时把全部成员移出。
    ///     返回按标识排序的新清单，保证落盘顺序稳定。
    /// </summary>
    public static List<string> SetGroup(IReadOnlyCollection<string> source, CardGroup group, bool filtered)
    {
        var set = new HashSet<string>(source, StringComparer.Ordinal);
        foreach (var entry in group.Members)
        {
            if (filtered)
                set.Add(entry);
            else
                set.Remove(entry);
        }

        var result = new List<string>(set.Count);
        result.AddRange(set);
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
