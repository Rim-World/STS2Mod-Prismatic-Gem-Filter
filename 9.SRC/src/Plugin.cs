using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;

namespace PrismaticGemFilter;

/// <summary>
///     模组入口。游戏按清单 id 找到同名的 <c>PrismaticGemFilter.dll</c>，再调用这里的方法。
///     <para>RitsuLib 自己带 <c>[ModInitializer]</c>，本模组声明了依赖，加载顺序保证它先初始化。</para>
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Plugin
{
    /// <summary>必须与 mod_manifest.json 的 id、程序集文件名三者一致。</summary>
    public const string ModId = "PrismaticGemFilter";

    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        Logger = RitsuLibFramework.CreateLogger(ModId);

        var assembly = Assembly.GetExecutingAssembly();
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        try
        {
            ModSettings.Register();
        }
        catch (Exception ex)
        {
            // 设置失败不得阻止过滤。回落默认模式，发牌仍按默认判据工作。
            Logger.Error($"[settings] 注册失败，本次运行使用默认判定模式: {ex}");
        }

        var patcher = RitsuLibFramework.CreatePatcher(ModId, "main", "candidate filter");
        patcher.RegisterPatch<CardCreationPatch>();

        if (!RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod))
            return;

        Logger.Info(
            $"ready. groups={CardGroups.All.Length} entries={CardGroupTable.Rows.Length} " +
            $"exemptions={ExclusionTable.DefaultExemptionKeys.Count} schema={ModSettings.CurrentSchema} " +
            $"patches={patcher.AppliedPatchCount}/{patcher.RegisteredPatchCount}");
    }

    private static void DisableMod()
    {
        Logger.ErrorNoTrace("候选牌过滤补丁未能应用，本模组不生效。发牌行为与未安装时一致。");
    }
}
