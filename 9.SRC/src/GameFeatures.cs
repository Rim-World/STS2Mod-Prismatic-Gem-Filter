using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Models;

namespace PrismaticGemFilter;

/// <summary>
///     当前游戏版本具备哪些内容。目前只区分「有没有愚者」。
///     <para>
///         0.111 没有愚者角色，设置页里与愚者相关的项必须隐藏；0.113 起全量显示。
///         判据优先看游戏程序集里有没有 <c>FoolCardPool</c> 类型，这是最直接的能力检查；
///         类型探测失败时退回 <c>release_info.json</c> 的版本号。两条都失败时按含愚者处理，
///         宁可多显示，也不在新版本上误藏。
///     </para>
/// </summary>
internal static class GameFeatures
{
    /// <summary>当前游戏版本是否包含愚者内容。</summary>
    public static bool HasFool { get; } = DetectFool();

    private static bool DetectFool()
    {
        try
        {
            var hasFool = typeof(CardModel).Assembly.GetType(
                "MegaCrit.Sts2.Core.Models.CardPools.FoolCardPool",
                throwOnError: false) is not null;
            Plugin.Logger?.Info($"[compat] fool={hasFool} source=type");
            return hasFool;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.Warn($"[compat] 类型探测失败，改用版本号判断: {ex.Message}");
        }

        try
        {
            var version = ReleaseInfoManager.Instance.SemVer;
            if (version is not null)
            {
                var hasFool = version.Major > 0 || version.Minor >= 113;
                Plugin.Logger?.Info(
                    $"[compat] game={version.Major}.{version.Minor}.{version.Patch} " +
                    $"fool={hasFool} source=release_info");
                return hasFool;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.Warn($"[compat] 读取游戏版本失败: {ex.Message}");
        }

        Plugin.Logger?.Warn("[compat] 无法判断是否含愚者内容，按含愚者处理。");
        return true;
    }
}
