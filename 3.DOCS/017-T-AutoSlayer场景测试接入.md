# 017 AutoSlayer 场景测试接入

- 分类：T（任务知识）
- 时间：2026-11-17 19:59
- 关联：`8.TEST/0.0.10/自动测试-场景-过滤生效-godot.log`、`8.TEST/0.0.10/自动测试-场景-总开关关闭-godot.log`、`4.ADR/012-对局内自动测试用场景模式加临时门模组.md`、`2.PLANS/011-S-接入自动测试.md`

## 一、为什么要接自动测试

0.0.1 到 0.0.10 之间，「对局内发牌是否真的被过滤」一直靠用户手动开一局验证。用户每轮只能打一两场，样本少，且要等真人时间。

游戏自带两个自动化设施：AutoSlayer 机器人（自动打完整局）与 ScenarioRunner（按脚本走一局）。父级 `AGENTS.md` 记录了 AutoSlayer 的存在与启动参数。本轮把 ScenarioRunner 接上，第一次在真实对局里取到了过滤前后的候选集合。

## 二、为什么发行版默认跑不起来

`NGame.cs` 的两个分支都套了发行版判定：

| 位置 | 代码 | 后果 |
|---|---|---|
| `MegaCrit.Sts2.Core.Nodes/NGame.cs:698` | `if (!IsReleaseGame() && CommandLineHelper.HasArg("scenario"))` | 发行版启动时 `--scenario` 被整段跳过 |
| `NGame.cs:702` | `else if (!IsReleaseGame() && CommandLineHelper.HasArg("autoslay"))` | 同上，`--autoslay` 也跳过 |
| `NGame.cs:770` | `public static bool IsReleaseGame() { return true; }` | 返回值写死 |

所以原样加参数不会生效。父级 `AGENTS.md` 给的解法是用 Harmony 后置补丁把 `IsReleaseGame` 的返回值改成 `false`。本轮按这个解法做了一个临时门模组。

补丁生效后还有第二个门槛：`ScenarioRunner` 自己在 `ScenarioRunner.cs:254` 用 `new DevConsole(shouldAllowDebugCommands: true)` 建控制台，所以脚本里可以直接用 `relic`、`fight`、`kill` 这类调试命令，不需要另外开调试开关。

## 三、门模组

源码在 `8.TEST/0.0.10/AutoSlayGate/`，只做两件事：

1. 后置补丁 `NGame.IsReleaseGame`，返回值改成 `false`。
2. 挂两个探针：`CardCreationOptions.GetPossibleCards` 打印过滤后的候选集合，`CardFactory.CreateForReward` 打印真正给出的三张牌。

门模组用游戏自带的 `[ModInitializer(nameof(Initialize))]`（命名空间 `MegaCrit.Sts2.Core.Modding`），不依赖 RitsuLib。清单 `affects_gameplay` 写 `false`。

编译要引用三个程序集：`sts2.dll`、`0Harmony.dll`、`GodotSharp.dll`，都在 `data_sts2_windows_x86_64` 下。少引 `GodotSharp` 会报 `CS0012: 类型“Control”在未引用的程序集中定义`，因为 `NGame` 继承 Godot 的节点类型。

门模组只在测试期间放进游戏 `mods/` 目录，测试完成后移入回收站。项目源码与成品里不含任何测试代码。

## 四、场景脚本

脚本文件 `8.TEST/0.0.10/场景-卡牌奖励过滤.txt`：

```
relic PRISMATIC_GEM
fight CULTISTS_NORMAL
wait combat-started
kill all
wait 3
```

要点：

- `ScenarioRunner` 写死铁甲战士、单人对局、固定种子（`StartRunAsync` 里 `Player.CreateForNewRun<Ironclad>`），脚本不能换角色。
- 语法只有四类：控制台命令行、`wait <条件>`、`wait <秒数>`、`endturn`。`#` 开头或空格后的 `#` 起注释。`wait` 的秒数上限 120。
- 可用条件只有两个：`combat-started` 与 `turn-started`。
- 脚本行的控制台命令失败会让整次场景失败并返回非零退出码。
- 场景结束打一条 `Scenario completed successfully`，然后退出码 0。

启动命令：

