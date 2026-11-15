using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
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

    /// <summary>已做过一次成员自检的卡池，避免每次发牌都扫名单。</summary>
    private static readonly ConditionalWeakTable<CardPoolModel, object> VerifiedPools = new();

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
    /// <param name="__instance">被补丁的那个选项实例，Harmony 注入，用于判定棱彩宝石会不会加宽本次发牌。</param>
    /// <param name="player">本次发牌的玩家。</param>
    /// <param name="__result">候选集合。</param>
    public static void Postfix(
        CardCreationOptions __instance,
        Player player,
        ref IEnumerable<CardModel> __result)
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

            var settings = ModSettings.Current;

            // SP-020：总开关关闭时什么都不做。
            if (!settings.MasterEnabled)
                return;

            if (!ShouldFilter(__instance, player, settings.Mode))
                return;

            FilterIndex.Get(out var markerExclusions, out var explicitExclusions);
            if (markerExclusions.Count == 0 && explicitExclusions.Count == 0)
                return;

            var ownPoolEntries = OwnPoolEntries.GetValue(
                player.Character.CardPool,
                static pool => pool.AllCardIds.Select(id => id.Entry).ToHashSet(StringComparer.Ordinal));

            VerifyGroupMembership(player.Character.CardPool, ownPoolEntries);

            var kept = CardFilter.Apply(
                candidates, ownPoolEntries, markerExclusions, explicitExclusions, out var stats);

            if (stats.Removed == 0)
            {
                // 候选里出现别的职业的牌，说明这次发牌被加宽过，此时「没排除任何牌」是值得记录的结论。
                // 候选全在本职业卡池里时属于常规发牌，降到 Debug，避免刷日志。
                var message = Describe(settings.Mode, stats);

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

            Plugin.Logger.Info(Describe(settings.Mode, stats));
            __result = kept;
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error($"[filter] failed, candidates left unchanged: {ex}");
        }
    }

    private static string Describe(FilterMode mode, CardFilter.Stats stats) =>
        $"[filter] mode={mode} groups={FilterIndex.EnabledGroups} candidates={stats.Candidates} " +
        $"removed={stats.Removed} kept={stats.Kept} poolGate={stats.PoolGateSkipped} otherPool={stats.OtherPoolCards}";

    /// <summary>
    ///     按当前判定模式决定要不要继续过滤。规则见 SP-005、SP-019 与 <c>4.ADR/008</c>。
    /// </summary>
    private static bool ShouldFilter(CardCreationOptions options, Player player, FilterMode mode)
    {
        if (options is null)
            return false;

        // 第二个取值：候选是否跨界由 CardFilter 统计，这里只看模式。
        if (mode == FilterMode.AnyWidenedPool)
            return true;

        // 默认取值：复刻原版棱彩宝石的早退集合，五条全部成立才认为它加宽了本次发牌。
        if (!HoldsPrismaticGem(player))
            return false;

        var flags = options.Flags;
        if (!flags.HasFlag(CardCreationFlags.IsCardReward))
            return false;
        if (flags.HasFlag(CardCreationFlags.NoCardPoolModifications))
            return false;

        var pools = options.CardPools;
        // 0.107.1 里卡池为空即用自定义卡表，棱彩宝石对那种选项早退。0.111 起该构造方式已移除。
        if (pools.Count == 0)
            return false;
        if (pools.All(static pool => pool.IsColorless))
            return false;

        return true;
    }

    /// <summary>玩家是否持有棱彩宝石。按类型判定，不拼字符串标识。</summary>
    private static bool HoldsPrismaticGem(Player player)
    {
        var relics = player.Relics;
        for (var i = 0; i < relics.Count; i++)
        {
            if (relics[i] is PrismaticGem)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     首次见到某个卡池时核对该池的名单成员是否真的存在。
    ///     用途：名单由 CSV 生成，标识写错或版本缺卡时在这里暴露，不必等玩家发现某张牌漏筛。
    /// </summary>
    private static void VerifyGroupMembership(CardPoolModel pool, HashSet<string> ownPoolEntries)
    {
        if (VerifiedPools.TryGetValue(pool, out _))
            return;

        VerifiedPools.Add(pool, new object());

        var declared = 0;
        var missing = new List<string>();

        foreach (var group in CardGroups.All)
        {
            if (group.Kind != GroupKind.Marker)
                continue;

            foreach (var row in CardGroupTable.Rows)
            {
                if (!string.Equals(row.Group, group.Id, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(row.Pool, pool.GetType().Name, StringComparison.Ordinal))
                    continue;

                declared++;
                if (!ownPoolEntries.Contains(row.Entry))
                    missing.Add(row.Group + ":" + row.Entry);
            }
        }

        if (declared == 0)
            return;

        if (missing.Count == 0)
            Plugin.Logger.Info($"[self-check] pool={pool.GetType().Name} declared={declared} missing=0");
        else
            Plugin.Logger.Warn(
                $"[self-check] pool={pool.GetType().Name} declared={declared} missing={missing.Count} " +
                $"entries={string.Join(",", missing)}");
    }
}
