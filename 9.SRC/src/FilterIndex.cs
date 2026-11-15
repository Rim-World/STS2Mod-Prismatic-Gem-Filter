using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     排除索引。设置变更后重建一次，发牌时每张候选牌只做两次哈希查表。
///     <para>为什么分两个集合：SP-014 要求多人限定卡不被按标记判定的组排除，而豁免要按分组求值。</para>
///     <para>
///         集合含义：<c>Marker</c> 是 12 个按标记判定的组的排除集合，已扣掉该组豁免与多人限定卡；
///         <c>Explicit</c> 是显式名单组的排除集合，已扣掉该组豁免。两者的成员都可能是同一张卡，
///         任一集合命中即排除。
///     </para>
///     <para>构建成本模型见 <c>3.DOCS/003-E-豁免与多人卡取证.md</c> 第四节。</para>
/// </summary>
internal static class FilterIndex
{
    private static readonly object Gate = new();

    private static IReadOnlySet<string> _marker = new HashSet<string>(StringComparer.Ordinal);
    private static IReadOnlySet<string> _explicit = new HashSet<string>(StringComparer.Ordinal);
    private static int _builtVersion = -1;
    private static int _enabledGroups;

    /// <summary>上一次构建时生效的组数，供日志使用。</summary>
    public static int EnabledGroups
    {
        get
        {
            Ensure();
            return _enabledGroups;
        }
    }

    /// <summary>取当前的排除集合。设置变过则先重建。</summary>
    public static void Get(out IReadOnlySet<string> marker, out IReadOnlySet<string> explicitExclusions)
    {
        Ensure();
        marker = _marker;
        explicitExclusions = _explicit;
    }

    /// <summary>作废索引。设置写入后由 <see cref="ModSettings" /> 调用。</summary>
    public static void Invalidate()
    {
        lock (Gate)
        {
            _builtVersion = -1;
        }
    }

    private static void Ensure()
    {
        var version = ModSettings.Version;
        if (_builtVersion == version)
            return;

        lock (Gate)
        {
            if (_builtVersion == version)
                return;

            Build(version);
        }
    }

    private static void Build(int version)
    {
        var snapshot = ModSettings.Current;
        var marker = new HashSet<string>(StringComparer.Ordinal);
        var explicitExclusions = new HashSet<string>(StringComparer.Ordinal);
        var enabled = 0;

        foreach (var group in CardGroups.All)
        {
            if (!snapshot.EnabledGroups.Contains(group.Id))
                continue;

            enabled++;
            var target = group.Kind == GroupKind.Explicit ? explicitExclusions : marker;

            foreach (var entry in group.Members)
            {
                if (snapshot.IsExempt(group.Id, entry))
                    continue;

                target.Add(entry);
            }
        }

        // SP-014：多人限定卡不进入按标记判定的排除集合。显式名单组不受此限。
        marker.ExceptWith(ExclusionTable.MultiplayerProtected);

        _marker = marker;
        _explicit = explicitExclusions;
        _enabledGroups = enabled;
        _builtVersion = version;

        Plugin.Logger.Info(
            $"[index] rebuilt. version={version} groups={enabled} marker={marker.Count} explicit={explicitExclusions.Count}");
    }
}
