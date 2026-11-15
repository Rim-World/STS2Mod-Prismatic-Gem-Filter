using System.Reflection;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace PrismaticGemFilter;

/// <summary>
///     界面文案的取词入口。
///     <para>
///         三份 JSON 作为嵌入资源打进本程序集，用 RitsuLib 的 <see cref="I18N" /> 加载。
///         语言由 <c>LocManager.Instance.Language</c> 决定：简体中文取 <c>zhs</c>，繁体中文取 <c>zht</c>，
///         其余语言取 <c>eng</c>。缺当前语言的条目时回退 <c>eng</c> 的条目。规则见 SP-022。
///     </para>
///     <para>
///         不放进模组目录的 <c>localization/</c> 下，因为游戏只把模组本地化并入已有的游戏表；
///         本模组的键不属于任何游戏表。且嵌入资源不需要额外文件，符合单文件交付形态。
///     </para>
/// </summary>
internal static class Localization
{
    /// <summary>嵌入资源的逻辑名前缀，与项目文件里的 <c>LogicalName</c> 一致。</summary>
    private const string ResourceFolder = "PrismaticGemFilter.localization";

    private static readonly I18N? Texts = Create();

    /// <summary>
    ///     取一条界面文案。<paramref name="fallback" /> 必须是英文，这样资源缺失时最差也只退回英语，
    ///     不会出现键名。见 SP-022。
    /// </summary>
    public static ModSettingsText Text(string key, string fallback) =>
        Texts is null ? ModSettingsText.Literal(fallback) : ModSettingsText.I18N(Texts, key, fallback);

    /// <summary>同上，但取回已经解析好的字符串，用于拼接说明文字。</summary>
    public static string Resolve(string key, string fallback) =>
        Texts is null ? fallback : Texts.Get(key, fallback);

    /// <summary>
    ///     卡牌名。取游戏自带的 <c>cards</c> 表，键是 <c>&lt;卡牌标识&gt;.title</c>。
    ///     这样 17 种游戏语言下的卡名都正确，本模组不必自己维护卡名。
    /// </summary>
    public static ModSettingsText CardTitle(string entry) =>
        ModSettingsText.LocString("cards", entry + ".title", entry);

    private static I18N? Create()
    {
        try
        {
            return new I18N(
                "PrismaticGemFilter.texts",
                null,
                [ResourceFolder],
                null,
                typeof(Localization).Assembly,
                null);
        }
        catch (Exception ex)
        {
            // 文案加载失败不得阻止设置页注册，最差退回英语字面文本。
            if (Plugin.Logger is not null)
                Plugin.Logger.Error($"[i18n] 界面文案加载失败，本次运行使用英语字面文本: {ex}");
            return null;
        }
    }
}
