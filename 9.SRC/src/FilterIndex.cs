using System.Collections.Generic;

namespace PrismaticGemFilter;

/// <summary>
///     排除索引。设置变更后重建一次，发牌时每张候选牌只做一次哈希查表。
///     <para>
///         0.0.4 曾分两个集合，让按标记判定的组不排除多人限定卡。0.0.5 起这类卡不再出现在
///         那些组里，两个集合不可能相交，因此合并为一个。理由见 ADR-009。
///     </para>
///     <para>构建成本模型见 <c>3.DOCS/003-E-豁免与多人卡取证.md</c> 第四节。</para>
/// </summary>
internal static class FilterIndex
{
    private static readonly object Gate = new();

    private static IReadOnlySet<string> _excluded = new HashSet<string>(StringComparer.Ordinal);
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
    public static IReadOnlySet<string> Get()
    {
        Ensure();
        return _excluded;
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
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var enabled = 0;

        foreach (var group in CardGroups.Visible)
        {
            if (!snapshot.EnabledGroups.Contains(group.Id))
                continue;

            enabled++;

            foreach (var entry in group.Members)
            {
                if (snapshot.IsExempt(group.Id, entry))
                    continue;

                excluded.Add(entry);
            }
        }

        _excluded = excluded;
        _enabledGroups = enabled;
        _builtVersion = version;

        Plugin.Logger.Info(
            $"[index] rebuilt. version={version} groups={enabled} excluded={excluded.Count}");
    }
}
