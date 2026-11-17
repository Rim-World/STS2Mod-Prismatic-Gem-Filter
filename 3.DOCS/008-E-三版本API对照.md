# 008-E 三版本 API 对照

- 建立时间：2026-11-15 17:12
- 结论：本项目运行期要用的游戏成员在 0.107.1、0.111.0、0.113.1 三版上签名一致。差异全部落在「新增内容」与「辅助 API」上，可以被离线表吸收。因此单一程序集、单一安装包的交付形态可行。
- 对应需求：RQ-004
- 相关决策：[ADR-002](../4.ADR/002-三版本交付形态.md)

> 2026-11-17 补注：0.107.1 自本日起不再支持，受支持版本为 0.111.0 与 0.113.1。本文关于 0.111 与 0.113.1 的对照结论继续有效；涉及 0.107.1 的部分只作历史证据。见 [ADR-011](../4.ADR/011-支持版本收窄为111与113.md)。

## 一、范围与方法

比对对象为三个版本的完整反编译：

| 版本 | 路径 |
|---|---|
| 0.107.1 | `STS2-Reference\1.STS2-107Reference\decompiled` |
| 0.111.0 | `STS2-Reference\2.STS2-111Reference\decompiled` |
| 0.113.1 | `STS2-Reference\4.STS2-113.1Reference\decompiled` |

方法为逐文件读源码取签名，再用卡牌索引 `card-index-eng-zhs.json` 比对卡牌集合。文件按类型名索引，因此不同版本的同名文件可直接对照。

## 二、运行期要用的成员

过滤逻辑只需要读取候选牌，不需要构造或修改 `CardCreationOptions`。下表是全部要用的成员。

| 成员 | 0.107.1 | 0.111.0 | 0.113.1 | 签名 |
|---|---|---|---|---|
| `CardCreationOptions.GetPossibleCards(Player)` | 169 行 | 134 行 | 134 行 | 相同 |
| `CardCreationOptions.CardPools` | 25 行 | 24 行 | 24 行 | 相同 |
| `CardCreationOptions.CardPoolFilter` | 33 行 | 32 行 | 32 行 | 相同 |
| `CardCreationOptions.Flags` | 58 行 | 50 行 | 50 行 | 相同 |
| `CardModel.Pool` | 297 行 | 298 行 | 359 行 | 相同 |
| `CardModel.MultiplayerConstraint` | 295 行 | 296 行 | 355 行 | 相同 |
| `Player.UnlockState` | 79 行 | 79 行 | 80 行 | 相同 |
| `UnlockState.CharacterCardPools` | 111 行 | 111 行 | 115 行 | 相同 |
| `AbstractModel.Id` | 31 行 | 31 行 | 33 行 | 相同 |

行号是各版本反编译文件中的位置，用来复核。

`CardFactory.CreateForReward` 的调用点也一致：

| 版本 | `CreateForReward` 里调用 `GetPossibleCards` 的行号 |
|---|---|
| 0.107.1 | `CardFactory.cs:217` |
| 0.111.0 | `CardFactory.cs:217` |
| 0.113.1 | `CardFactory.cs:218` |

三版写法都是：

```csharp
IEnumerable<CardModel> options2 = options.GetPossibleCards(player).Except(blacklist).ToList();
```

## 三、有差异的部分

### 3.1 CardCreationOptions 的构造与改写

| 项 | 0.107.1 | 0.111.0 起 |
|---|---|---|
| `CustomCardPool` 属性 | 有 | 无 |
| `CardCreationOptions(IEnumerable<CardModel>, ...)` 构造 | 有 | 无 |
| `WithCustomPool` | 有 | 无 |
| `WithCardPools` | `(pools, filter)` 两参数 | `(pools)` 一参数 |
| `WithFilter` | 无 | 有 |
| `WithRarityOdds` | 无 | 有 |
| `GetPossibleCards` 的 `CustomCardPool` 优先分支 | 有 | 无 |

本项目只读不写，不调用上面任何一个 `With*` 方法，也不构造该类型。所以这组差异不影响本项目。

### 3.2 CardCreationFlags

| 项 | 0.107.1 | 0.111.0 | 0.113.1 |
|---|---|---|---|
| `NoRarityModification` | 1 | 1 | 1 |
| `NoUpgradeRoll` | 2 | 2 | 2 |
| `NoHookUpgrades` | 4 | 4 | 4 |
| `NoModifyHooks` | 8 | 8 | 8 |
| `NoCardPoolModifications` | 0x10 | 0x10 | 0x10 |
| `NoCardModelModifications` | 0x20 | 0x20 | 0x20 |
| `ForceRarityOddsChange` | 0x40 | 0x40 | 0x40 |
| `IsCardReward` | 0x80 | 0x80 | 0x80 |
| `IsFromCombat` | 无 | 0x100 | 0x100 |

已用到的两个值三版相同。`IsFromCombat` 是 0.111.0 新增，本项目不用。

### 3.3 CardTag

