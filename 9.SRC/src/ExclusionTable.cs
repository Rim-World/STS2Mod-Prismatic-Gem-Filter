using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     排除逻辑用到的两张固定表：默认豁免清单与多人限定卡放行表。
///     <para>
///         本文件在 0.0.3 时只有一组奥斯提牌名单。0.0.4 起成员名单改由
///         <see cref="CardGroupTable" /> 提供，本文件改为承载两张跨分组的表。
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
    ];

    /// <summary>
    ///     多人限定卡的放行表。这些卡在 0.113.1 上 <c>MultiplayerConstraint</c> 为 <c>MultiplayerOnly</c>，
    ///     按 SP-014 不得被按标记判定的组排除。
    ///     <para>
    ///         用固定名单而不用运行期属性判定，理由有两条。属性判定要在每张候选牌上读一次模型，
    ///         而名单判定的成本是一次哈希查表；旧版本缺少该成员时，名单写法也不会抛异常。
    ///     </para>
    ///     <para>来源：<c>3.DOCS/003-E-豁免与多人卡取证.md</c> 第 3.3 节的 9 张。</para>
    /// </summary>
    private static readonly string[] MultiplayerProtectedValue =
    [
        "BLADE_SYMPHONY",
        "CONCOCT",
        "CONSTELLATION",
        "HAMMER_TIME",
        "HIBERNATE",
        "IGNITION",
        "LEGION_OF_BONE",
        "SNEAKY",
        "UNDERWORLD",
    ];

    /// <summary>默认豁免清单，元素是 <c>分组标识:卡牌标识</c>。见 SP-021。</summary>
    public static IReadOnlyList<string> DefaultExemptionKeys => DefaultExemptionKeysValue;

    /// <summary>多人限定卡放行表。不属于任何名单组的这些卡不得被按标记判定的组排除。见 SP-014。</summary>
    public static IReadOnlySet<string> MultiplayerProtected { get; } =
        new HashSet<string>(MultiplayerProtectedValue, StringComparer.Ordinal);

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
