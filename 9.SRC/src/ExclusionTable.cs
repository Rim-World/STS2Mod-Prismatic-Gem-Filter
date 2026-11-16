using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     默认豁免清单。键的形如 <c>分组标识:卡牌标识</c>。
///     <para>
///         本文件在 0.0.3 时只有一组奥斯提牌名单。0.0.4 起成员名单改由
///         <see cref="CardGroupTable" /> 提供，本文件改为承载跨分组的默认豁免表。
///         0.0.5 删掉了多人限定卡放行表：这类卡已从各分组移出，见 ADR-009。
///         文件名保留，避免打断既有引用与提交历史。
///     </para>
/// </summary>
internal static class ExclusionTable
{
    /// <summary>豁免项的键分隔符。落盘形如 <c>doom:NEUROSURGE</c>。</summary>
    private const char Separator = ':';

    private static readonly string[] DefaultExemptionKeysValue =
    [
        // 灾厄牌组：精神过载。用户 2026-11-15 07:00 点名。
        "doom:NEUROSURGE",

        // 球牌组（含激发、球位）：聚变、暴涨。
        "orb:FUSION",
        "orb:BULK_UP",

        // 铸剑牌组与星牌组：大爆炸在两组各记一条，见 SP-013。
        "forge:BIG_BANG",
        "star:BIG_BANG",

        // 星牌组：汇流。
        "star:CONVERGENCE",

        // 集中牌组：超能光束。它带集中关键词却降低集中，用户 2026-11-16 19:30 点名。
        "focus:HYPERBEAM",

        // 悲伤牌组：活跃气氛。它同时给悲伤与喜悦，用户 2026-11-17 01:42 点名默认不过滤。
        "sadness:BRIGHTEN_UP",
    ];

    /// <summary>默认豁免清单，元素是 <c>分组标识:卡牌标识</c>。见 SP-021。</summary>
    public static IReadOnlyList<string> DefaultExemptionKeys => DefaultExemptionKeysValue;

    /// <summary>拼出豁免项在落盘清单里的键。</summary>
    public static string Key(string group, string entry) => group + Separator + entry;

    /// <summary>该卡在该分组下是否默认为豁免状态。</summary>
    public static bool IsDefaultExempt(string group, string entry)
    {
        var key = Key(group, entry);
        foreach (var item in DefaultExemptionKeysValue)
        {
            if (string.Equals(item, key, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
