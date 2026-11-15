using System.Collections.Generic;
using System.Linq;

namespace PrismaticGemFilter;

/// <summary>分组按什么判定成员。</summary>
internal enum GroupKind
{
    /// <summary>按卡牌标记判定。成员名单只是缓存，来源是 <c>3.DOCS/002-E</c> 的检索结果。</summary>
    Marker,

    /// <summary>按类名枚举。成员名单就是定义本身，见 SP-016。</summary>
    Explicit,
}

/// <summary>一个分组开关的全部静态信息。</summary>
/// <param name="Id">落盘与界面用的标识。发布后不得改动，否则玩家存档里的键会失效。</param>
/// <param name="Character">所属职业，用于界面分节与文案键。</param>
/// <param name="Kind">判定方式。</param>
/// <param name="DefaultEnabled">首次启动时的默认勾选状态，见 SP-012。</param>
/// <param name="Members">卡牌标识，顺序与名单文件一致。</param>
internal sealed record CardGroup(
    string Id,
    string Character,
    GroupKind Kind,
    bool DefaultEnabled,
    string[] Members);

/// <summary>
///     13 个分组开关的静态定义。成员来自 <see cref="CardGroupTable" />，该表由名单 CSV 生成。
///     <para>默认值依据 SP-012：7 项勾选、6 项不勾。</para>
/// </summary>
internal static class CardGroups
{
    /// <summary>全部职业在界面上的排列顺序。</summary>
    public static readonly string[] Characters =
    [
        "necrobinder", "defect", "silent", "regent", "fool", "cross",
    ];

    /// <summary>全部分组，顺序即界面上的排列顺序。</summary>
    public static readonly CardGroup[] All = Build();

    private static readonly Dictionary<string, CardGroup> ById =
        All.ToDictionary(static group => group.Id, StringComparer.Ordinal);

    public static CardGroup Get(string id) => ById[id];

    public static bool TryGet(string id, out CardGroup group) => ById.TryGetValue(id, out group!);

    /// <summary>某职业名下的分组，按 <see cref="All" /> 的顺序。</summary>
    public static IEnumerable<CardGroup> OfCharacter(string character) =>
        All.Where(group => group.Character == character);

    private static CardGroup[] Build()
    {
        // 定义表：标识、职业、判定方式、默认勾选。成员从生成表里按标识取。
        (string Id, string Character, GroupKind Kind, bool Default)[] definitions =
        [
            ("osty", "necrobinder", GroupKind.Marker, true),
            ("summon", "necrobinder", GroupKind.Marker, true),
            ("doom", "necrobinder", GroupKind.Marker, true),
            ("orb", "defect", GroupKind.Marker, true),
            ("focus", "defect", GroupKind.Marker, true),
            ("sly", "silent", GroupKind.Marker, true),
            ("shiv", "silent", GroupKind.Marker, false),
            ("poison", "silent", GroupKind.Marker, false),
            ("forge", "regent", GroupKind.Marker, false),
            ("star", "regent", GroupKind.Marker, false),
            ("arrow", "fool", GroupKind.Marker, false),
            ("impale", "fool", GroupKind.Marker, true),
            ("multiplayer", "cross", GroupKind.Explicit, false),
        ];

        var membersById = CardGroupTable.Rows
            .GroupBy(static row => row.Group, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static row => row.Entry).ToArray(),
                StringComparer.Ordinal);

        return definitions
            .Select(definition => new CardGroup(
                definition.Id,
                definition.Character,
                definition.Kind,
                definition.Default,
                membersById.TryGetValue(definition.Id, out var members) ? members : []))
            .ToArray();
    }
}
