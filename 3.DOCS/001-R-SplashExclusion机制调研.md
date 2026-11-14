# 001-R SplashExclusion 机制调研

- 调研时间：2026-11-15 05:10
- 方法：反编译。工具为 `ilspycmd` 9.1.0，本机全局已安装，未新下载。
- 证据目录：`TMP/0.0.1/调研-反编译`。下文行号均指该目录内的反编译文件。

## 调研对象

| 项 | 值 |
|---|---|
| 创意工坊条目 | `3755773515` |
| 模组 id | `SplashExclusion` |
| 版本 | 1.2.0 |
| 主程序集 | `SplashExclusion.dll`，36864 字节，SHA256 `48D9BE67A7AC6CF16954B7DE2D83124FA1A3D8E7E17C4561846F3B2F249447B5` |
| 依赖 | `STS2-RitsuLib` 0.6.12 及以上 |
| 声称的最低游戏版本 | 0.113.0 |

## 一句话机制

不改任何卡牌的效果代码，只在三处做减法：可用的角色卡池列表、候选牌列表、战斗随机生成的放行开关。

## 拦截点总览

| 层 | 位置 | 手法 |
|---|---|---|
| 卡池列表 | `Splash.OnPlay` 内部 | Prefix 返回 false，整段重写 |
| 候选牌列表 | 同上 | 按 `CardTag` 过滤 |
| 全部生成器的过滤 | `CardFactory.FilterForCombat` | Prefix，按标志位替换返回值 |
| 每个生成器的调用点 | 23 个方法的 IL | Transpiler，插入来源标签 |
| 设置与存档 | RitsuLib | JSON 存档加设置页 |
| 第三方预测模组 | RandomForeseer 的镜像方法 | 反射查找加同款 Transpiler |

## 机制 A：整段重写 Splash 的出牌流程

原版 `Splash.OnPlay` 见 `game/Splash.cs:20`。流程为：取已解锁角色卡池、多于一个时移除自己角色的池、只留攻击牌、抽 3 张、选 1 张、免费打出、入手。

重写版本见 `splash/SplashExclusion/SplashPoolExclusionPatch.cs:40`。结构与原版一致，只加两处过滤：按池过滤、按标签过滤。

必须注意的三点：

1. 重写体照抄了 0.113.0 的实现。游戏改动 `Splash` 后，这段代码会静默失效或跑回原版。
2. 重写体保留了 `_mockGeneratedCard` 字段这条测试通道。漏掉会让测试用例失效。
3. Prefix 有两个短路：多人禁用时跑原版，排除列表为空时跑原版。

## 机制 B：来源标签加单点放行

这是本模组最值得借鉴的部分，分四步。

1. 登记生成器。`CombatGenerationPolicy.GeneratorTypes` 共 23 条映射，其中 18 张牌、5 个能力（`splash/SplashExclusion/CombatGenerationPolicy.cs:16`）。键是牌类型，值是真正含工厂调用的类型。能力牌要做转换，例如 `Calamity` 映射到 `CalamityPower`。
2. 找出调用点。`FindSourceMethods` 遍历值类型的所有嵌套类型，找 IL 里调用 `CardFactory.GetForCombat` 或 `GetDistinctForCombat` 的方法（`splash/SplashExclusion/GeneratorSourcePatch.cs:24`）。必须扫嵌套类型，因为异步方法的工厂调用在编译器生成的状态机里。
3. 改调用点。Transpiler 在每条工厂调用指令前压入来源名，并把被调方法换成多一个 `string` 参数的包装（`splash/SplashExclusion/GeneratorSourcePatch.cs:60`）。插入指令时用 `MoveLabelsFrom` 与 `MoveBlocksFrom` 保住跳转目标。
4. 作用域内放行。`WithSource` 用 `[ThreadStatic]` 标志括住原调用（`splash/SplashExclusion/CombatGenerationPolicy.cs:74`）。`FilterForCombat` 的 Prefix 看到标志为真时，把返回值换成放宽后的过滤（`splash/SplashExclusion/CombatGenerationPatch.cs:11`）。

