using System.Collections.Generic;
using System.Threading;

namespace PrismaticGemFilter;

/// <summary>
///     实际过滤池。设置变更后重建一次，发牌时每张候选牌只做一次哈希查表。
///     <para>
///         0.0.14 起池由两份独立状态算出：已开启且当前版本可见的分组，减去全局豁免牌。
///         分组开关与单卡豁免都不直接写池；池只作内存缓存，见 SP-031 至 SP-039 与 <c>4.ADR/016</c>。
///     </para>
///     <para>构建成本模型见 <c>3.DOCS/003-E-豁免与多人卡取证.md</c> 第四节。</para>
/// </summary>
internal static class FilterIndex
{
    private static readonly object Gate = new();

    private static IReadOnlySet<string> _excluded = new HashSet<string>(StringComparer.Ordinal);
    private static int _builtVersion = -1;
    private static int _activeGroups;

    /// <summary>上一次构建时至少排除一张牌的可见组数，供日志使用。</summary>
    public static int ActiveGroups
    {
        get
        {
            Ensure();
            return Volatile.Read(ref _activeGroups);
        }
    }

    /// <summary>取当前的实际过滤池。设置变过则先重建。</summary>
    public static IReadOnlySet<string> Get()
    {
        Ensure();
        return Volatile.Read(ref _excluded);
    }

    /// <summary>本组有多少成员当前位于实际过滤池。组关闭时，跨组牌仍可能被其他组计入。</summary>
    public static int CountFilteredMembers(CardGroup group)
    {
        return FilterState.CountInPool(group.Members, Get());
    }

    /// <summary>作废索引。设置写入后由 <see cref="ModSettings" /> 调用。</summary>
    public static void Invalidate()
    {
        Volatile.Write(ref _builtVersion, -1);
    }

    private static void Ensure()
    {
        var version = ModSettings.Version;
        if (Volatile.Read(ref _builtVersion) == version)
            return;

        lock (Gate)
        {
            if (Volatile.Read(ref _builtVersion) == version)
                return;

            Build(version);
        }
    }

    private static void Build(int version)
    {
        var snapshot = ModSettings.Current;
        var excluded = FilterState.BuildPool(
            CardGroups.Visible,
            snapshot.EnabledGroups,
            snapshot.Exemptions);

        var active = 0;
        foreach (var group in CardGroups.Visible)
        {
            if (!snapshot.IsGroupEnabled(group.Id))
                continue;

            foreach (var entry in group.Members)
            {
                if (snapshot.IsExempt(entry))
                    continue;

                active++;
                break;
            }
        }

        Volatile.Write(ref _excluded, excluded);
        Volatile.Write(ref _activeGroups, active);
        Volatile.Write(ref _builtVersion, version);

        Plugin.Logger.Info(
            $"[index] rebuilt. version={version} groups={active} filtered={excluded.Count}");
    }
}
