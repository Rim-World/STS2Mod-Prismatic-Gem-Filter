using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace PrismaticGemFilter;

/// <summary>
///     在 <see cref="CardCreationOptions.GetPossibleCards" /> 之后过滤候选牌。
///     <para>
///         目标方法三版签名一致：<c>public IEnumerable&lt;CardModel&gt; GetPossibleCards(Player player)</c>。
///         它是所有发牌链路的公共入口，见 <c>4.ADR/001-在候选牌枚举处统一过滤.md</c>。
///     </para>
/// </summary>
internal sealed class CardCreationPatch : IPatchMethod
{
    /// <summary>玩家职业卡池的卡牌标识集合，按卡池实例缓存。卡池模型是规范单例，可以按引用做键。</summary>
    private static readonly ConditionalWeakTable<CardPoolModel, HashSet<string>> OwnPoolEntries = new();

    public static string PatchId => "prismatic_gem_filter.card_reward_candidates";

    public static string Description => "按分组排除其他职业专属的候选牌";

    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), [typeof(Player)]),
    ];

    /// <summary>
    ///     后置过滤。整个方法体都有异常保护：出错时保持原结果，不得把异常抛进发牌链路。
    /// </summary>
    public static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        try
        {
            if (__result is null)
                return;
            if (player?.Character is null)
                return;

            var candidates = __result as List<CardModel> ?? __result.ToList();
            if (candidates.Count == 0)
                return;

            var ownPoolEntries = OwnPoolEntries.GetValue(
                player.Character.CardPool,
                static pool => pool.AllCardIds.Select(id => id.Entry).ToHashSet(StringComparer.Ordinal));

            var kept = CardFilter.Apply(candidates, ownPoolEntries, ExclusionTable.OstyAttack, out var stats);

            if (stats.Removed == 0)
            {
                // 候选里出现别的职业的牌，说明这次发牌被加宽过，此时「没排除任何牌」是值得记录的结论。
                // 候选全在本职业卡池里时属于常规发牌，降到 Debug，避免刷日志。
                var message =
                    $"[filter] candidates={stats.Candidates} removed=0 kept={stats.Kept} " +
                    $"poolGate={stats.PoolGateSkipped} otherPool={stats.OtherPoolCards}";

                if (stats.OtherPoolCards > 0)
                    Plugin.Logger.Info(message);
                else
                    Plugin.Logger.Debug(message);

                __result = candidates;
                return;
            }

            if (kept.Count == 0)
            {
                Plugin.Logger.Warn(
                    $"[filter] candidates={stats.Candidates} removed={stats.Removed} -> empty, filter abandoned");
                __result = candidates;
                return;
            }

            Plugin.Logger.Info(
                $"[filter] candidates={stats.Candidates} removed={stats.Removed} " +
                $"kept={stats.Kept} poolGate={stats.PoolGateSkipped} otherPool={stats.OtherPoolCards}");
            __result = kept;
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] failed, candidates left unchanged: {ex}");
        }
    }
}
