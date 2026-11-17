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

日志第 2059 至 2060 行：

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
