# 软件设计文档

## 状态

- 项目版本：0.0.11
- 代码状态：`9.SRC` 已实现 15 个分组开关（0.111 上可见 11 个）、总开关、分组豁免清单、单集合排除索引、设置页、界面本地化、配置重置与按游戏版本能力显隐。
- 说明：SDD 的每条设计描述都对应一个源码符号。未实现的组件只登记职责，标注「未建立」。

## 上下文与范围

本项目是 Slay the Spire 2 的模组，进程内注入，通过 Harmony 后置修改发牌候选集合。它不新增加载器，不修改游戏文件。

系统边界：

- 边界内：过滤规则、开关模型、豁免清单、设置页、界面本地化、跨版本探测。
- 边界外：游戏自身的发牌流程、卡池定义、遗物实现、卡牌本地化文本。

## 组件分解

| 组件 | 源码符号 | 负责 | 不负责 |
|---|---|---|---|
| 入口 | `src/Plugin.cs` 的 `Plugin.Initialize` | 建日志、注册程序集、注册设置、注册补丁、失败时停用本模组 | 不实现过滤判定，不读写存档 |
| 拦截层 | `src/CardCreationPatch.cs` 的 `CardCreationPatch.Postfix` | 在 `GetPossibleCards` 之后介入，按总开关与判定模式决定是否继续，取候选、调判定、写回结果、打日志、异常兜底 | 不解释开关含义，不做逐张判定，不建索引 |
| 判定规则 | `src/CardFilter.cs` 的 `CardFilter.Apply` | 逐张判定保留或排除，输出统计 | 不接触游戏对象以外的状态，不读玩家遗物，不读设置，不消耗随机数 |
| 分组定义 | `src/CardGroups.cs` 的 `CardGroups.All` 与 `CardGroups.Visible`，数据来自 `src/CardGroupTable.cs` | 登记 15 个分组的标识、所属分节、判定方式、默认勾选状态、是否多人专属与成员；按当前游戏版本剔除愚者卡池成员，另维护可见分组与界面分节顺序 `Sections` | 不保存勾选状态，不建索引，不探测版本 |
| 固定表 | `src/ExclusionTable.cs` | 存默认豁免清单，拼豁免键 | 不判断卡池归属，不读设置，不参与判定 |
| 排除索引 | `src/FilterIndex.cs` 的 `FilterIndex.Get` | 按当前设置与可见分组算出排除集合，设置变过则重建 | 不逐张判定，不接触 `CardModel` |
| 设置模型 | `src/ModSettings.cs` 的 `SettingsModel`、`SettingsSnapshot` | 落盘模型、绑定、快照与写入版本号；启动时按架构版本决定是否重置为默认值 | 不建索引，不做逐张判定 |
| 设置页 | `src/ModSettings.cs` 的 `ConfigurePage`、`AddGroupSection`、`ConfigureExemptionPage` | 渲染总开关、判定模式、当前版本可见的分组开关与豁免子页，按 `Sections` 分节，拼装开关标题与说明，把改动写回绑定 | 不执行过滤，不建索引 |
| 本地化 | `src/Localization.cs` | 按游戏语言取界面文案，卡名转查游戏的 `cards` 表 | 不含业务规则，不决定默认值 |
| 游戏能力探测 | `src/GameFeatures.cs` 的 `GameFeatures.HasFool` | 判断当前游戏有没有愚者内容，给分组定义提供唯一的能力输入 | 不做界面显隐，不改名单，不读设置 |

## 依赖与分层

允许的依赖方向：入口 → {拦截层, 设置模型}；拦截层 → {判定规则, 排除索引, 设置模型, 分组定义}；排除索引 → {设置模型, 分组定义, 固定表}；设置页 → {设置模型, 分组定义, 固定表, 本地化}；分组定义 → 游戏能力探测。

禁止：判定规则 → 任何单例；游戏能力探测 → 设置模型、分组定义或任何界面组件；本地化 → 设置模型。

检查方式：`CardFilter.Apply` 的入参只有候选、卡池成员集合与一个排除集合，签名里没有 `Player`、`ModelDb`、`Plugin`、`ModSettings`；`CardGroups` 与 `ExclusionTable` 的成员都是 `static readonly` 常量表。