放宽条件是：卡牌本身允许在战斗内生成，或者稀有度属于 Common、Uncommon、Rare；并且稀有度不是 Basic、Ancient、Event。

覆盖面已核对。游戏里把 `CanBeGeneratedInCombat` 覆盖为 false 的类型共 25 个，其中 22 张牌、3 个药水。放行后真正能进入候选的是 Rare 档，例如 `Feed`、`HandOfGreed`、`Nightmare`、`MythicForm`、`Alchemize`。Ancient 档的 `Abundance`、`NeowsFury` 与 Status 档的 `Amnesia` 仍被稀有度条件挡住。

## 机制 C：兼容第三方预测模组

`RandomForeseerCompatibility` 的做法是「装不上就放弃，绝不拖累主功能」：按名字找程序集、反射取类型、给镜像方法打同款 Transpiler、断言 Splash 镜像里的卡池 `SelectMany` 恰好出现一次，不符则回滚全部补丁并写一条 Warn（`splash/SplashExclusion/RandomForeseerCompatibility.cs:33`）。

## 机制 D：多人保护

`IsMultiplayerSession()` 读 `RunManager.Instance.NetService.Type != NetGameType.Singleplayer`。`NetGameType` 的值为 None=0、Singleplayer=1、Host=2、Client=3、Replay=4。存档字段 `disable_in_multiplayer` 默认为 true。

理由充分：改变候选列表会改变随机数的消耗结果，两端不同步会立刻发散。

## 标签机制的细节：屏蔽奥斯提牌靠标签，不靠穷举

调研对象的排除判定完全基于 `CardTag`，逐卡运行时读标签，不维护卡牌清单。链路如下：

1. 设置页开关 `排除奥斯提攻击牌` 绑定 `TagExclusionBinding("OstyAttack")`（`splash/SplashExclusion/SplashExclusionMod.cs:201`）。
2. 勾选后把字符串写进 `ExcludedTags`（同文件 :345 的 `SetTagExcluded`）。
3. 过滤时用 `Enum.TryParse<CardTag>` 把字符串转成枚举值（同文件 :316 的 `GetExcludedTagValues`）。
4. `SplashCardFilters.Cards` 逐卡判断 `!excluded.Any(t => c.Tags.Contains(t))`（`splash/SplashExclusion/SplashCardFilters.cs:39`）。

标签的数据来源是游戏自身。每张卡在 `CanonicalTags` 里声明 `CardTag`，`CardModel.Tags` 在运行时展开。

0.113.1 的实测数量（按参考库 `4.STS2-113.1Reference\decompiled` 统计）：

| 标签 | 卡牌数 | 卡牌类 |
|---|---|---|
| `OstyAttack` | 13 | `BoneShards`、`Fetch`、`Flatten`、`HighFive`、`Poke`、`Protector`、`Rattle`、`RightHandHand`、`SicEm`、`Snap`、`Squeeze`、`SweepingGaze`、`Unleash` |
| `Shoot` | 17 | `ArrowStab`、`BlossomShot`、`BlotOutTheSky`、`DivineShot`、`Florescence`、`FoolishShot`、`HeavyShot`、`HobblingShot`、`LockedIn`、`MelodicShot`、`QuickShot`、`RainShot`、`RapidShot`、`RepeatShot`、`SkyShot`、`TheShot`、`TrickShot` |

`CardTag` 在 0.113.1 共 10 项：`None`、`Strike`、`Defend`、`Minion`、`OstyAttack`、`Shiv`、`Wah`、`Cho`、`Ki`、`Shoot`。

`CardKeyword` 只有 8 项：`None`、`Exhaust`、`Ethereal`、`Innate`、`Unplayable`、`Retain`、`Sly`、`Eternal`。它不承载球、集中一类语义。

结论：标签机制只能覆盖游戏预先定义过标签的类别。球牌、集中、球位没有标签与关键词，必须另找判定依据。

## Orrery（星系仪）：用户要覆盖的遗物发牌

| 项 | 值 |
|---|---|
| 类名 | `MegaCrit.Sts2.Core.Models.Relics.Orrery` |
| 中文名 | 星系仪（`localization/zhs/relics.json` 的 `ORRERY.title`） |
| 稀有度 | `RelicRarity.Shop`，商店专属 |
| 效果 | 拾起时获得 5 次卡牌奖励 |
| 卡池 | 构造时只用 `base.Owner.Character.CardPool`，即本职业池 |

