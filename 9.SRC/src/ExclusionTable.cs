using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     默认豁免清单。元素是卡牌标识，例如 <c>NEUROSURGE</c>。
///     <para>
///         豁免按卡牌记录，不按分组。同一张牌同时属于两个分组时，两个豁免页共享同一个开关，
///         见 SP-013 与 <c>4.ADR/014-豁免按卡牌跨组同步.md</c>。
///     </para>
///     <para>
///         本文件在 0.0.3 时只有一组奥斯提牌名单。0.0.4 起成员名单改由
///         <see cref="CardGroupTable" /> 提供，本文件改为承载默认豁免表。
///         0.0.5 删掉了多人限定卡放行表：这类卡已从各分组移出，见 ADR-009。
///         0.0.12 把豁免键由 <c>分组标识:卡牌标识</c> 改为裸卡牌标识，见 ADR-014。
///         文件名保留，避免打断既有引用与提交历史。
///     </para>
/// </summary>
internal static class ExclusionTable
{
    private static readonly string[] DefaultExemptionKeysValue =
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

        // 铸剑牌组与星牌组：大爆炸。按卡牌只记一条，两个分组共享，见 SP-013。
        "BIG_BANG",

        // 星牌组：汇流。
        "CONVERGENCE",

        // 集中牌组：超能光束。它带集中关键词却降低集中，用户 2026-11-16 19:30 点名。
        "HYPERBEAM",

        // 悲伤牌组：活跃气氛。它同时给悲伤与喜悦，用户 2026-11-17 01:42 点名默认不过滤。
        "BRIGHTEN_UP",
    ];

    /// <summary>默认豁免清单，元素是卡牌标识。见 SP-021。</summary>
    public static IReadOnlyList<string> DefaultExemptionKeys => DefaultExemptionKeysValue;

    /// <summary>该卡是否默认为豁免状态。不区分分组。</summary>
    public static bool IsDefaultExempt(string entry)
    {
        foreach (var item in DefaultExemptionKeysValue)
        {
            if (string.Equals(item, entry, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