## 数据设计

### 落盘模型

`SettingsModel` 五个成员：

| 成员 | 类型 | 说明 |
|---|---|---|
| `SchemaVersion` | `int` | 架构版本，缺省 0（旧档没有这个字段时读作 0） |
| `Mode` | `FilterMode` | 判定模式，按数字存 |
| `MasterEnabled` | `bool` | 总开关，缺省 `true` |
| `Groups` | `Dictionary<string, bool>?` | 分组勾选状态，缺省 `null` |
| `Exemptions` | `List<string>?` | 豁免清单，缺省 `null` |

`null` 表示玩家从未改过。此时取代码默认值：分组按 `CardGroup.DefaultEnabled`，豁免按 `ExclusionTable.DefaultExemptionKeys`。这样新增分组与调整默认值都不需要写存档迁移。

新增分组的键在旧档里不存在，`ReadGroup` 会回落到 `CardGroup.DefaultEnabled`。因此加组、改组默认值都不必递增架构版本号。0.0.7 新增第 14 组时未递增，实测见 `8.TEST/0.0.7/自检-0.0.7加载与名单-godot.log` 第 445 行的 `groups=10/14`。

0.0.8 递增到 2。原因与「加组」不同：默认豁免清单也变了。豁免清单按全量快照落盘，老档里没有新条目时不会自动补默认值，只能整份重置，见 `2.PLANS/008-S-悲伤牌分组.md` 第四节。实测日志 `8.TEST/0.0.8/自检-0.0.8重置与名单-godot.log` 第 444 行：`[settings] reset. schema 1 -> 2, groups=11/15 exemptions=8`。

0.0.11 递增到 3。默认豁免新增血肉戏法、洁净、唤起，暴涨的键由 `orb:BULK_UP` 改为 `focus:BULK_UP`。老档里的旧键已无效，新键又不在清单里，必须重置。0.113.1 无头日志：`[compat] fool=True source=type`、`groups=11/15 exemptions=11 pages=16`、`ready. groups=15 entries=180 exemptions=11 schema=3`。

### 分组与分节

`CardGroup` 是位置记录，六个字段：`Id`、`Section`、`Kind`、`DefaultEnabled`、`MultiplayerOnly`、`Members`。`Section` 决定分组挂在哪一节，取值为 `CardGroups.Sections` 的元素。`MultiplayerOnly` 只供豁免页加提示，判定不看它。

`Sections` 的顺序就是设置页分节的顺序。前六节是职业标识，`other` 收纳不按职业归类的组。0.0.7 之前该数组叫 `Characters`，字段叫 `Character`；改名原因是第 14 组不属于任何职业。文案键 `pgf.char.<分节>` 与 `pgf.section.<分节>` 未改名。

`CardGroups.Build(bool hasFool)` 在建表时按卡池过滤成员：`hasFool` 为假时剔除所有 `CardGroupTable` 里 `Pool == "FoolCardPool"` 的行。`All` 保留 15 个组用于存档键与默认值，`Visible` 只保留 `Members.Length > 0` 的组用于设置页与排除索引。0.111 的 `Visible` 为 11 组，「多人专属牌」由 10 张降为 9 张；0.113 为 15 组。规则见 SP-030，取舍见 [ADR-013](../4.ADR/013-设置项按游戏版本显隐.md)。

`GameFeatures.HasFool` 先查 `typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.CardPools.FoolCardPool")`。类型探测抛异常时退回 `ReleaseInfoManager.Instance.SemVer`，`Major > 0` 或 `Minor >= 113` 视为真；两条都失败时返回 `true`。这样 0.111 不加载 Godot 文件接口也能判定，且未来版本改名时仍有版本号兜底。

`SchemaVersion` 与 `ModSettings.CurrentSchema` 不一致时，`ResetIfOutdated` 把模型整体改写为默认值并落盘，见 SP-024。旧档没有该字段，因此读到 0，首次启动即触发一次重置。新建数据不走这条路径：注册时的 `defaultFactory` 直接把当前版本号写进去。

### 豁免键的形式