源码见参考库 `4.STS2-113.1Reference\decompiled\MegaCrit.Sts2.Core.Models.Relics\Orrery.cs`。

棱彩宝石为什么能影响它：`CardReward` 构造函数会执行 `options.WithFlags(CardCreationFlags.IsCardReward)`。棱彩宝石的挂钩只检查「没有 `NoCardPoolModifications`」且「有 `IsCardReward`」，因此对星系仪的奖励生效。

按此推论，拦截点选在 `CardCreationOptions.GetPossibleCards` 可以同时命中战斗奖励与星系仪。该推论基于源码，未运行验证。

## 球、集中、球位的判定依据（供 P2 使用）

0.113.1 的球类卡牌不走标签，而是直接调用下列 API：

- `OrbCmd.Channel<T>`，例如 `BallLightning`。
- `OrbCmd.EvokeNext`，例如 `Dualcast`。
- `OrbCmd.AddSlots`，例如 `Capacitor`。
- `FocusPower`，例如 `Defragment`。

因此判定「某张牌属于球体系」有三条可行路线：

1. 显式卡牌清单。简单，但每次游戏更新要维护。
2. 按卡池归属加人工分类。`DefectCardPool` 在 0.113.1 有 91 张牌，仍需逐张分类。
3. 扫描卡牌类的 IL，看是否引用 `OrbCmd`、`FocusPower` 一类类型。调研对象用同样的手法扫描 `CardFactory` 调用，已有先例。

## 与本项目的直接关联：棱彩宝石一侧的事实

| 事实 | 证据 |
|---|---|
| 游戏存在遗物 `PrismaticGem`，Ancient 稀有度，加 1 点能量上限 | `game_full/sts2.decompiled.cs:98112` |
| 它的跨色逻辑在 `ModifyCardRewardCreationOptions`：把卡池并上全部角色卡池 | 同上 :98129 |
| 该逻辑受 `NoCardPoolModifications` 与 `IsCardReward` 两个标志约束 | 同上 :98135 |
| `CardCreationOptions` 自带 `CardPoolFilter` 字段，注释明确说明它是给棱彩宝石这类改动准备的 | 同上 :39246 |
| `GetPossibleCards` 枚举卡池时套用 `CardPoolFilter` | 同上 :39348 |
| `CreateForReward` 在候选过窄时会抛 `InvalidOperationException` | `game/CardFactory.cs:240` |
| 另有遗物直接调用 `CreateForReward` 发牌，例如 `Kaleidoscope`（每个奖励从 3 个其他角色的池各取 1 张） | `game_full/sts2.decompiled.cs:94067` |
| `CardCreationFlags` 含 `NoModifyHooks`（8）、`NoCardPoolModifications`（0x10）、`NoCardModelModifications`（0x20），用于避免递归与越权修改 | 同上 :39164 |

## 可直接借鉴的套路

1. 用一个汇合点加标志位，替代逐个生成器打补丁。
2. `[ThreadStatic]` 作用域只包住工厂调用这一小段同步窗口。
3. 插入 IL 指令时转移标签与异常块。
4. 设置页延后到主菜单就绪事件注册。
5. 第三方兼容一律「找不到跳过、结构不符回滚」。
6. 多人默认自禁用。

## 必须避开的坑

1. 整段重写对方方法对版本最敏感。本项目的目标版本有三个，不宜照搬。
2. 放宽全局过滤条件会放大误伤范围。
3. 依赖「IL 里出现工厂调用」的定位方式很脆弱。

## 未确认

- 0.107.1 与 0.111.0 是否存在 `CardCreationOptions.CardPoolFilter` 与 `GetPossibleCards` 的相同签名。参考库已有两份反编译，比对尚未执行。
- 「集中、球位」在本项目里需要覆盖哪些具体卡牌。`Defragment`、`Capacitor` 是示例，完整清单未整理。
- 未运行游戏验证任何行为。全部结论来自源码阅读。
