using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace AutoSlayGate;

/// <summary>
///     跨版本测试门（游戏 0.111.x）。入口做四件事：翻转发行版判定、起宝石注入协程、写日志、装补丁。
///     依据：0.111.1 的 <c>NGame</c> 只在 <c>IsReleaseGame()</c> 为 false 时处理 <c>--autoslay</c> 参数。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Gate
{
    public static void Initialize()
    {
        Log.Info("[gate] AutoSlayGate 载入（0.111 分支），准备翻转 IsReleaseGame");
        new Harmony("prismatic_gem_filter.autoslay_gate").PatchAll(typeof(Gate).Assembly);
        GemInjector.Start();
        Log.Info("[gate] 补丁完成：IsReleaseGame→false，宝石注入已排队，候选集合与发牌结果探针已挂");
    }
}

/// <summary>把发行版判定改回 false。游戏据此才会解析 autoslay 参数。</summary>
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
///     对局内注入棱彩宝石。0.111.1 没有场景脚本，改由机器人开一局，这里轮询到对局存在后给本地玩家发宝石。
///     只用游戏自己的 <c>RelicCmd.Obtain</c>，与调试命令 <c>relic</c> 走同一条路径。
/// </summary>
internal static class GemInjector
{
    private const string GemId = "PRISMATIC_GEM";

    /// <summary>轮询 2400 次 × 500 毫秒 = 20 分钟上限，避免协程永久占线程。</summary>
    private const int MaxPolls = 2400;

    public static void Start()
    {
        // 想跑「不持有宝石」的对照组时，启动前设环境变量 GATE_NO_GEM=1，注入器直接退出。
        if (Environment.GetEnvironmentVariable("GATE_NO_GEM") == "1")
        {
            Log.Info("[gate] GATE_NO_GEM=1，跳过棱彩宝石注入（对照组）");
            return;
        }

        TaskHelper.RunSafely(LoopAsync());
    }

    private static async Task LoopAsync()
    {
        for (var i = 0; i < MaxPolls; i++)
        {
            await Task.Delay(500);
            try
            {
                var manager = RunManager.Instance;
                if (manager is null || !manager.IsInProgress)
                {
                    continue;
                }

                // 0.111.1 的 RunManager.State 是私有的，对外只开放这个调试用取值方法。
                var state = manager.DebugOnlyGetState();
                if (state is null)
                {
                    continue;
                }

                var me = LocalContext.GetMe(state);
                if (me is null)
                {
                    continue;
                }

                var gem = ModelDb.AllRelics.FirstOrDefault(relic => relic.Id.Entry == GemId);
                if (gem is null)
                {
                    Log.Error("[gate] 找不到 PRISMATIC_GEM 模型，注入中止");
                    return;
                }

                if (me.GetRelicById(gem.Id) is not null)
                {
                    Log.Info("[gate] 本地玩家已持有棱彩宝石，注入结束");
                    return;
                }

                Log.Info("[gate] 对局已开始，开始注入棱彩宝石");
                await RelicCmd.Obtain(gem.ToMutable(), me);
                Log.Info("[gate] 棱彩宝石注入完成");
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"[gate] 注入异常：{ex.GetType().Name}: {ex.Message}");
                return;
            }
        }

        Log.Error("[gate] 注入超时：20 分钟内没有等到对局");
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

/// <summary>发牌结果探针：打印一次奖励真正给出的牌。</summary>
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