豁免项是 `分组标识:卡牌标识`，例如 `doom:NEUROSURGE`。带分组前缀的原因：`BIG_BANG` 同属铸剑与星，两处的豁免必须能分别勾选，见 SP-013。默认清单共 11 条，其中 `doom:SLEIGHT_OF_FLESH`、`summon:CLEANSE`、`summon:INVOKE`、`focus:BULK_UP` 是 0.0.11 新增或移动的键，见 SP-021。

### 排除名单的标识形式

`CardGroupTable` 存的是 `ModelId.Entry` 字符串，不是完整 `ModelId`。理由：`Entry` 等于卡牌类名转大写蛇形（`StringHelper.Slugify`），与类别 slug 的写法无关；判定时读 `card.Id.Entry`，不拼接字符串，不产生分配。

该表由 `TOOLS/scripts/build-card-group-table.py` 从名单 CSV 生成，180 行、去重 178 张、15 个分组。脚本在生成前跑四道核对：卡名存在性、稀有度不在禁止集合、多人限定卡只在多人卡牌组、跨组重复只有双组卡。任一条不过就非零退出、不写盘，规则见 SP-014 与 SP-023。

### 池门控的数据来源

`CardCreationPatch` 用 `ConditionalWeakTable<CardPoolModel, HashSet<string>>` 按卡池实例缓存「该卡池全部卡牌的 `Entry` 集合」，键为 `player.Character.CardPool`。

不用 `CardModel.Pool` 属性。原因：该属性对 `ModelDb.AllCardPools` 取第一个包含该牌的池，一牌多池时返回值取决于池的枚举顺序；改用「是否属于玩家职业卡池」的成员判断，结果与顺序无关，且严格更保守（多池牌一律保留）。

### 排除集合

一个集合，成员是「已开启分组里、未被该组豁免的卡牌标识」的并集：

```
excluded = ⋃ 已开启的分组（成员 − 该组豁免）
```

0.0.4 曾按判定方式拆成标记集合与显式集合，并减掉一张多人限定卡放行表。0.0.5 把多人限定卡从按标记判定的分组里移走，两个集合不可能相交，于是合并。理由见 [ADR-009](../4.ADR/009-多人卡只进显式名单组.md)，旧构造见 [DOCS/012](../3.DOCS/012-T-分组开关与界面本地化的落地.md) 第四节。

### 设置的存放

`ModSettings` 用 RitsuLib 的数据槽存一个 `SettingsModel`，键 `settings`，文件名 `settings.json`，作用域 `SaveScope.Global`，落盘在 `mod_data/PrismaticGemFilter/`。枚举按数字存，见 `FilterMode` 的成员数值注释。

读取走 `ModSettingsBindings.WithDefault` 包一层默认值。未注册或读取抛异常时回落到默认快照，只记一次日志，不把异常抛进发牌链路。

快照 `SettingsSnapshot` 与排除索引都按 `ModSettings.Version` 缓存。写入路径统一经过 `ModSettings.Invalidate`，它既让快照失效，也调 `FilterIndex.Invalidate`。

### 界面文案的拼装

三处文案不是从 JSON 直接取的，由 `ModSettings` 里的三个方法在渲染时拼出。

| 方法 | 拼法 | 依据 |
|---|---|---|
| `ExcludeLabel` | `pgf.exclude.prefix` + `pgf.group.<id>.name` | SP-026 |
| `GroupDescription` | `pgf.group.<id>.desc` + `" · "` + `pgf.count` 或 `pgf.count.one` 填入 `group.Members.Length`；成员为 1 张时取单数键 | SP-028 |
| 豁免子页标题与行标题 | `pgf.exempt.page.title` 或 `pgf.exempt.row` + `" · "` + 组名 | SP-016 |

三者都用 `ModSettingsText.Dynamic` 包一层闭包。闭包在每次刷新时重新取词，因此切换游戏语言后整行跟着变，不需要重建页面。张数取自 `CardGroups.Visible` 的成员数组，当前版本不显示的牌不计入，名单更新时文案自动跟着变。

## 关键流程

一次发牌奖励的候选枚举：

