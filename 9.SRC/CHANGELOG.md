# 更新日志

本文件遵循 Keep a Changelog 规范。版本号遵循 Semantic Versioning。

## [Unreleased]

## [0.0.2] - 2026-11-15

### Changed

- 「没有排除任何牌」这一支的日志改为按情形分级。候选里出现别的职业的牌时打 `Info`，候选全在本职业卡池时打 `Debug`。原因：游戏把 `Debug` 级别的日志整个丢弃，原写法导致「玩家是本职业、过滤成为空操作」这一情形在日志里完全没有痕迹。
- 日志增加 `otherPool` 字段，取值为候选里不属于玩家职业卡池的张数。大于 0 说明这次发牌被加宽过。
- 部署步骤改为容忍文件占用。游戏开着时 DLL 被锁，原先会让构建整体失败；现在只出警告，构建仍算成功，关掉游戏再构建一次即可部署。

## [0.0.1] - 2026-11-15

最小工程。只实现「奥斯提牌」一组，一个开关恒为开，不含设置页与持久化。

### Added

- 模组入口 `src/Plugin.cs`。接入 RitsuLib 0.6.13，注册补丁器，补丁应用失败时停用本模组。
- 拦截层 `src/CardCreationPatch.cs`。对 `CardCreationOptions.GetPossibleCards(Player)` 打 Harmony 后置补丁，过滤跨角色发牌候选。
- 判定规则 `src/CardFilter.cs`。纯函数，逐张判定保留或排除，输出候选数、排除数、保留数与池门控跳过数。
- 排除名单 `src/ExclusionTable.cs`。奥斯提牌组 12 张，标识形如 `BONE_SHARDS`。
- 空集保护。保留列表为空时放弃本次过滤并打警告。
- 异常兜底。过滤过程任何异常只记日志，不改变候选集合。
- 清单 `mod_manifest.json`。`min_game_version` 为 0.107.1，依赖 `STS2-RitsuLib` 不低 0.6.13。
- 构建脚本内建版本一致性校验：清单 `version` 与项目 `Version` 不一致时构建失败。
- 构建后自动部署到 `mods/PrismaticGemFilter/`。

### Notes

- 编译基线为 0.113.1。运行时只用三版共有的成员。
- 池门控按「候选牌的标识是否属于玩家职业卡池」判定，不使用 `CardModel.Pool` 属性，避免一牌多池时的枚举顺序依赖。
