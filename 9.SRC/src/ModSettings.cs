using System.Collections.Generic;
using System.Linq;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace PrismaticGemFilter;

/// <summary>
///     落盘的设置模型。必须是 <c>class</c> 且有无参构造，属性而非字段。
///     <para>枚举按数字落盘，成员数值不得改动，见 <see cref="FilterMode" /> 的注释。</para>
///     <para>
///         <see cref="Groups" /> 与 <see cref="Exemptions" /> 为 <c>null</c> 表示该组设置从未被玩家改过，
///         此时取代码里的默认值。这样加新分组、改默认值都不需要迁移旧存档。
///     </para>
/// </summary>
internal sealed class SettingsModel
{
    public FilterMode Mode { get; set; } = FilterMode.PrismaticGemOnly;

    public bool MasterEnabled { get; set; } = true;

    public Dictionary<string, bool>? Groups { get; set; }

    public List<string>? Exemptions { get; set; }
}

/// <summary>某一时刻生效的设置。判定与建索引只读这里，避免每张牌都去查数据槽。</summary>
internal sealed class SettingsSnapshot
{
    public required FilterMode Mode { get; init; }

    public required bool MasterEnabled { get; init; }

    public required IReadOnlySet<string> EnabledGroups { get; init; }

    public required IReadOnlySet<string> ExemptionKeys { get; init; }

    /// <summary>某卡在某分组下是否豁免。键是 <c>分组标识:卡牌标识</c>。</summary>
    public bool IsExempt(string group, string entry) => ExemptionKeys.Contains(ExclusionTable.Key(group, entry));
}

/// <summary>
///     模组设置。读写全部走 RitsuLib 的绑定，不直接碰数据槽，这样界面上的一次改动会同时更新内存与存档。
///     <para>
///         作用域取 <c>SaveScope.Global</c>。原因：<c>Profile</c> 作用域在档案就绪之前读取会抛异常，
///         两个已找到的样例模组都用了 <c>Global</c>。规则见 SP-018。
///     </para>
///     <para>架构与求值规则见 <c>2.PLANS/004-M-分组开关与设置页.md</c>。</para>
/// </summary>
internal static class ModSettings
{
    public const string DataKey = "settings";

    private const string FileName = "settings.json";

    private static readonly Dictionary<string, IModSettingsValueBinding<bool>> GroupBindings =
        new(StringComparer.Ordinal);

    private static readonly Dictionary<string, IModSettingsValueBinding<bool>> CardBindings =
        new(StringComparer.Ordinal);

    private static bool _registered;
    private static bool _readFailed;
    private static int _version;
    private static SettingsSnapshot? _cached;

    private static ModDataStore Store => ModDataStore.For(Plugin.ModId);

    /// <summary>设置的版本号。每次写入自增，用来让快照与排除索引失效。</summary>
    public static int Version => _version;

    private static IModSettingsValueBinding<FilterMode> ModeBinding { get; } =
        ModSettingsBindings.WithDefault<FilterMode>(
            ModSettingsBindings.Global<SettingsModel, FilterMode>(
                Plugin.ModId,
                DataKey,
                static model => model.Mode,
                static (model, value) =>
                {
                    model.Mode = value;
                    Invalidate();
                }),
            static () => FilterMode.PrismaticGemOnly);

