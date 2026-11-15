# 更新日志

本文件遵循 Keep a Changelog 规范。版本号遵循 Semantic Versioning。

## [Unreleased]

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
