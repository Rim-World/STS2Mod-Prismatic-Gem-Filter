using System.Collections.Generic;
using System.Linq;

namespace PrismaticGemFilter;

/// <summary>
///     默认排除表。元素是卡牌标识，例如 <c>NEUROSURGE</c>。
///     <para>
///         0.0.13 起唯一筛选状态是「已排除的卡牌标识集合」，本表负责生成首次启动的默认集合。
///         规则：所有默认勾选分组的成员并集，减去显式默认豁免。
///         同一张跨组卡只记一条；<c>BIG_BANG</c> 在铸造与辉星两组都出现，但默认豁免只扣一次。
///         见 SP-021、SP-031 与 <c>4.ADR/015</c>。
///     </para>
///     <para>
///         默认集合按 <see cref="CardGroups.All" /> 计算，包含当前游戏版本隐藏的愚者卡。
///         隐藏卡不进入当前版本的可见分组，也不进入排除索引，但玩家切换游戏版本后仍能读到正确默认状态。
///     </para>
///     <para>
///         本文件在 0.0.3 时只有一组奥斯提牌名单。0.0.4 起成员名单改由
///         <see cref="CardGroupTable" /> 提供。0.0.5 删掉多人限定卡放行表。
///         0.0.12 把豁免键由 <c>分组标识:卡牌标识</c> 改为裸卡牌标识。
///         0.0.13 把「默认豁免」扩为「默认排除」，分组开关不再单独落盘。
///     </para>
/// </summary>
internal static class ExclusionTable
{
    /// <summary>显式默认豁免。这些牌即使所在分组默认勾选，也默认保留。</summary>
    private static readonly string[] ExplicitExemptionKeysValue =
    [
        // 灾厄牌组：精神过载。用户 2026-11-15 07:00 点名。
        "NEUROSURGE",

        // 灾厄牌组：血肉戏法。用户 2026-11-18 点名加入本组并默认豁免。
        "SLEIGHT_OF_FLESH",

        // 召唤牌组：洁净、唤起。用户 2026-11-18 点名默认豁免。
        "CLEANSE",
        "INVOKE",

        // 球牌组（含激发、球位）：聚变、暴涨。
        "FUSION",
        "BULK_UP",

        // 铸造牌组与辉星牌组：大爆炸。按卡牌只记一条，两个分组共享。
        "BIG_BANG",

        // 辉星牌组：汇流。
        "CONVERGENCE",

        // 集中牌组：超能光束。它带集中关键词却降低集中，用户 2026-11-16 19:30 点名。
        "HYPERBEAM",

        // 悲伤牌组：活跃气氛。它同时给悲伤与喜悦，用户 2026-11-17 01:42 点名默认不过滤。
        "BRIGHTEN_UP",
    ];

    private static readonly HashSet<string> DefaultExcludedSet = Build();

    /// <summary>默认排除集合，元素是卡牌标识，按标识排序。见 SP-031。</summary>
    public static readonly IReadOnlyList<string> DefaultExcludedKeys = BuildSortedList();

    /// <summary>显式默认豁免，供文档与测试核对。</summary>
    public static IReadOnlyList<string> ExplicitExemptionKeys => ExplicitExemptionKeysValue;

    /// <summary>该卡是否默认被排除。不区分分组。</summary>
    public static bool IsDefaultExcluded(string entry) => DefaultExcludedSet.Contains(entry);

    private static HashSet<string> Build()
    {
        // 用全表行与分组默认值求默认集合，不用 CardGroups.All 的 Members：
        // 0.111 上 All 会剔除愚者成员，若按它计算，玩家切到 0.113 后愚者卡会缺少默认状态。
        var defaultEnabledGroups = new HashSet<string>(
            CardGroups.All.Where(static group => group.DefaultEnabled).Select(static group => group.Id),
            StringComparer.Ordinal);

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in CardGroupTable.Rows)
        {
            if (defaultEnabledGroups.Contains(row.Group))
                set.Add(row.Entry);
        }

        foreach (var entry in ExplicitExemptionKeysValue)
            set.Remove(entry);

        return set;
    }

    private static string[] BuildSortedList()
    {
        var list = new List<string>(DefaultExcludedSet);
        list.Sort(StringComparer.Ordinal);
        return list.ToArray();
    }
}