```bash
cd "/c/Game/Steam/steamapps/common/Slay the Spire 2"
SteamAppId=2868840 ./SlayTheSpire2.exe --headless \
  --scenario="C:/Users/13167/Desktop/Mod/STS2Mod-优化棱彩宝石/8.TEST/0.0.10/场景-卡牌奖励过滤.txt" \
  --seed=GEMTEST1 --fast-mode=Fast
```

`--scenario` 与 `--autoslay` 互斥；`--fast-mode` 取 `Normal`、`Fast`、`Instant` 之一，缺省是 `Normal`。

## 五、实测结果

同一份设置、同一个种子 `GEMTEST1`，只改总开关，各跑一次。两次都退出码 0。

### 5.1 总开关开启（默认设置）

日志第 2150 至 2151 行：

```
[index] rebuilt. version=1 groups=11 excluded=122
[filter] mode=PrismaticGemOnly groups=11 candidates=599 removed=122 kept=477 poolGate=0 otherPool=514
```

`[filter]` 出现三次，对应三张候选牌各生成一次的链路。随后 `[reward] count=3 ids=HEADBUTT,GRAVE_WARDEN,TURBO`。

探针打印的最终候选是 391 张。391 = 599 − 122（本模组移除）− 86（另一个模组移除的 `MGR_MOD_CARD_*`，见第七节）。

### 5.2 总开关关闭

没有 `[filter]` 行，也没有 `[index]` 行：总开关关闭时补丁在构建索引之前就返回。探针候选 513 张，等于 599 − 86。发牌结果 `PERFECTED_STRIKE,POKE,UPROAR`，其中 `POKE` 属奥斯提组，正是开启总开关时会被移除的牌。

### 5.3 逐组核对

把两次运行的探针集合相减，得到开启总开关后真正被移除的 122 张。按分组核对：

| 分组 | 成员 | 默认豁免 | 实测移除 | 开关状态 |
|---|---|---|---|---|
| orb（球） | 25 | 2 | 23 | 开 |
| star（辉星） | 27 | 2 | 25 | 开 |
| doom（灾厄） | 13 | 1 | 12 | 开 |
| forge（铸造） | 11 | 1 | 10 | 开 |
| focus（集中） | 5 | 1 | 4 | 开 |
| sadness（悲伤） | 12 | 1 | 11 | 开 |
| osty（奥斯提） | 10 | 0 | 10 | 开 |
| summon（召唤） | 8 | 0 | 8 | 开 |
| sly（奇巧） | 9 | 0 | 9 | 开 |
| impale（扎穿） | 10 | 0 | 10 | 开 |
| bugged（缺陷） | 1 | 0 | 1 | 开 |
| poison（中毒） | 12 | 0 | 0 | 关 |
| shiv（小刀） | 12 | 0 | 0 | 关 |
| arrow（箭矢） | 13 | 0 | 0 | 关 |
| multiplayer（多人） | 10 | 0 | 0 | 关 |
| 合计 | 178 | 8 | 122 | 11 组开 |

每行都等于「成员减默认豁免」，开关关闭的组一张不移。豁免的 8 张（精神过载、聚变、暴涨、大爆炸两份、汇流、超能光束、活跃气氛）在两次运行里都留在候选中。

## 六、踩到的坑

**后置补丁的执行顺序。** 探针最初写 `HarmonyPriority(600)`，日志里排在 `[filter]` 之前，看到的是过滤前的集合。改成 `300` 后排在 `[filter]` 之后，看到过滤后的集合。结论：后置补丁按优先级从高到低执行，数值大的先跑。

**日志轮转。** 每次启动把旧的 `godot.log` 改名成带时间戳的文件。要边跑边看，必须读新的 `godot.log`，不要按旧文件的字节偏移等追加。

**场景会改真实进度。** `ApplyPreflight` 会关掉 FTUE、解锁四个职业的 epoch、把 `FastMode` 改成脚本参数值。本轮实测后 `progress.save` 被写过一次。模组设置文件 `mod_data/PrismaticGemFilter/settings.json` 没有被改。用户已自行备份存档。

**版本判据。** 日志里的 `ready. groups=15 entries=178 exemptions=8 schema=2 patches=1/1` 与 `Version=0.0.10.0` 仍然是最快的部署确认方式。

## 七、与别的模组共存

