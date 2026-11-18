using System.Collections.Generic;
using System.Linq;

namespace PrismaticGemFilter;

/// <summary>分组按什么判定成员。</summary>
internal enum GroupKind
{
    /// <summary>
    ///     按卡牌标记判定。成员名单只是缓存，来源是 <c>3.DOCS/002-E</c> 的检索结果。
    ///     <para>0.0.5 起这类组不含多人限定卡，见 SP-014 与 ADR-009。</para>
    /// </summary>
    Marker,

    /// <summary>按类名枚举。成员名单就是定义本身，见 SP-016。</summary>
    Explicit,
}

/// <summary>一个分组开关的全部静态信息。</summary>
/// <param name="Id">落盘与界面用的标识。发布后不得改动，否则玩家存档里的键会失效。</param>
/// <param name="Section">所属界面分节。多数分节对应一个职业，<c>cross</c> 与 <c>other</c> 不对应具体职业。</param>
/// <param name="Kind">判定方式。</param>
/// <param name="DefaultEnabled">首次启动时的默认勾选状态，见 SP-012。</param>
/// <param name="MultiplayerOnly">成员是否全部为多人模式限定卡。只有这一项为真的组会在豁免页标明。</param>
/// <param name="Members">当前游戏版本存在的卡牌标识，顺序与名单文件一致。0.111 上愚者卡池的成员已被剔除，见 SP-030。</param>
internal sealed record CardGroup(
    string Id,
    string Section,
    GroupKind Kind,
    bool DefaultEnabled,
    bool MultiplayerOnly,
    string[] Members);

/// <summary>
///     15 个分组开关的静态定义。成员来自 <see cref="CardGroupTable" />，该表由名单 CSV 生成。
///     <para>默认值依据 SP-012：11 项勾选、4 项不勾。0.111 上愚者相关的组会被隐藏，见 SP-030。</para>
/// </summary>
internal static class CardGroups
{
    private const string FoolPool = "FoolCardPool";

    /// <summary>界面分节的排列顺序。前六节对应职业，末节收纳不按职业归类的组。</summary>
    public static readonly string[] Sections =
    [
        "necrobinder", "defect", "silent", "regent", "fool", "cross", "other",
    ];

    /// <summary>全部分组，顺序即设置模型里的存储顺序。含当前版本不可见的组。</summary>
    public static readonly CardGroup[] All = Build(GameFeatures.HasFool);

    /// <summary>当前游戏版本要显示的分组。成员为空的组整组隐藏，见 SP-008 与 SP-030。</summary>
    public static readonly CardGroup[] Visible =
        All.Where(static group => group.Members.Length > 0).ToArray();

    private static readonly Dictionary<string, CardGroup> ById =
        All.ToDictionary(static group => group.Id, StringComparer.Ordinal);

    public static CardGroup Get(string id) => ById[id];

    public static bool TryGet(string id, out CardGroup group) => ById.TryGetValue(id, out group!);

    /// <summary>某分节名下当前版本可见的分组，按 <see cref="All" /> 的顺序。</summary>
    public static IEnumerable<CardGroup> OfSection(string section) =>
        Visible.Where(group => group.Section == section);

    private static CardGroup[] Build(bool hasFool)
    {
        // 定义表：标识、分节、判定方式、默认勾选、是否多人专属。成员从生成表里按标识取。
        (string Id, string Section, GroupKind Kind, bool Default, bool MultiplayerOnly)[] definitions =
        [
            ("osty", "necrobinder", GroupKind.Marker, true, false),
            ("summon", "necrobinder", GroupKind.Marker, true, false),
            ("doom", "necrobinder", GroupKind.Marker, true, false),
            ("orb", "defect", GroupKind.Marker, true, false),
            ("focus", "defect", GroupKind.Marker, true, false),
            ("sly", "silent", GroupKind.Marker, true, false),
            ("shiv", "silent", GroupKind.Marker, false, false),
            ("poison", "silent", GroupKind.Marker, false, false),
            ("forge", "regent", GroupKind.Marker, true, false),
            ("star", "regent", GroupKind.Marker, true, false),
            ("arrow", "fool", GroupKind.Marker, false, false),
            ("impale", "fool", GroupKind.Marker, true, false),
            ("sadness", "fool", GroupKind.Explicit, true, false),
            ("multiplayer", "cross", GroupKind.Explicit, false, true),
            ("bugged", "other", GroupKind.Explicit, true, false),
        ];

        var membersById = CardGroupTable.Rows
            .Where(row => hasFool || !string.Equals(row.Pool, FoolPool, StringComparison.Ordinal))
            .GroupBy(static row => row.Group, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static row => row.Entry).ToArray(),
                StringComparer.Ordinal);

        return definitions
            .Select(definition => new CardGroup(
                definition.Id,
                definition.Section,
                definition.Kind,
                definition.Default,
                definition.MultiplayerOnly,
                membersById.TryGetValue(definition.Id, out var members) ? members : []))
            .ToArray();
    }
}
