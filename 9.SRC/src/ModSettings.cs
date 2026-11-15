using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace PrismaticGemFilter;

/// <summary>
///     落盘的设置模型。必须是 <c>class</c> 且有无参构造，属性而非字段。
///     <para>枚举按数字落盘，成员数值不得改动，见 <see cref="FilterMode" /> 的注释。</para>
/// </summary>
internal sealed class SettingsModel
{
    public FilterMode Mode { get; set; } = FilterMode.PrismaticGemOnly;
}

/// <summary>
///     模组设置。当前只有一个判定模式，读写走 RitsuLib 的数据槽与绑定。
///     <para>
///         作用域取 <c>SaveScope.Global</c>。原因：<c>Profile</c> 作用域在档案就绪之前读取会抛异常，
///         两个已找到的样例模组都用了 <c>Global</c>。规则见 SP-018。
///     </para>
/// </summary>
internal static class ModSettings
{
    public const string DataKey = "settings";

    private const string FileName = "settings.json";

    private static bool _registered;
    private static bool _readFailed;

    private static ModDataStore Store => ModDataStore.For(Plugin.ModId);

    private static IModSettingsValueBinding<FilterMode> ModeBinding { get; } =
        ModSettingsBindings.WithDefault<FilterMode>(
            ModSettingsBindings.Global<SettingsModel, FilterMode>(
                Plugin.ModId,
                DataKey,
                static model => model.Mode,
                static (model, value) => model.Mode = value),
            static () => FilterMode.PrismaticGemOnly);

    /// <summary>当前判定模式。未注册或读取失败时回落到默认值，不向外抛异常。</summary>
    public static FilterMode CurrentMode
    {
        get
        {
            if (!_registered)
                return FilterMode.PrismaticGemOnly;

            try
            {
                return ModeBinding.Read();
            }
            catch (Exception ex)
            {
                if (!_readFailed)
                {
                    _readFailed = true;
                    Plugin.Logger.Error($"[settings] 读取判定模式失败，本次运行回落到默认值: {ex}");
                }

                return FilterMode.PrismaticGemOnly;
            }
        }
    }

    /// <summary>注册数据槽与设置页。由 <see cref="Plugin.Initialize" /> 调用。</summary>
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

        RitsuLibFramework.RegisterModSettings(Plugin.ModId, ConfigurePage);

        _registered = true;
        Plugin.Logger.Info($"[settings] registered. mode={CurrentMode}");
    }

    private static void ConfigurePage(ModSettingsPageBuilder page)
    {
        page.WithModDisplayName(ModSettingsText.Literal("棱彩宝石过滤"))
            .WithTitle(ModSettingsText.Literal("棱彩宝石过滤"))
            .WithDescription(ModSettingsText.Literal("选择过滤的生效范围。默认与原版棱彩宝石的作用范围一致。"))
            .AddSection("mode", section =>
            {
                section.WithTitle(ModSettingsText.Literal("生效范围"))
                    .AddEnumChoice<FilterMode>(
                        "mode_choice",
                        ModSettingsText.Literal("判定模式"),
                        ModeBinding,
                        static value => ModSettingsText.Literal(
                            value == FilterMode.PrismaticGemOnly
                                ? "只在棱彩宝石加宽本次发牌时生效"
                                : "只要出现其他职业的牌就生效"),
                        ModSettingsText.Literal(
                            "默认取值不会影响万花筒、海玻璃、色彩哲学家，也不会影响战斗内生成的牌。"),
                        ModSettingsChoicePresentation.Dropdown);
            });
    }
}