    private static IModSettingsValueBinding<bool> MasterBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, bool>(
                Plugin.ModId,
                DataKey,
                static model => model.MasterEnabled,
                static (model, value) =>
                {
                    model.MasterEnabled = value;
                    Invalidate();
                }),
            static () => true);

    /// <summary>豁免清单的父绑定。元素是 <c>分组标识:卡牌标识</c>，缺省时取默认清单。</summary>
    private static IModSettingsValueBinding<List<string>> ExemptionBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, List<string>>(
                Plugin.ModId,
                DataKey,
                static model => model.Exemptions ?? new List<string>(ExclusionTable.DefaultExemptionKeys),
                static (model, value) =>
                {
                    model.Exemptions = value;
                    Invalidate();
                }),
            static () => new List<string>(ExclusionTable.DefaultExemptionKeys));

    /// <summary>当前生效的设置。设置未注册或读取失败时返回代码里的默认值。</summary>
    public static SettingsSnapshot Current
    {
        get
        {
            var cached = _cached;
            if (cached is not null && _cachedVersion == _version)
                return cached;

            var snapshot = Build();
            _cached = snapshot;
            _cachedVersion = _version;
            return snapshot;
        }
    }

    private static int _cachedVersion = -1;

    /// <summary>注册数据槽、绑定与全部设置页面。由 <see cref="Plugin.Initialize" /> 调用。</summary>
    public static void Register()
    {
        if (_registered)
            return;

        using (RitsuLibFramework.BeginModDataRegistration(Plugin.ModId, true))
        {
            Store.Register<SettingsModel>(
                DataKey,
                FileName,
                SaveScope.Global,
                defaultFactory: () => new SettingsModel(),
                autoCreateIfMissing: true);
        }

        BuildBindings();

        RitsuLibFramework.RegisterModSettings(Plugin.ModId, ConfigurePage);

        // 每个分组一个豁免子页。父页先注册，子页用 AsChildOf 挂在它下面。
        foreach (var group in CardGroups.All)
            RitsuLibFramework.RegisterModSettings(
                Plugin.ModId,
                page => ConfigureExemptionPage(page, group),
                ExemptionPageId(group.Id));

        _registered = true;
        Invalidate();

        var snapshot = Current;
        Plugin.Logger.Info(
            $"[settings] registered. mode={snapshot.Mode} master={snapshot.MasterEnabled} " +
            $"groups={snapshot.EnabledGroups.Count}/{CardGroups.All.Length} exemptions={snapshot.ExemptionKeys.Count} " +
            $"pages={1 + CardGroups.All.Length}");
    }

    /// <summary>让快照与排除索引失效。所有写入路径都必须经过这里。</summary>
    private static void Invalidate()
    {
        _version++;
        FilterIndex.Invalidate();
    }

    private static SettingsSnapshot Build()
    {
        if (!_registered)
            return Defaults();

        try
        {
            var enabled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in CardGroups.All)
            {
                if (GroupBindings.TryGetValue(group.Id, out var binding) && binding.Read())
                    enabled.Add(group.Id);
            }

            var exemptions = ExemptionBinding.Read();

            return new SettingsSnapshot
            {
                Mode = ModeBinding.Read(),
                MasterEnabled = MasterBinding.Read(),
                EnabledGroups = enabled,
                ExemptionKeys = new HashSet<string>(exemptions, StringComparer.Ordinal),
            };
        }
        catch (Exception ex)
        {
            if (!_readFailed)
            {
                _readFailed = true;
                Plugin.Logger.Error($"[settings] 读取设置失败，本次运行回落到默认值: {ex}");
            }

            return Defaults();
        }
    }

    private static SettingsSnapshot Defaults()
    {
        var enabled = new HashSet<string>(
            CardGroups.All.Where(static group => group.DefaultEnabled).Select(static group => group.Id),
            StringComparer.Ordinal);

        return new SettingsSnapshot
        {
            Mode = FilterMode.PrismaticGemOnly,
            MasterEnabled = true,
            EnabledGroups = enabled,
            ExemptionKeys = new HashSet<string>(ExclusionTable.DefaultExemptionKeys, StringComparer.Ordinal),
        };
    }

    private static void BuildBindings()
    {
        foreach (var group in CardGroups.All)
        {
            var id = group.Id;
            var defaultValue = group.DefaultEnabled;
            GroupBindings[id] = ModSettingsBindings.WithDefault(
                ModSettingsBindings.Global<SettingsModel, bool>(
                    Plugin.ModId,
                    DataKey,
                    model => ReadGroup(model, id),
                    (model, value) =>
                    {
                        (model.Groups ??= new Dictionary<string, bool>(StringComparer.Ordinal))[id] = value;
                        Invalidate();
                    }),
                () => defaultValue);

            foreach (var entry in group.Members)
                CardBindings[ExclusionTable.Key(id, entry)] = CreateCardBinding(id, entry);
        }
    }

    private static IModSettingsValueBinding<bool> CreateCardBinding(string groupId, string entry)
    {
        var key = ExclusionTable.Key(groupId, entry);

        return ModSettingsBindings.WithDefault(
            ModSettingsBindings.Project<List<string>, bool>(
                ExemptionBinding,
                groupId + "_" + entry,
                list => list.Contains(key, StringComparer.Ordinal),
                (list, exempted) => Toggle(list, key, exempted)),
            () => ExclusionTable.IsDefaultExempt(groupId, entry));
    }

    /// <summary>返回改动后的新清单。不改入参，避免绕过存档。</summary>
    private static List<string> Toggle(List<string> source, string key, bool exempted)
    {
        var copy = new List<string>(source);
        if (exempted)
        {
            if (!copy.Contains(key, StringComparer.Ordinal))
                copy.Add(key);
        }
        else
        {
            copy.RemoveAll(item => string.Equals(item, key, StringComparison.Ordinal));
        }

        return copy;
    }

    private static bool ReadGroup(SettingsModel model, string id)
    {
        if (model.Groups is not null && model.Groups.TryGetValue(id, out var value))
            return value;

        return CardGroups.TryGet(id, out var group) && group.DefaultEnabled;
    }

    private static string ExemptionPageId(string groupId) => "exempt_" + groupId;

    private static void ConfigurePage(ModSettingsPageBuilder page)
    {
        page.WithModDisplayName(Localization.Text("pgf.mod.name", "Prismatic Gem Filter"))
            .WithTitle(Localization.Text("pgf.page.title", "Prismatic Gem Filter"))
            .WithDescription(Localization.Text("pgf.page.desc", "Choose which other-class cards a card reward can offer."))
            .AddSection("effect_scope", AddEffectScopeSection);

        foreach (var character in CardGroups.Characters)
        {
            var characterId = character;
            var groups = CardGroups.OfCharacter(characterId).ToArray();
            if (groups.Length == 0)
                continue;

            page.AddSection("groups_" + characterId, section => AddCharacterSection(section, characterId, groups));
        }
    }

    private static void AddEffectScopeSection(ModSettingsSectionBuilder section)
    {
        section.WithTitle(Localization.Text("pgf.section.scope", "Scope"))
            .AddToggle(
                "master",
                Localization.Text("pgf.master.name", "Enable filtering"),
                MasterBinding,
                Localization.Text("pgf.master.desc", "Off leaves every card reward unchanged."))
            .AddEnumChoice<FilterMode>(
                "mode_choice",
                Localization.Text("pgf.mode.name", "When it applies"),
                ModeBinding,
                static value => value == FilterMode.PrismaticGemOnly
                    ? Localization.Text("pgf.mode.gem", "Only when Prismatic Gem widens this reward")
                    : Localization.Text("pgf.mode.any", "Whenever another class's card appears"),
                Localization.Text("pgf.mode.desc",
                    "The default matches what the vanilla relic itself affects."),
                ModSettingsChoicePresentation.Dropdown);
    }

    private static void AddCharacterSection(
        ModSettingsSectionBuilder section,
        string character,
        CardGroup[] groups)
    {
        section.WithTitle(Localization.Text("pgf.char." + character, character));

        foreach (var group in groups)
        {
            var id = group.Id;
            section.AddToggle(
                    "group_" + id,
                    Localization.Text("pgf.group." + id + ".name", id),
                    GroupBindings[id],
                    Localization.Text("pgf.group." + id + ".desc",
                        $"{group.Members.Length} cards. Unchecked keeps this group unchanged."))
                .AddSubpage(
                    "exempt_" + id,
                    ModSettingsText.Dynamic(() =>
                        Localization.Resolve("pgf.exempt.row", "Exemptions") + " · " +
                        Localization.Resolve("pgf.group." + id + ".name", id)),
                    ExemptionPageId(id),
                    Localization.Text("pgf.exempt.button", "Open"),
                    Localization.Text("pgf.exempt.row.desc",
                        "Tick the cards this group must not exclude."));
        }
    }

    private static void ConfigureExemptionPage(ModSettingsPageBuilder page, CardGroup group)
    {
        var id = group.Id;

        page.AsChildOf(Plugin.ModId)
            .WithSidebarVisibleOnlyWhenActive()
            .WithTitle(ModSettingsText.Dynamic(() =>
                Localization.Resolve("pgf.exempt.page.title", "Exemptions") + " · " +
                Localization.Resolve("pgf.group." + id + ".name", id)))
            .WithDescription(Localization.Text("pgf.exempt.page.desc",
                "A ticked card is never excluded by this group alone. Another checked group can still exclude it."))
            .AddSection("cards", section =>
            {
                section.WithTitle(Localization.Text("pgf.exempt.section", "Cards in this group"));

                foreach (var entry in group.Members)
                    section.AddToggle(
                        "card_" + entry,
                        Localization.CardTitle(entry),
                        CardBindings[ExclusionTable.Key(id, entry)],
                        BuildCardHint(group, entry));
            });
    }

    /// <summary>
    ///     卡牌说明的两类附加信息：多人限定卡提示、同属其他分组的提示。
    ///     两者都没有时返回 <c>null</c>，界面不显示说明行。
    /// </summary>
    private static ModSettingsText? BuildCardHint(CardGroup group, string entry)
    {
        var others = CardGroups.All
            .Where(other => !ReferenceEquals(other, group) && Array.IndexOf(other.Members, entry) >= 0)
            .Select(static other => other.Id)
            .ToArray();

        var protectedByMarkerRule =
            group.Kind == GroupKind.Marker && ExclusionTable.MultiplayerProtected.Contains(entry);

        if (others.Length == 0 && !protectedByMarkerRule)
            return null;

        return ModSettingsText.Dynamic(() =>
        {
            var parts = new List<string>(2);

            if (protectedByMarkerRule)
                parts.Add(Localization.Resolve("pgf.hint.multiplayer",
                    "Multiplayer-only card: marker-based groups never exclude it."));

            if (others.Length > 0)
            {
                var separator = Localization.Resolve("pgf.list.sep", ", ");
                var names = string.Join(
                    separator,
                    others.Select(other => Localization.Resolve("pgf.group." + other + ".name", other)));
                parts.Add(string.Format(
                    Localization.Resolve("pgf.hint.alsoIn", "Also in another group: {0}"),
                    names));
            }

            return string.Join("\n", parts);
        });
    }
}
