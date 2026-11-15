using System.Collections.Generic;

using MegaCrit.Sts2.Core.Models;

namespace PrismaticGemFilter;

/// <summary>
///     候选集合的判定逻辑。抽成纯函数，便于在没有对局的情况下用真实卡池数据自测。
/// </summary>
internal static class CardFilter
{
    /// <summary>一次过滤的统计。字段含义与日志里的键名一致。</summary>
    /// <param name="Candidates">候选总数。</param>
    /// <param name="Removed">被排除的张数。</param>
    /// <param name="Kept">保留的张数。</param>
    /// <param name="PoolGateSkipped">命中排除集合但属于玩家职业卡池、因而放行的张数。</param>
    /// <param name="OtherPoolCards">候选里不属于玩家职业卡池的张数。大于 0 说明这次发牌被加宽过。</param>
    public readonly record struct Stats(
        int Candidates,
        int Removed,
        int Kept,
        int PoolGateSkipped,
        int OtherPoolCards);

    /// <summary>
    ///     逐张判定并返回保留的牌。
    ///     <para>
    ///         判定顺序：不在任一排除集合内 → 保留；在集合内但属于玩家职业卡池 → 保留；其余 → 排除。
    ///         池门控对应 SP-004，两个集合的划分对应 SP-014。
    ///     </para>
    /// </summary>
    /// <param name="candidates">待过滤的候选牌。</param>
    /// <param name="ownPoolEntries">玩家职业卡池全部卡牌的 <c>ModelId.Entry</c> 集合，即池门控的放行范围。</param>
    /// <param name="markerExclusions">按标记判定的组的排除集合，已扣掉豁免与多人限定卡。</param>
    /// <param name="explicitExclusions">显式名单组的排除集合，已扣掉该组豁免。</param>
    /// <param name="stats">输出统计。</param>
    public static List<CardModel> Apply(
        IReadOnlyList<CardModel> candidates,
        ISet<string> ownPoolEntries,
        IReadOnlySet<string> markerExclusions,
        IReadOnlySet<string> explicitExclusions,
        out Stats stats)
    {
        var kept = new List<CardModel>(candidates.Count);
        var removed = 0;
        var poolGateSkipped = 0;
        var otherPoolCards = 0;

        foreach (var card in candidates)
        {
            var entry = card.Id.Entry;
            var inOwnPool = ownPoolEntries.Contains(entry);

            if (!inOwnPool)
                otherPoolCards++;

            if (!explicitExclusions.Contains(entry) && !markerExclusions.Contains(entry))
            {
                kept.Add(card);
                continue;
            }

            if (inOwnPool)
            {
                poolGateSkipped++;
                kept.Add(card);
                continue;
            }

            removed++;
        }

        stats = new Stats(candidates.Count, removed, kept.Count, poolGateSkipped, otherPoolCards);
        return kept;
    }
}