1. 游戏调用 `CardCreationOptions.GetPossibleCards(player)`，返回惰性序列。
2. 后置方法先物化候选，再取设置快照。总开关关闭则直接返回（SP-020）。
3. 按判定模式决定是否继续。默认模式复刻棱彩宝石的四条早退：持有遗物、带 `IsCardReward`、不带 `NoCardPoolModifications`、卡池不全为无色。第二个模式只要候选出现玩家职业卡池之外的牌就继续。
4. `FilterIndex.Get` 取排除集合，为空则直接返回。首次取用时按设置建索引。
5. 取玩家职业卡池的 `Entry` 集合，调 `CardFilter.Apply` 得到保留列表与统计。
6. `Removed == 0` 时原样返回；保留列表为空时打警告并原样返回；否则写回保留列表。
7. 调用方（`CardFactory` 等）继续按稀有度掷骰。

第 3 步的两种判据见 `4.ADR/008`。默认模式不满足时直接返回，不调 `CardFilter.Apply`，也不打 Info 日志。

首个候选调用还会执行一次成员自检：按卡池类型核对名单里的成员是否真的在该池中，结果写日志。用途是暴露名单写错或版本缺卡。

## 失败与降级

- 后置方法整体包在 `try/catch` 里。任何异常都只记日志，不写回 `__result`，发牌链路照常。
- 候选集合过滤后为空时放弃过滤。规则见 `6.SPEC` 的 SP-002。
- 补丁应用失败时由 `ApplyRequiredPatcher` 调用 `DisableMod`，本模组不生效，游戏行为与未安装时一致。
- 设置注册失败时只记日志，发牌仍按默认快照工作。
- 读取架构版本失败或重置写盘失败时只记日志，本次运行沿用存档里的值，不阻断启动。规则见 SP-024 的注记。
- 本地化资源加载失败时退回英语字面文本，不显示键名。规则见 SP-022。
- 界面上某张牌在某语言下没有卡名时，显示卡牌标识本身。卡名来自游戏表，不属于本组件。
- 当前版本不缺成员时照常；0.111 缺愚者卡池时，`GameFeatures.HasFool` 返回假，愚者分节与四个空组整体隐藏，「多人专属牌」只显示 9 张，其余分组照常工作。规则见 SP-008 与 SP-030。
- `GameFeatures` 的类型探测失败时退回版本号；两条都失败时按含愚者处理，只多显示、不误藏。

## 资源与构建视图

| 项 | 值 |
|---|---|
| 目标框架 | `net9.0` |
| 程序集名 | `PrismaticGemFilter.dll`，必须与清单 `id` 相同 |
| 清单 | `9.SRC/mod_manifest.json`，`has_pck` 为 false |
| 嵌入资源 | `9.SRC/localization/{eng,zhs,zht}.json`，逻辑名 `PrismaticGemFilter.localization.<语言>.json` |
| 编译期引用 | 游戏目录 `sts2.dll`、`0Harmony.dll`；创意工坊 RitsuLib 的 `compat/0.113.0/` 与 `shared/` |
| 全部引用 | `Private=false`，不进产物 |
| 部署 | 构建后复制 DLL 与清单到 `mods/PrismaticGemFilter/` |
| 版本一致性 | 构建前校验清单 `version` 与项目 `Version` 相同 |

## 需求追溯

| 需求 | 组件 |
|---|---|
| RQ-001 | 拦截层、判定规则 |
| RQ-002 | 拦截层 |
| RQ-003 | 设置模型、设置页、排除索引 |
| RQ-004 | 跨版本探测（未建立） |
| RQ-006 | 设置模型、设置页 |
| RQ-008 | 判定规则 |
| RQ-009 | 固定表、排除索引、设置页 |
| RQ-010 | 固定表、排除索引 |
| RQ-011 | 分组定义、排除索引 |
| RQ-012 | 拦截层、设置模型 |
| RQ-013 | 拦截层 |
| RQ-014 | 本地化 |
| RQ-015 | 分组定义、固定表 |
| RQ-016 | 设置模型 |
| RQ-017 | 分组定义 |
| RQ-018 | 设置页、本地化 |
| RQ-019 | 分组定义、设置页、本地化 |
| RQ-020 | 分组定义、固定表、设置页、本地化 |
| RQ-021 | 游戏能力探测、分组定义、设置页、排除索引 |

