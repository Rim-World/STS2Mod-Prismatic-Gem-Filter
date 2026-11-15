namespace PrismaticGemFilter;

/// <summary>
///     判定模式。取值顺序固定，序列化时按值存，不得调整顺序。
/// </summary>
internal enum FilterMode
{
    /// <summary>默认。只在棱彩宝石确实加宽了本次发牌时才过滤，见 <c>4.ADR/008</c>。</summary>
    PrismaticGemOnly = 0,

    /// <summary>只要候选里出现玩家职业卡池之外的牌就过滤，不看遗物。</summary>
    AnyWidenedPool = 1,
}