| 版本 | 成员 |
|---|---|
| 0.107.1 | None, Strike, Defend, Minion, OstyAttack, Shiv |
| 0.111.0 | None, Strike, Defend, Minion, OstyAttack, Shiv |
| 0.113.1 | None, Strike, Defend, Minion, OstyAttack, Shiv, Wah, Cho, Ki, Shoot |

0.113.1 新增 4 项：`Wah`、`Cho`、`Ki`、`Shoot`。前 3 项属于愚者职业的机制，`Shoot` 是箭矢标记。

### 3.4 CardKeyword

三版成员完全一致，共 8 项：None, Exhaust, Ethereal, Innate, Unplayable, Retain, Sly, Eternal。

`Sly` 三版都有，「奇巧牌」组的判据在三个版本上成立。

### 3.5 卡池与角色

| 版本 | 卡池类型 |
|---|---|
| 0.107.1 | Colorless, Curse, Defect, Deprecated, Event, Ironclad, Mock, Necrobinder, Quest, Regent, Silent, Status, Token |
| 0.111.0 | 上述加 Deprived |
| 0.113.1 | 上述加 Fool |

愚者（Fool）只在 0.113 系列存在。`FoolCardPool` 在 0.107.1 与 0.111.0 中不存在。

### 3.6 其他类型

| 类型 | 0.107.1 | 0.111.0 | 0.113.1 |
|---|---|---|---|
| `ImpaledPower` | 无 | 无 | 有 |

`ImpaledPower` 是扎穿标记，只在 0.113.1 存在。

## 四、对分组的影响

| 组 | 判据 | 0.107.1 | 0.111.0 | 0.113.1 |
|---|---|---|---|---|
| 箭矢牌 | `CardTag.Shoot`，愚者 | 无此职业，空组 | 无此职业，空组 | 16 张 |
| 扎穿牌 | `ImpaledPower`，愚者 | 无此职业，空组 | 无此职业，空组 | 10 张 |
| 奇巧牌 | `CardKeyword.Sly`，静默猎手 | 有效 | 有效 | 有效 |
| 其余 10 组 | 标记判定 | 有效 | 有效 | 有效 |

空组的表现符合 SP-008：该开关在旧版本上不产生任何排除。

## 五、卡牌集合的差异

用各版本的 `card-index-eng-zhs.json` 比对卡牌标识：

| 版本 | 条目数 |
|---|---|
| 0.107.1 | 606 |
| 0.111.0 | 624 |
| 0.113.1 | 745 |

两处关键结论：

1. 0.107.1 有、0.113.1 没有的卡牌只有 1 张：`SCARE`（恫吓）。
2. `SCARE` 的源码 `Cards/Scare.cs` 不含任何分组标记。它不带 `CanonicalTags`，没有召唤变量、持续伤害、球、集中、扎穿、奇巧、刀、毒、铸剑或星标记。

因此以 0.113.1 生成的排除表可以直接用于三个版本。旧版本多出的 `SCARE` 不在任何组内，不需要列入。新版本多出的 139 张卡在旧版本根本不会出现，列入表里也不会命中。

## 六、对交付形态的结论

`6.ADR/002` 的备选条件是「只有在某关键成员在旧版缺失且无法降级时，才拆出变体目录」。

对照结果：关键成员全部三版一致，缺失的只有新职业与新标记，而它们都是空组，不需要运行时判断。因此不满足拆分条件。

结论：**一个程序集、一个 `mod_manifest.json`、一个安装目录，同时服务三个版本。**

配套约束：

1. 编译基线用本机的 0.113.1 程序集。本机没有 0.107.1 与 0.111.0 的 `sts2.dll`，无法用旧版编译。
2. 运行期只允许调用第二节表内的成员。任何不在表内的成员，必须先补进本表再使用。
3. 分组用离线表，表里存卡牌标识字符串。运行时不引用 `CardTag.Shoot`、`ImpaledPower` 这类只在 0.113 存在的类型。
4. `mod_manifest.json` 的 `min_game_version` 写 `0.107.1`。

## 七、未验证

- 未验证：单程序集在 0.107.1 与 0.111.0 上的实际加载与发牌。确认方法：在旧版本游戏内加载，读日志确认补丁成功。
- 未验证：0.111.0 上 `DeprivedCardPool` 是否参与普通发牌。本次只确认类型存在。
- 未确认：旧版本是否存在「卡牌标识相同但归属卡池不同」的情况。本次只比对了标识集合，没有逐张比对各版本的 `Pool` 归属。

## 八、取证路径

| 内容 | 路径 |
|---|---|
| 三个版本反编译 | `STS2-Reference\1.STS2-107Reference`、`2.STS2-111Reference`、`4.STS2-113.1Reference` |
| 卡牌索引 | 各参考目录下的 `card-index-eng-zhs.json` |
| 过滤点源码 | 各版本 `CardCreationOptions.cs` |
| 卡池类型清单 | 各版本 `decompiled` 下以 `CardPool.cs` 结尾的文件 |
