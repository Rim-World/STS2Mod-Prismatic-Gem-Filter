using System.Collections.Generic;
using System.Linq;
using System.Threading;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace PrismaticGemFilter;

/// <summary>
///     落盘的设置模型。必须是 <c>class</c> 且有无参构造，属性而非字段。
///     <para>枚举按数字落盘，成员数值不得改动，见 <see cref="FilterMode" /> 的注释。</para>
///     <para>
///         唯一筛选状态是 <see cref="Filtered" />：元素为已排除的卡牌标识。
///         它同时是分组开关与单卡豁免的唯一来源。见 SP-031 与 <c>4.ADR/015</c>。
///     </para>
///     <para>
///         <see cref="Filtered" /> 为 <c>null</c> 表示玩家从未改过，此时取代码里的默认集合。
///         这样加新分组、改默认值都不需要迁移旧存档。
///     </para>
///     <para>
///         <see cref="SchemaVersion" /> 是架构版本。它与模组当前的架构版本不一致时，启动会整体重置为默认值，
///         见 SP-024。旧版本的文件没有这个字段，读出来是 0，因此这里不设初始值：
///         新建数据的版本号由注册时的 <c>defaultFactory</c> 写入。
///     </para>
/// </summary>
internal sealed class SettingsModel
{
    public int SchemaVersion { get; set; }

    public FilterMode Mode { get; set; } = FilterMode.PrismaticGemOnly;

    public bool MasterEnabled { get; set; } = true;

    /// <summary>已排除的卡牌标识。null 表示从未改过，取默认集合。</summary>
    public List<string>? Filtered { get; set; }
}

/// <summary>某一时刻生效的设置。判定与建索引只读这里，避免每张牌都去查数据槽。</summary>
internal sealed class SettingsSnapshot
{
    public required FilterMode Mode { get; init; }

    public required bool MasterEnabled { get; init; }

    public required IReadOnlySet<string> FilteredKeys { get; init; }

    /// <summary>
    ///     该卡是否被排除。这是设置层的唯一卡牌状态，不含总开关、判定模式、本职业卡池门控与空结果回退。
    ///     见 SP-031 与 SP-036。
    /// </summary>
    public bool IsFiltered(string entry) => FilteredKeys.Contains(entry);
}

/// <summary>
///     模组设置。读写全部走 RitsuLib 的绑定，不直接碰数据槽，这样界面上的一次改动会同时更新内存与存档。
///     <para>
///         作用域取 <c>SaveScope.Global</c>。原因：<c>Profile</c> 作用域在档案就绪之前读取会抛异常，
///         两个已找到的样例模组都用了 <c>Global</c>。规则见 SP-018。
///     </para>
///     <para>架构与求值规则见 <c>2.PLANS/014-M-卡牌筛选状态唯一化.md</c> 与 <c>4.ADR/015</c>。</para>
/// </summary>
internal static class ModSettings
{
    public const string DataKey = "settings";

    /// <summary>
    ///     当前设置架构版本。改动默认值、改名单、改落盘结构时递增，旧档会在启动时被重置为默认值。见 SP-024。
    ///     <para>
    ///         0.0.8 升到 2：默认豁免清单多了一条。存档里的豁免清单是全量快照，不重置的话老玩家拿不到新默认值。
    ///     </para>
    ///     <para>
    ///         0.0.11 升到 3：默认豁免新增洁净、唤起、血肉戏法，暴涨由球牌组移到集中牌组。
    ///         旧的 <c>orb:BULK_UP</c> 已无效，新的 <c>focus:BULK_UP</c> 又不在老档里，必须整份重置。
    ///     </para>
    /// <para>
    ///         0.0.12 升到 4：豁免键由 <c>分组标识:卡牌标识</c> 改为裸卡牌标识，同一张牌的豁免状态跨组共享。
    ///         旧键全部失效；暴涨也移回球牌组。见 SP-013 与 ADR-014。
    ///     </para>
    ///     <para>
    ///         0.0.13 升到 5：<c>Groups</c> 与 <c>Exemptions</c> 合并为 <c>Filtered</c>。
    ///         分组开关不再单独落盘，改为排除集合的聚合视图与批量命令。旧档整份重置。见 SP-031 与 ADR-015。
    ///     </para>
    /// </summary>
    public const int CurrentSchema = 5;

    private const string FileName = "settings.json";

    private static readonly Dictionary<string, IModSettingsValueBinding<bool>> GroupBindings =
        new(StringComparer.Ordinal);

    private static readonly Dictionary<string, IModSettingsValueBinding<bool>> CardBindings =
        new(StringComparer.Ordinal);

