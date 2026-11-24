using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
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
///     A8/A9 测试门。
///     <para>
///         A8：翻转发行版判定，并用游戏自己的 <c>RelicCmd.Obtain</c> 给本地玩家注入棱彩宝石。
///         A9：仅在 <c>GATE_A9=1</c> 时挂候选、耗时、随机数与基准探针。
///     </para>
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Gate
{
    public static void Initialize()
    {
        Log.Info("[gate] A8/A9 测试门载入");
        var harmony = new Harmony("prismatic_gem_filter.a8_a9_gate");
        harmony.PatchAll(typeof(Gate).Assembly);
        EmptyFilterPatch.TryInstall(harmony);
        GemInjector.Start();
        Log.Info("[gate] 补丁完成：IsReleaseGame→false，宝石注入已排队");
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
///     对局内注入棱彩宝石。机器人开一局后，轮询到对局存在，再给本地玩家发宝石。
///     只用游戏自己的 <c>RelicCmd.Obtain</c>，与调试命令 <c>relic</c> 走同一条路径。
/// </summary>
internal static class GemInjector
{
    private const string GemId = "PRISMATIC_GEM";

    /// <summary>轮询 2400 次 × 500 毫秒 = 20 分钟上限，避免协程永久占线程。</summary>
    private const int MaxPolls = 2400;

    public static void Start()
    {
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
///     候选集合探针。仅在 <c>GATE_A9=1</c> 时启用，避免完整自动局刷大量候选清单。
///     优先级低于本模组过滤补丁的默认值，后置补丁才会更晚执行。
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), new[] { typeof(Player) })]
internal static class CandidateProbePatch
{
    private static bool Prepare() =>
        Environment.GetEnvironmentVariable("GATE_A9") == "1";

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

/// <summary>测量 <c>GetPossibleCards</c> 从进入到过滤完成的耗时。仅在 A9 时启用。</summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), new[] { typeof(Player) })]
internal static class TimingPatch
{
    [ThreadStatic]
    private static long _start;

    private static bool Prepare() =>
        Environment.GetEnvironmentVariable("GATE_A9") == "1";

    private static void Prefix()
    {
        _start = Stopwatch.GetTimestamp();
    }

    [HarmonyPostfix]
    [HarmonyPriority(300)]
    private static void Postfix(IEnumerable<CardModel> __result)
    {
        try
        {
            var elapsedUs = (Stopwatch.GetTimestamp() - _start) * 1_000_000.0 / Stopwatch.Frequency;
            var count = __result?.Count() ?? 0;
            Log.Info($"[timing] getpossible_us={elapsedUs:F1} count={count}");
        }
        catch (Exception ex)
        {
            Log.Error($"[timing] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>
///     随机数计数探针。记录 <c>PlayerRng.Rewards</c> 的计数在 <c>GetPossibleCards</c>
///     前后的变化。过滤不消耗随机数时，差值为 0。仅在 A9 时启用。
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), new[] { typeof(Player) })]
internal static class RngProbePatch
{
    [ThreadStatic]
    private static int _before;

    [ThreadStatic]
    private static bool _armed;

    private static bool Prepare() =>
        Environment.GetEnvironmentVariable("GATE_A9") == "1";

    private static void Prefix(Player player)
    {
        _before = RewardCounter(player);
        _armed = true;
    }

    [HarmonyPostfix]
    [HarmonyPriority(300)]
    private static void Postfix(Player player)
    {
        if (!_armed)
            return;

        _armed = false;
        var after = RewardCounter(player);
        Log.Info($"[rng] rewards_counter before={_before} after={after} delta={after - _before}");
    }

    private static int RewardCounter(Player player) =>
        player.PlayerRng.Rewards.ToSerializable().counter;
}

/// <summary>
///     性能探针。第一个被过滤的候选集合上，测缓存读取和单次过滤。
///     反射调用包含少量调度开销，结果作为上限使用。仅在 A9 时启用。
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), new[] { typeof(Player) })]
internal static class BenchPatch
{
    private const int Iterations = 10_000;

