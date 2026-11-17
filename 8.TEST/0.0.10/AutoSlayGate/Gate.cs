using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace AutoSlayGate;

/// <summary>
///     测试门。入口只做两件事：装 Harmony 补丁，写一行日志。
///     依据：父级 AGENTS.md「测试」一节，发行版必须让 <c>NGame.IsReleaseGame</c> 返回 false 才会处理
///     scenario 与 autoslay 启动参数。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Gate
{
    public static void Initialize()
    {
        Log.Info("[gate] AutoSlayGate 载入，准备翻转 IsReleaseGame");
        new Harmony("prismatic_gem_filter.autoslay_gate").PatchAll(typeof(Gate).Assembly);
        Log.Info("[gate] 补丁完成：IsReleaseGame→false，候选集合与发牌结果探针已挂");
    }
}

/// <summary>把发行版判定改回 false。游戏据此才会解析 scenario 参数。</summary>
[HarmonyPatch(typeof(NGame), nameof(NGame.IsReleaseGame))]
internal static class ReleaseFlagPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result = false;
    }
}

/// <summary>
///     候选集合探针，打印过滤之后的集合。
///     <para>
///         优先级必须低于本模组过滤补丁的默认值，后置补丁才会更晚执行。
///         实测 600 比默认 400 更早执行，因此这里取 300。
///     </para>
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), new[] { typeof(Player) })]
internal static class CandidateProbePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(300)]
    private static void Postfix(IEnumerable<CardModel> __result)
    {
        try
        {
            if (__result is null)
            {
                Log.Info("[probe] result=null");
                return;
            }

            var ids = __result.Select(card => card.Id.Entry).ToList();
            Log.Info($"[probe] count={ids.Count} ids={string.Join(",", ids)}");
        }
        catch (Exception ex)
        {
            Log.Error($"[probe] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>发牌结果探针：打印一次战斗奖励真正给出的三张牌。</summary>
[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward),
    new[] { typeof(Player), typeof(int), typeof(CardCreationOptions) })]
internal static class RewardProbePatch
{
    [HarmonyPostfix]
    private static void Postfix(IEnumerable<CardCreationResult> __result)
    {
        try
        {
            var ids = __result?.Select(result => result.Card.Id.Entry).ToList() ?? new List<string>();
            Log.Info($"[reward] count={ids.Count} ids={string.Join(",", ids)}");
        }
        catch (Exception ex)
        {
            Log.Error($"[reward] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
