using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     排除名单。原型阶段只实现一组：奥斯提牌。
///     <para>
///         标识取 <c>ModelId.Entry</c>，它等于卡牌类名转大写蛇形（<c>BoneShards</c> → <c>BONE_SHARDS</c>）。
///         用 Entry 而不是完整 <c>ModelId</c>，是为了不依赖卡牌类别的 slug 写法。
///     </para>
///     <para>
///         数据来源：<c>3.DOCS/002-E-卡牌分组判定依据.md</c>。
///         已核对 <c>4.STS2-113.1Reference/decompiled/MegaCrit.Sts2.Core.Models.CardPools/NecrobinderCardPool.cs</c>
///         的 <c>GenerateAllCards</c>，12 张牌全部在列。
///     </para>
/// </summary>
internal static class ExclusionTable
{
    private static readonly string[] OstyAttackEntries =
    [
        "BONE_SHARDS",
        "FETCH",
        "FLATTEN",
        "HIGH_FIVE",
        "POKE",
        "PROTECTOR",
        "RATTLE",
        "RIGHT_HAND_HAND",
        "SIC_EM",
        "SNAP",
        "SQUEEZE",
        "UNLEASH",
    ];

    /// <summary>奥斯提牌组的卡牌标识。静态只读，启动时构造一次。</summary>
    public static IReadOnlySet<string> OstyAttack { get; } =
        new HashSet<string>(OstyAttackEntries, StringComparer.Ordinal);

    /// <summary>清单条目数，供日志与自检使用。</summary>
    public static int OstyAttackCount => OstyAttackEntries.Length;
}