## 偏差与限制

- 判定规则按「是否属于玩家职业卡池」实现，未使用 `CardModel.Pool` 的相等比较。两者在单池归属时等价；多池归属时本实现保留，属 SP-004 允许范围。
- 候选集合被物化。原实现返回惰性序列，调用方在下一句就 `.ToList()`，因此集合内容不变，只多了这次枚举。
- 0.0.2 的拦截层没有遗物判定，只有「候选是否被加宽」，曾使万花筒、海玻璃、色彩哲学家、角色卡修正器四处来源在未持有棱彩宝石时被误删。0.0.3 已按 `4.ADR/008` 修掉，历史记录见 `3.DOCS/010-E`。
- 设置作用域取 `SaveScope.Global`。`SaveScope.Profile` 在档案就绪前读取会抛异常，两个已找到的样例模组都用 Global。条款见 SP-018。
- 多人限定卡的归属靠名单保证，不用运行期的 `MultiplayerConstraint` 属性。代价是模组读不到该属性，若某个未来版本把普通卡改成多人限定，本模组察觉不到，只能靠重新跑名单门禁发现。见 ADR-009 的代价一节。
- 默认豁免清单是代码常量，与名单一样不随游戏版本变化。0.111.0 与 0.113.1 的稀有度与多人限制已核对一致，见 `3.DOCS/013-T`。
- 模组自身的显示名与页面标题取自嵌入资源，不合并进游戏的本地化表。因此游戏模组列表里的名字仍是清单的 `name` 字段，与界面内标题可能不一致。

## 变更记录

| 时间 | 变更 |
|---|---|
| 2026-11-15 05:10 | 建立骨架，登记计划组件与职责边界 |
| 2026-11-15 17:57 | 回填最小工程的四个组件与源码符号，补数据设计、关键流程、构建视图与偏差 |
| 2026-11-15 19:37 | 拦截层职责改为按模式判定；新增判定模式组件；关键流程补第 2 步；偏差登记 SP-005 违约 |
| 2026-11-15 20:55 | 判定模式组件落地为 `ModSettings` 与 `FilterMode`。补设置存放一节。关键流程改为五条判据。偏差标记已修复 |
| 2026-11-15 23:22 | 项目版本 0.0.4。新增分组定义、固定表、排除索引、设置页、本地化五个组件。补落盘模型、豁免键、两个排除集合、构建视图的嵌入资源。关键流程补总开关、索引与自检。追溯表补 RQ-006、RQ-009 至 RQ-011、RQ-014 |
| 2026-11-16 19:30 | 项目版本 0.0.5。排除索引合并为一个集合，两个排除集合一节改写为排除集合。固定表去掉多人限定卡放行表。落盘模型补 `SchemaVersion` 与重置行为。名单规模改为 166 行 164 张并登记四道门禁。追溯表补 RQ-015、RQ-016。偏差改写多人限定的代价 |
| 2026-11-16 21:00 | 项目版本 0.0.6。设置页职责补文案拼装。新增界面文案的拼装一节，登记三个拼装方法与 `ModSettingsText.Dynamic` 的刷新方式。追溯表补 RQ-018 |
| 2026-11-16 22:44 | 项目版本 0.0.7。新增第 14 组「现版本有 bug 的牌」与第七个分节 `other`。`Characters`、`Character` 改名为 `Sections`、`Section`，`CardGroup` 增加 `MultiplayerOnly` 字段。补「分组与分节」一节。文案拼装表补单复数。追溯表补 RQ-019。修正头部仍写 0.0.5 的滞后 |
| 2026-11-17 01:55 | 项目版本 0.0.8。分组数改为 15，名单规模改为 178 行 176 张。落盘一节补架构版本升到 2 的原因（默认豁免变化与加组的区别）。追溯表补 RQ-020 |
| 2026-11-18 19:16 | 项目版本 0.0.11。新增游戏能力探测组件 `GameFeatures`，分组定义按 `FoolCardPool` 过滤成员并派生 `Visible`。设置页与排除索引改用可见分组。名单规模改为 180 行 178 张，默认豁免 11 条。关键流程的默认判据由五条改为四条。追溯表补 RQ-021 |