    private static bool _registered;
    private static bool _readFailed;
    private static int _version;
    private static SettingsSnapshot? _cached;
    private static int _cachedVersion = -1;

    private static ModDataStore Store => ModDataStore.For(Plugin.ModId);

    /// <summary>设置的版本号。每次写入自增，用来让快照与排除索引失效。</summary>
    public static int Version => Volatile.Read(ref _version);

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

    /// <summary>排除集合的父绑定。元素是卡牌标识，缺省时取默认集合。</summary>
    private static IModSettingsValueBinding<List<string>> FilteredBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SettingsModel, List<string>>(
                Plugin.ModId,
                DataKey,
                static model => model.Filtered ?? new List<string>(ExclusionTable.DefaultExcludedKeys),
                static (model, value) =>
                {
                    model.Filtered = value;
                    Invalidate();
                }),
            static () => new List<string>(ExclusionTable.DefaultExcludedKeys));

    /// <summary>当前生效的设置。设置未注册或读取失败时返回代码里的默认值。</summary>
    public static SettingsSnapshot Current
    {
        get
        {
            var cached = Volatile.Read(ref _cached);
            if (cached is not null && Volatile.Read(ref _cachedVersion) == Version)
                return cached;

            var snapshot = Build();
            Volatile.Write(ref _cached, snapshot);
            Volatile.Write(ref _cachedVersion, _version);
            return snapshot;
        }
    }

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
                defaultFactory: static () => new SettingsModel { SchemaVersion = CurrentSchema },
                autoCreateIfMissing: true);
        }

        BuildBindings();
        ResetIfOutdated();

        RitsuLibFramework.RegisterModSettings(Plugin.ModId, ConfigurePage);

        // 每个可见分组一个豁免子页。当前版本不存在的组整组隐藏，不注册页面，见 SP-030。
        foreach (var group in CardGroups.Visible)
            RitsuLibFramework.RegisterModSettings(
                Plugin.ModId,
                page => ConfigureExemptionPage(page, group),
                ExemptionPageId(group.Id));

        _registered = true;
        Invalidate();

        var snapshot = Current;
        var visibleFiltered = new HashSet<string>(StringComparer.Ordinal);
        var visibleEntries = 0;
        foreach (var group in CardGroups.Visible)
        {
            visibleEntries += group.Members.Length;
            foreach (var entry in group.Members)
            {
                if (snapshot.IsFiltered(entry))
                    visibleFiltered.Add(entry);
            }
        }

        Plugin.Logger.Info(
            $"[settings] registered. mode={snapshot.Mode} master={snapshot.MasterEnabled} " +
            $"groups={CardGroups.Visible.Length} entries={visibleEntries} filtered={visibleFiltered.Count} " +
            $"stored={snapshot.FilteredKeys.Count} pages={1 + CardGroups.Visible.Length}");
    }

    /// <summary>让快照与排除索引失效。所有写入路径都必须经过这里。</summary>
    private static void Invalidate()
    {
        Interlocked.Increment(ref _version);
        Volatile.Write(ref _cached, null);
        FilterIndex.Invalidate();
    }

    /// <summary>
    ///     架构版本不一致时把全部设置改写为当前默认值。见 SP-024。
    ///     <para>
    ///         写入是一次 <c>Modify</c> 加一次 <c>Save</c>，只落一次盘。架构版本号放在最后写，
    ///         中途崩掉时下次启动会重新走一遍，不会留下「标了新版本但值还是旧的」的档。
    ///     </para>
    /// </summary>
    private static void ResetIfOutdated()
    {
        int stored;
        try
        {
            stored = Store.Get<SettingsModel>(DataKey).SchemaVersion;
        }
        catch (Exception ex)
        {
            Plugin.Logger.Warn($"[settings] 读取架构版本失败，跳过重置检查: {ex.Message}");
            return;
        }

        if (stored == CurrentSchema)
            return;

        try
        {
            Store.Modify<SettingsModel>(DataKey, model =>
            {
                model.Mode = FilterMode.PrismaticGemOnly;
                model.MasterEnabled = true;
                model.Filtered = new List<string>(ExclusionTable.DefaultExcludedKeys);
                model.SchemaVersion = CurrentSchema;
            });
            Store.Save(DataKey);

            Invalidate();

            Plugin.Logger.Info(
                $"[settings] reset. schema {stored} -> {CurrentSchema}, " +
                $"filtered={ExclusionTable.DefaultExcludedKeys.Count} " +
                $"explicitExemptions={ExclusionTable.ExplicitExemptionKeys.Count}");
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error(
                $"[settings] 重置设置失败，本次运行按默认筛选状态工作，存档仍可能是旧架构: {ex}");
        }
    }

    private static SettingsSnapshot Build()
    {
        if (!_registered)
            return Defaults();

        try
        {
            var filtered = FilteredBinding.Read();

            return new SettingsSnapshot
            {
                Mode = ModeBinding.Read(),
                MasterEnabled = MasterBinding.Read(),
                FilteredKeys = new HashSet<string>(filtered, StringComparer.Ordinal),
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
        return new SettingsSnapshot
        {
            Mode = FilterMode.PrismaticGemOnly,
            MasterEnabled = true,
            FilteredKeys = new HashSet<string>(ExclusionTable.DefaultExcludedKeys, StringComparer.Ordinal),
        };
    }

    private static void BuildBindings()
    {
        foreach (var group in CardGroups.All)
        {
            var id = group.Id;
            GroupBindings[id] = ModSettingsBindings.WithDefault(
                ModSettingsBindings.Project<List<string>, bool>(
                    FilteredBinding,
                    "group_" + id,
                    list => FilterState.IsGroupFiltered(list, group),
                    (list, filtered) => FilterState.SetGroup(list, group, filtered)),
                () => FilterState.IsGroupFiltered(ExclusionTable.DefaultExcludedKeys, group));

            foreach (var entry in group.Members)
            {
                if (!CardBindings.ContainsKey(entry))
                    CardBindings[entry] = CreateCardBinding(entry);
            }
        }
    }

    private static IModSettingsValueBinding<bool> CreateCardBinding(string entry)
    {
        return ModSettingsBindings.WithDefault(
            ModSettingsBindings.Project<List<string>, bool>(
                FilteredBinding,
                entry,
                list => !FilterState.IsFiltered(list, entry),
                (list, exempted) => FilterState.SetExempt(list, entry, exempted)),
            () => !ExclusionTable.IsDefaultExcluded(entry));
    }

    private static string ExemptionPageId(string groupId) => "exempt_" + groupId;

    private static void ConfigurePage(ModSettingsPageBuilder page)
    {
        page.WithModDisplayName(Localization.Text("pgf.mod.name", "Prismatic Gem Filter"))
            .WithTitle(Localization.Text("pgf.page.title", "Prismatic Gem Filter"))
            .WithDescription(Localization.Text("pgf.page.desc",
                "Prismatic Gem lets card rewards offer cards from other classes. " +
                "A group switch acts on the whole group: off keeps every card, on excludes every card. " +
                "Adjust individual cards on the group's exempt page."))
            .AddSection("effect_scope", AddEffectScopeSection);

        foreach (var section in CardGroups.Sections)
        {
            var sectionId = section;
            var groups = CardGroups.OfSection(sectionId).ToArray();
            if (groups.Length == 0)
                continue;

            page.AddSection("groups_" + sectionId, builder => AddGroupSection(builder, sectionId, groups));
        }
    }

    private static void AddEffectScopeSection(ModSettingsSectionBuilder section)
    {
        section.WithTitle(Localization.Text("pgf.section.scope", "Master switch"))
            .AddToggle(
                "master",
                Localization.Text("pgf.master.name", "Enable filtering"),
                MasterBinding,
                Localization.Text("pgf.master.desc", "Off makes this mod do nothing."))
            .AddEnumChoice<FilterMode>(
                "mode_choice",
                Localization.Text("pgf.mode.name", "When to filter"),
                ModeBinding,
                static value => value == FilterMode.PrismaticGemOnly
                    ? Localization.Text("pgf.mode.gem", "Only while Prismatic Gem applies")
                    : Localization.Text("pgf.mode.any", "Whenever another class's card appears"),
                Localization.Text("pgf.mode.desc", "Keep the default value if you are not sure."),
                ModSettingsChoicePresentation.Dropdown);
    }

    /// <summary>
    ///     一个界面分节：标题、一句该节的归属说明，下挂本节的若干分组开关。
    ///     <para>分节名多数就是职业标识，<c>cross</c> 与 <c>other</c> 不对应职业，文案键同形。</para>
    /// </summary>
    private static void AddGroupSection(
        ModSettingsSectionBuilder section,
        string sectionId,
        CardGroup[] groups)
    {
        section.WithTitle(Localization.Text("pgf.char." + sectionId, sectionId))
            .WithDescription(Localization.Text("pgf.section." + sectionId, sectionId));

        foreach (var group in groups)
        {
            var id = group.Id;
            section.AddToggle("group_" + id, ExcludeLabel(id), GroupBindings[id], GroupDescription(group))
                .AddSubpage(
                    "exempt_" + id,
                    ModSettingsText.Dynamic(() =>
                        Localization.Resolve("pgf.exempt.row", "Exempt cards") + " · " +
                        Localization.Resolve("pgf.group." + id + ".name", id)),
                    ExemptionPageId(id),
                    Localization.Text("pgf.exempt.button", "Open"),
                    Localization.Text("pgf.exempt.row.desc", "Choose the cards to keep."));
        }
    }

    /// <summary>
    ///     分组开关的标题。开关的含义是「排除本组的牌」，标题因此由前缀加组名拼成：
    ///     中文是「排除」加「奥斯提牌」，英文是 <c>Exclude </c> 加 <c>Osty cards</c>。
    ///     <para>两段都取本地化文案，切换语言时整行跟着变，不需要每种语言各写一份标题。</para>
    /// </summary>
    private static ModSettingsText ExcludeLabel(string groupId) =>
        ModSettingsText.Dynamic(() =>
            Localization.Resolve("pgf.exclude.prefix", "Exclude ") +
            Localization.Resolve("pgf.group." + groupId + ".name", groupId));

    /// <summary>
    ///     分组开关的说明：一句短定义、本组成员张数、当前已排除张数。
    ///     <para>张数由名单算出，不写死在文案里，名单变动时说明自动跟着变。</para>
    ///     <para>状态由 <see cref="FilteredBinding" /> 派生，并通过同一个父绑定刷新。见 SP-035。</para>
    ///     <para>英语的张数分单复数两种写法，其余语言共用一条。见 SP-028 的注记。</para>
    /// </summary>
    private static ModSettingsText GroupDescription(CardGroup group) =>
        ModSettingsText.Dynamic(() =>
        {
            var count = group.Members.Length;
            var countKey = count == 1 ? "pgf.count.one" : "pgf.count";
            var countText = string.Format(Localization.Resolve(countKey, "{0} cards"), count);
            var filtered = FilterState.CountFiltered(Current.FilteredKeys, group);
            var stateText = string.Format(
                Localization.Resolve("pgf.group.filtered", "{0}/{1} filtered"), filtered, count);

            return Localization.Resolve("pgf.group." + group.Id + ".desc", group.Id) + " · " +
                   countText + " · " + stateText;
        }, FilteredBinding);

    private static void ConfigureExemptionPage(ModSettingsPageBuilder page, CardGroup group)
    {
        var id = group.Id;

        page.AsChildOf(Plugin.ModId)
            .WithSidebarVisibleOnlyWhenActive()
            .WithTitle(ModSettingsText.Dynamic(() =>
                Localization.Resolve("pgf.exempt.page.title", "Exempt cards") + " · " +
                Localization.Resolve("pgf.group." + id + ".name", id)))
            .WithDescription(Localization.Text("pgf.exempt.page.desc",
                "Checked keeps the card; unchecked excludes it. A group switch changes every card in that group."))
            .AddSection("cards", section =>
            {
                section.WithTitle(Localization.Text("pgf.exempt.section", "Cards in this group"));

                foreach (var entry in group.Members)
                    section.AddToggle(
                        "card_" + entry,
                        Localization.CardTitle(entry),
                        CardBindings[entry],
                        BuildCardHint(group, entry));
            });
    }

    /// <summary>
    ///     卡牌说明的两类附加信息：多人限定卡提示、同属其他分组的提示。
    ///     两者都没有时返回 <c>null</c>，界面不显示说明行。
    /// </summary>
    private static ModSettingsText? BuildCardHint(CardGroup group, string entry)
    {
        var others = CardGroups.Visible
            .Where(other => !ReferenceEquals(other, group) && Array.IndexOf(other.Members, entry) >= 0)
            .Select(static other => other.Id)
            .ToArray();

        // 成员全是多人限定卡的组才加这条提示。判定按组的静态标记，不按判定方式推断：
        // 0.0.7 起显式名单组不止一个，不能再用 Kind 代表「多人专属」。
        var isMultiplayerOnly = group.MultiplayerOnly;

        if (others.Length == 0 && !isMultiplayerOnly)
            return null;

        return ModSettingsText.Dynamic(() =>
        {
            var parts = new List<string>(2);

            if (isMultiplayerOnly)
                parts.Add(Localization.Resolve("pgf.hint.multiplayer",
                    "Appears only in multiplayer."));

            if (others.Length > 0)
            {
                var separator = Localization.Resolve("pgf.list.sep", ", ");
                var names = string.Join(
                    separator,
                    others.Select(other => Localization.Resolve("pgf.group." + other + ".name", other)));
                parts.Add(string.Format(
                    Localization.Resolve("pgf.hint.alsoIn", "Also in: {0}"),
                    names));
            }

            return string.Join("\n", parts);
        });
    }
}