    private static bool _done;

    private static bool Prepare() =>
        Environment.GetEnvironmentVariable("GATE_A9") == "1";

    [HarmonyPostfix]
    [HarmonyPriority(200)]
    private static void Postfix(IEnumerable<CardModel> __result, Player player)
    {
        if (_done)
            return;

        try
        {
            var candidates = __result as IReadOnlyList<CardModel> ?? __result.ToList();
            if (candidates.Count == 0)
                return;

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == "PrismaticGemFilter");
            if (assembly is null)
            {
                Log.Warn("[bench] PrismaticGemFilter 程序集未加载，跳过");
                return;
            }

            var filterType = assembly.GetType("PrismaticGemFilter.CardFilter", throwOnError: false);
            var indexType = assembly.GetType("PrismaticGemFilter.FilterIndex", throwOnError: false);
            var apply = filterType?.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var get = indexType?.GetMethod(
                "Get",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (apply is null || get is null)
            {
                Log.Warn("[bench] 找不到 CardFilter.Apply 或 FilterIndex.Get，跳过");
                return;
            }

            var ownPool = player.Character.CardPool.AllCardIds
                .Select(id => id.Entry)
                .ToHashSet(StringComparer.Ordinal);
            var exclusions = get.Invoke(null, null) as IReadOnlySet<string>;
            if (exclusions is null || exclusions.Count == 0)
                return;

            _done = true;

            var stopwatch = Stopwatch.StartNew();
            object? first = null;
            for (var i = 0; i < Iterations; i++)
            {
                var current = get.Invoke(null, null);
                first ??= current;
            }
            stopwatch.Stop();
            var getNs = stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0 / Iterations;
            var cachedReference = ReferenceEquals(first, get.Invoke(null, null));

            var args = new object?[] { candidates, ownPool, exclusions, null };
            for (var i = 0; i < 100; i++)
            {
                args[3] = null;
                apply.Invoke(null, args);
            }

            stopwatch.Restart();
            for (var i = 0; i < Iterations; i++)
            {
                args[3] = null;
                apply.Invoke(null, args);
            }
            stopwatch.Stop();
            var applyUs = stopwatch.Elapsed.TotalMilliseconds * 1_000.0 / Iterations;

            Log.Info(
                $"[bench] candidates={candidates.Count} exclusions={exclusions.Count} " +
                $"iterations={Iterations} apply_avg_us={applyUs:F2} get_avg_ns={getNs:F1} " +
                $"cached_reference={cachedReference}");
        }
        catch (Exception ex)
        {
            Log.Error($"[bench] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>
///     空候选回退探针。仅在 <c>GATE_EMPTY=1</c> 时把 <c>CardFilter.Apply</c> 的返回值强制为空，
///     让真实发牌链路进入 <c>kept.Count == 0</c> 的回退分支。
///     本补丁只存在于测试门，不进入模组成品。
/// </summary>
internal static class EmptyFilterPatch
{
    private static bool _installed;

    public static void TryInstall(Harmony harmony)
    {
        if (Environment.GetEnvironmentVariable("GATE_EMPTY") != "1" || _installed)
            return;

        var type = AccessTools.TypeByName("PrismaticGemFilter.CardFilter");
        var method = type is null ? null : AccessTools.Method(type, "Apply");
        if (method is null)
        {
            Log.Error("[gate] GATE_EMPTY=1，但找不到 PrismaticGemFilter.CardFilter.Apply");
            return;
        }

        var postfix = new HarmonyMethod(
            typeof(EmptyFilterPatch).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic));
        harmony.Patch(method, postfix: postfix);
        _installed = true;
        Log.Info("[gate] 空候选回退补丁已挂：CardFilter.Apply 的真实结果替换为空列表");
    }

    private static void Postfix(ref List<CardModel> __result)
    {
        __result = new List<CardModel>();
    }
}
