using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace PrismaticGemFilter;

/// <summary>
///     候选集合的判定逻辑。抽成纯函数，便于在没有对局的情况下用真实卡池数据自测。
/// </summary>
internal static class CardFilter
{
    /// <summary>一次过滤的统计。字段含义与日志里的键名一致。</summary>
    public readonly record struct Stats(int Candidates, int Removed, int Kept, int PoolGateSkipped);

    /// <summary>
    ///     逐张判定并返回保留的牌。
    ///     <para>判定顺序：不在排除名单内 → 保留；在名单内但属于玩家职业卡池 → 保留；其余 → 排除。</para>
    /// </summary>
    /// <param name="candidates">待过滤的候选牌。</param>
    /// <param name="ownPoolEntries">玩家职业卡池全部卡牌的 <c>ModelId.Entry</c> 集合，即池门控的放行范围。</param>
    /// <param name="excludedEntries">当前勾选分组的排除名单，元素是 <c>ModelId.Entry</c>。</param>
    /// <param name="stats">输出统计。</param>
    public static List<CardModel> Apply(
        IReadOnlyList<CardModel> candidates,
        ISet<string> ownPoolEntries,
        IReadOnlySet<string> excludedEntries,
        out Stats stats)
    {
        var kept = new List<CardModel>(candidates.Count);
        var removed = 0;
        var poolGateSkipped = 0;

        foreach (var card in candidates)
        {
            var entry = card.Id.Entry;

            if (!excludedEntries.Contains(entry))
            {
                kept.Add(card);
                continue;
            }

            if (ownPoolEntries.Contains(entry))
            {
                poolGateSkipped++;
                kept.Add(card);
                continue;
            }

            removed++;
        }

        stats = new Stats(candidates.Count, removed, kept.Count, poolGateSkipped);
        return kept;
    }
}