599 张候选里有 86 张是 `MGR_MOD_CARD_*`，是另一个卡牌模组新增的牌。总开关关闭的那次运行里它们也不在候选中，说明移除它们的是别的模组，不是本模组。具体是哪个模组未确认；本机装了 `CharacterModIsolation`，但它的 DLL 里没有 `GetPossibleCards` 字样，不能据此下结论。

对结论没有影响：本模组的贡献恰好是 122 张，与索引数值一致。

## 八、未验证

- 1000 次发牌的统计口径（RQ-001、RQ-009 的验收用例）。本轮只跑了一次奖励、三张候选。
- 遗物发牌入口（星系仪、梦境捕手、祈祷轮、白星）在默认判定模式下的实测。本轮只走战斗奖励。
- 界面渲染、豁免页、三种语言。无头模式不画界面。
- 0.111.0 上的同一脚本。本机没有 0.111.0 安装。
- AutoSlayer 完整跑一局（`--autoslay`）。本轮只用场景脚本，没有跑全自动整局。

## 九、遗物发牌补测（2026-11-17 20:21）

脚本 `8.TEST/0.0.10/场景-遗物发牌过滤.txt`，种子 `ELITE1`，一次跑完，退出码 0。证据 `8.TEST/0.0.10/自动测试-场景-遗物发牌-godot.log`。

| 入口 | 触发方式 | 奖励次数 | `[filter]` 调用 | 候选 / 移除 / 保留 | 发牌示例 |
|---|---|---|---|---|---|
| 星系仪 `ORRERY` | `relic ORRERY` | 5 | 15 | 599 / 122 / 477 | `HOLOGRAM,NOCK,PIERCING_WAIL` |
| 遗失宝箱 `LOST_COFFER` | `relic LOST_COFFER` | 1 | 3 | 599 / 122 / 477 | `CHO_READY,REAP,COMPILE_DRIVER` |
| 玻璃眼 `GLASS_EYE` | `relic GLASS_EYE` | 5 | 15 | 140/35、245/56、174/31 | `DODGE_AND_ROLL,CHO_FIRM,SCULPTING_STRIKE` |
| 祈祷轮 `PRAYER_WHEEL` | 普通房间胜利 | 2 | 6 | 599 / 122 / 477 | `LEADING_STRIKE,CRUSH_UNDER,DAGGER_THROW` |
| 白星 `WHITE_STAR` | 精英房间胜利 | 2 | 6 | 599 / 122 / 477 | `FLECHETTES,SWEEPING_BEAM,RAPID_SHOT` |
| 梦境捕手 `DREAM_CATCHER` | 休息处回血 | 1 | 3 | 599 / 122 / 477 | `PIERCING_WAIL,TIMBER,MOLTEN_FIST` |

说明：

- 六个入口的移除数都等于默认设置的 122。玻璃眼的候选按稀有度分三组，所以数值不同：common 140、uncommon 245、rare 174，分别移除 35、56、31，即 122 在各稀有度子集里的交集。
- 祈祷轮与白星都是在基础奖励之外各加 1 次奖励，因此是 2 次奖励、6 次调用。
- 玻璃眼的候选里有 120、210、149 张其他职业的牌，说明棱彩宝石确实加宽了这个入口。玻璃眼与棱彩宝石同属欧罗巴斯选项，正常一局只能拿一件；本场是测试命令同时给的。
- 梦境捕手要玩家在休息处选回血才发奖励，场景没有点击能力。门模组因此加了一条测试命令 `gaterestheal`，直接调用游戏自己的 `HealRestSiteOption.ExecuteRestSiteHeal`，走同一条奖励钩子。

踩到的坑：

- 精英房间的奖励生成比普通房间慢。等 4 秒不够，8 秒左右才出现 `[filter]`；改成 12 秒后稳定。
- 同一套脚本在 `RELIC1` 种子下抽到的精英是 `PHROG_PARASITE_ELITE`，击杀后 12 秒内不结算奖励；换成 `ELITE1` 种子的 `BYGONE_EFFIGY_ELITE` 就正常。原因未确认，可能是该精英的死亡处理与结算时机不同。

未覆盖：万花筒（手工名单，带 `NoCardPoolModifications`，按 SP-005 不该过滤）、海玻璃（自带他职业池，不经过棱彩宝石加宽）、猎杀（要打出这张牌才有奖励）、水晶球与试炼（事件小游戏）、Blitz 模式。
