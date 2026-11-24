# 测试文件索引

## 当前状态

`9.SRC` 已建立 0.0.15。构建、部署、支持版本门槛（0.111.0 起）、补丁应用、设置注册、三语文案各 70 条、16 个分组的默认值、独立的分组开关与全局豁免、按两者重算的实际过滤池、名单四道门禁已验证。0.0.7 的索引数值已由用户实测通过。0.0.10 不改过滤行为。对局内的发牌行为已于 2026-11-17 19:53 至 20:21 用游戏自带的场景脚本在真实对局里验证：默认设置下 122 张应排除牌一张不剩，总开关关闭后全部回来；六个遗物发牌入口同样被过滤。界面渲染仍未实测。2026-11-17 23:19 至 23:41，用户在 0.111.1 上完成第二次实测：同一份 DLL 加载成功、补丁 1 应用 0 失败、持宝石时移除 100 张（0.113.1 为 122 张，差额是 0.111.1 没有的 22 张愚者机制牌）、不持宝石时 0 条过滤行。见 `3.DOCS/018`。2026-11-18 20:40 至 20:44，0.0.13 在 0.111.1 上完成数据级、无头与对局内绑定探针验证：默认排除 99 张。该版本的「分组开关与单卡开关读写同一集合」做法已在 0.0.14 被分层模型取代，不能当作当前实现说明。

2026-11-18 21:12 至 21:27 完成 0.113.1 全量回归。按 0.113 程序集重新构建的 0.0.13 在无头启动、数据级状态、机械人对局、分组关闭、总开关关闭和六个遗物发牌入口上全部通过。0.113.1 的默认集合为 121 张，15 个可见分组，无设置重置。界面渲染仍未验证。

2026-11-19 01:30 至 01:48 完成 0.0.14 的分层状态验证。数据级 23 项全过；0.113.1 无头启动显示 `schema=6`、默认池 121；默认场景 3 条 `removed=121`；forge 关闭与 `BIG_BANG` 取消豁免的跨组场景 `filtered=112`、3 条 `removed=112`。最终构建 DLL SHA-256 `d7ff0752...c304e7d` 的默认场景复测也通过。设置文件已恢复，测试门已移入回收站。界面渲染仍未验证。

2026-11-24 01:02 至 01:12 完成 0.0.15 的 0.111.1 验证。数据级 30 项全过；无头启动 `schema=7`、`groups=9/12`、`entries=154`、`filtered=104`、`exemptions=13`；首轮对局 3 条 `[filter] candidates=429 removed=70`。候选集合保留了守墓人、捕捉灵魂、降灵，未包含 6 张未豁免灵魂牌；彼岸一瞥与灵魂绑定因多人组默认关闭而未出现。自动局在 300 秒超时被终止，未完整跑完。

2026-11-24 01:37 至 01:38 完成 0.0.15 的 0.113.1 场景验证。无头启动 `schema=7`、`groups=12/16`、`entries=191`、`filtered=126`、`pages=17`；默认场景 3 条 `[filter] candidates=678 removed=126`；3 次候选探针均为 466 张，保留 3 张默认豁免灵魂牌，未包含 6 张未豁免灵魂牌。场景退出码 0。设置文件已按测试前副本恢复，测试门已移出游戏目录。

2026-11-24 22:37 完成 0.113.1 的无宝石对照组。3 次候选探针均为 85 张，`[filter]` 0 条，奖励全为本职业牌，场景退出码 0。RQ-005 的两版本边界均已验证。0.113.1 界面渲染与多人对局仍未验证。

## 本地门禁

`CI-CD/` 放本机跑的门禁，不随版本清理。

| 文件名 | 放入时间 | 用途 | 依赖 |
|---|---|---|---|
| `CI-CD/check-user-prompt.py` | 2026-11-16 20:10 | 提交门禁：改动源码或配置的提交必须带入一条新的用户 prompt；只改文档与测试日志的提交放行 | Python 3.8 以上，无第三方库 |
| `CI-CD/install-hooks.ps1` | 2026-11-16 20:12 | 把上面那个脚本装成 `.git/hooks/pre-commit` 与 `post-commit`，已有同名钩子先备份 | PowerShell 5.1 以上 |

安装：

```
powershell -ExecutionPolicy Bypass -File 8.TEST/CI-CD/install-hooks.ps1
```

手动查询状态：

```
python 8.TEST/CI-CD/check-user-prompt.py --mode show
```

## check-user-prompt.py

**为什么不用 git 判断**：`1.PRD/用户prompt/` 在 `.gitignore` 里，git 看不到这些文件，暂存区里永远没有 prompt。门禁只能读文件系统。

**怎么记状态**：`.git/user-prompt-gate.json` 记录「已被某次提交消费过的 prompt 文件名」。这个文件在 `.git` 下面，不入库。提交前比对目录里的文件与这份名单，没有新文件就拒绝；提交成功后由 `post-commit` 把新文件写进名单。

**首次启用**：以 HEAD 的提交时间为界。比 HEAD 早的文件记为已消费，比 HEAD 晚的算作本次带入。这样装门禁不会把历史 prompt 误判成待消费，也不会白送一次放行。

**判定哪些改动要 prompt**：先看暂存区相对 HEAD 改了哪些路径，再按 `is_documentation` 逐个分类。只要有一个路径不是文档，这次提交就要 prompt。规则是宁可误拦不可漏放。

| 路径 | 分类 | 说明 |
|---|---|---|
| 任意位置的 `.md` | 文档 | 含 `AGENTS.md`、`README.md`、`1.PRD` 至 `7.ChangeLog` 下的正文 |
| `9.SRC/README.md`、`9.SRC/CHANGELOG.md` | 文档 | 源码目录里只认这两个 |
| `9.SRC/` 下其余路径 | 代码 | 源码、项目文件、清单、文案 JSON 都算 |
| `TOOLS/` 下的 `.md` | 文档 | 工具说明 |
| `TOOLS/` 下其余路径 | 代码 | 脚本算代码 |
| `8.TEST/CI-CD/` | 代码 | 门禁脚本改动必须带 prompt |
| `8.TEST/` 下的 `.log` | 文档 | 测试产物 |
| `8.TEST/` 下其余路径 | 代码 | 附件、临时文件都不放过 |
| 10.OUTPUT、PRIVACY、`.gitignore`、其余位置 | 代码 | 未列入上表的路径一律从严 |

**放行的四种情形**：

| 情形 | 处理 |
|---|---|
| 目录里有未消费的 prompt | 放行，并打印带入了哪几条 |
| 暂存区里全是文档与测试日志 | 放行，不消耗 prompt。文档回填、版本日志补记、测试日志登记走这条 |
| 暂存区相对 HEAD 没有改动（只改提交信息） | 放行，提示这是 amend |
| `.git/user-prompt-gate.skip` 里写了理由 | 放行一次，理由记入 `.git/user-prompt-gate.log`，随后删除该文件 |

**拒绝时**：退出码 1，`pre-commit` 随之中止提交。提示里列出这次改动涉及的源码文件，并给出下一步应写的文件名。不得用 `git commit --no-verify` 绕过，那是全局失效，不是一次豁免。

**已知限制**：

- 找不到 `python` 时钩子直接放行并告警，装了但没生效会无声无息。确认方法是手动跑一次 `--mode show`。
- 只校验「有没有新文件」，不校验内容是否真的是用户原话。
- 文件名不是四位递增编号时只提示，不拦。编号规则靠人和 `--mode show` 的输出维护。
- 状态文件删掉后会按「首次启用」重建基线，等于重置门禁。
- 分类按路径判断，不看内容。改动 `.md` 正文里贴的示例代码不会被算作源码改动。
- 一次提交同时改文档与源码时按源码处理，要 prompt。

## 版本登记表

### 0.0.1

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `selfcheck-godot.log` | 2026-11-15 17:52 | 否 | 99 | 带自检探针的启动日志。含 12 个标识的运行时解析结果，以及池门控三种情形的判定输出 | 是 |
| `探针-ModelDbSelfCheckPatch.cs.txt` | 2026-11-15 17:52 | 否 | 6 | 自检探针的源码留档。探针已从 `9.SRC` 移除，此处保留以便复测与后续版本降级检查复用 | 是 |
| `build-clean-godot.log` | 2026-11-15 17:57 | 否 | 92 | 撤掉探针后的干净构建启动日志。记录 1 个补丁应用成功 | 是 |
| `release-godot.log` | 2026-11-15 18:03 | 否 | 91 | Release 构建的启动日志。0.0.1 | 否 |
| `user-playtest-ironclad-necro-godot.log` | 2026-11-15 18:44 | 否 | 180 | 用户首次实测日志。含铁甲战士与亡灵契约师两局。战士局的 3 行过滤记录是本项目第一条对局内证据 | 是 |
| `release-0.0.2-godot.log` | 2026-11-15 18:47 | 否 | 71 | 0.0.2 的无头启动日志。确认新版本加载且补丁应用成功 | 是 |
| `user-playtest-elitehunter-godot.log` | 2026-11-15 18:54 | 否 | 180 | 静默猎手配「精英猎手」修正器的完整对局日志。34 次过滤调用，含精英奖励 28 次与普通奖励 6 次。分析见 `3.DOCS/009-E` | 是 |

### 0.0.3

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-设置与判据-godot.log` | 2026-11-15 20:52 | 否 | 121 | 0.0.3 的无头启动日志。含设置注册成功、落盘 17 字节、补丁 1 应用 0 失败三项证据 | 是 |
| `user-playtest-colorful-philosophers-godot.log` | 2026-11-15 22:25 | 否 | 194 | 用户实测日志。9 次战斗奖励各自排除 12 张；持宝石与卸宝石各触发一次色彩哲学家，两次都未过滤。分析见 `3.DOCS/011-E` | 是 |
| `user-restart-mode-persisted-godot.log` | 2026-11-15 22:26 | 否 | 138 | 改设置后重启的启动日志。显示 `mode=AnyWidenedPool`，证明取值跨进程保持 | 是 |
| `user-playtest-anywidened-godot.log` | 2026-11-15 22:42 | 否 | 153 | 第二个取值下的实测日志。色彩哲学家按颜色分别排除 3 / 6 / 1 张，水晶球 3 次奖励共 9 次调用。分析见 `3.DOCS/011-E` 情形五 | 是 |
| `user-playtest-orrery-godot.log` | 2026-11-15 22:51 | 否 | 142 | 星系仪实测。5 次卡牌奖励共 15 次调用，每次 `removed=12`。分析见 `3.DOCS/011-E` 情形六 | 是 |

### 0.0.4

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-设置页注册与本地化-godot.log` | 2026-11-15 23:16 | 否 | 127 | Debug 构建的无头启动日志。含 14 个页面注册成功、简体中文取词 51 条、13 组 184 行数据表加载三项证据 | 是 |
| `自检-Release启动-godot.log` | 2026-11-15 23:18 | 否 | 125 | Release 构建的无头启动日志。与上一份的三行关键证据相同，用于确认部署产物为 Release。第 455 至 460 行是证据位置。早于豁免入口加组名那次改动 | 是 |
| `自检-Release最终构建-godot.log` | 2026-11-15 23:27 | 否 | 127 | 交付用的 Release 构建的无头启动日志。0 行 `[ERROR]`，三行关键证据与上一份一致 | 是 |

### 0.0.5

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-首次启动重置-godot.log` | 2026-11-16 19:42 | 否 | 116 | Release 构建的无头启动日志。第 386 行是 `[settings] reset. schema 0 -> 1`，第 392 行是 `ready. groups=13 entries=166 exemptions=7 schema=1`，第 913 行是设置落盘 532 字节。用于证明旧档被重置为默认值 | 是 |
| `自检-第二次启动不重置-godot.log` | 2026-11-16 19:43 | 否 | 118 | 同一份构建的第二次无头启动。没有 `reset` 行，其余两行与上一份相同，用于证明重置只执行一次 | 是 |

第一版实现（19:41 那次启动）没有触发重置，当时的日志已被后续启动轮转覆盖，未留档。失败现象、原因与修法记在 `3.DOCS/013-T-名单门禁与配置重置.md` 第三节。

### 0.0.6

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-0.0.6加载与文案-godot.log` | 2026-11-16 20:56 | 否 | 116 | Release 构建的无头启动日志。关键证据两处：`[PrismaticGemFilter.texts] ... entries=59, local=59, fallbackEntries=0` 证明三语文案全量加载且简体无缺键；`ready. groups=13 entries=166 exemptions=7 schema=1` 证明名单与设置未受影响。全文 0 行 `[ERROR]` | 是 |

本轮的界面观感（三语排版、字号、开关排布）无头模式看不到，属未验证项。

### 0.0.7

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `构建-0.0.7-release.log` | 2026-11-16 22:36 | 否 | 0.4 | Release 构建输出。0 警告 0 错误，构建目标同时把 DLL 与清单部署到游戏目录 | 否 |
| `自检-0.0.7加载与名单-godot.log` | 2026-11-16 22:40 | 否 | 126 | Release 构建的无头启动日志。关键证据三处：第 443 行 `Version=0.0.7.0` 证明部署产物是本轮构建；第 444 行 `entries=64, local=64, fallbackEntries=0` 证明三语文案全量加载；第 445 行 `groups=10/14` 证明新组按默认值参与、旧档未被改写。第 449 行 `ready. groups=14 entries=166` 是名单规模。全文 0 行 `[ERROR]` | 是 |
| `验收清单-0.0.7.md` | 2026-11-16 23:26 | 否 | 6.0 | 交给用户的验收步骤：界面核对、索引数值期望值、豁免与持久化、烟雾测试、回归。含数值来历的算式 | 是 |
| `用户实测-0.0.7索引数值-godot.log` | 2026-11-17 01:40 | 否 | 230 | 用户按验收清单第二节执行的实测日志。5 条 `[index]` 依次为 10/111、9/110、10/111、11/124、10/111，五点全中。同一候选集合 513 张时 `removed` 由 102 变为 101，差 1 张即本组唯一成员。全文 1 行 `[ERROR]`，是游戏自身的骨骼动画报错，与本模组无关 | 是 |

本轮未验证项：新分节「其他」的界面观感；「遮天蔽日」在对局里是否真的不再出现。两项都需要真人操作。

验收结果（2026-11-17 01:40）：第一步的索引数值五点全中，通过。界面观感、豁免链路、持久化与对局内效果仍未验证。

### 0.0.8

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `构建-0.0.8-release.log` | 2026-11-17 01:44 | 否 | 0.4 | Release 构建输出。0 警告 0 错误，构建目标同时把 DLL 与清单部署到游戏目录 | 否 |
| `自检-0.0.8重置与名单-godot.log` | 2026-11-17 01:45 | 否 | 52 | Release 构建的无头启动日志。关键证据四处：第 444 行 `[settings] reset. schema 1 -> 2, groups=11/15 exemptions=8` 证明架构版本重置生效；第 445 行 `entries=66, local=66, fallbackEntries=0` 证明三语文案全量加载；第 446 行 `groups=11/15 ... pages=16` 证明新组与豁免页注册；第 450 行 `ready. groups=15 entries=178` 是新一代名单规模。全文 0 行 `[ERROR]` | 是 |
| `验收清单-0.0.8.md` | 2026-11-17 02:00 | 否 | 2.9 | 给用户的验收步骤：愚者节文案与豁免页、索引数值五点（122、111、122、123、122）、对局内观察 | 是 |

本轮未验证项：设置页「排除悲伤相关牌」一行的观感与豁免页滚动；非愚者对局里这 11 张是否真的不再出现；架构版本重置后玩家自定义被清空属预期，需用户确认可接受。

### 0.0.9

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `构建-0.0.9-release.log` | 2026-11-17 19:05 | 否 | 0.4 | Release 构建输出。0 警告 0 错误，版本号与清单经构建目标校验后部署到游戏目录 | 否 |
| `自检-0.0.9依赖门与加载-godot.log` | 2026-11-17 19:06 | 否 | 231 | Release 构建的无头启动日志。关键证据四处：第 445 行 `Version=0.0.9.0` 证明部署产物是本轮构建；第 446 行 `entries=66, local=66, fallbackEntries=0` 证明三语文案全量加载；第 447 行 `groups=11/15 ... pages=16` 证明依赖门放行后设置注册正常；第 451 行 `ready. groups=15 entries=178 exemptions=8 schema=2 patches=1/1` 是加载完成的判据。设置文件未被改写。全文 17 行 `ERROR`，全部来自其他模组：`RegentFX` 的着色器预加载 13 行，`Loadout` 的纹理与任务编号 4 行 | 是 |

本轮未验证项：RitsuLib 0.6.13 被拒绝加载的路径；0.107.1 与 0.111.0 的加载；界面与对局内效果沿用 0.0.8 的待测项。

### 0.0.10

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `构建-0.0.10-release.log` | 2026-11-17 19:15 | 否 | 14 | 首次 Release 构建。编译 0 错误，但游戏进程占用 DLL，复制重试 10 次后放弃，部署未完成。保留为部署失败的证据 | 是 |
| `构建-0.0.10-release-重试部署.log` | 2026-11-17 19:31 | 否 | 0.4 | 游戏退出后重试。0 警告 0 错误，DLL 与清单部署成功；部署的 DLL 与 `bin/Release` 产物 SHA-256 相同 | 否 |
| `自检-0.0.10收窄两版本-godot.log` | 2026-11-17 19:31 | 否 | 158 | Release 构建的无头启动日志（0.113.1）。第 445 行 `Version=0.0.10.0` 证明部署产物是本轮构建；第 446 行 `entries=66, local=66` 证明三语文案全量加载；第 451 行 `ready. groups=15 entries=178 exemptions=8 schema=2 patches=1/1` 是加载完成的判据。设置文件未被改写。全文 13 行 `ERROR`，全部来自 `RegentFX` 的着色器预加载，与本模组无关 | 是 |
| `场景-卡牌奖励过滤.txt` | 2026-11-17 19:50 | 否 | 0.5 | 场景脚本。加棱彩宝石、跳进「邪教徒」战斗、秒杀敌人、等奖励生成。角色由场景运行器写死为铁甲战士 | 是 |
| `AutoSlayGate/` | 2026-11-17 19:50 | 否 | 128 | 临时门模组源码与构建产物：翻转 `NGame.IsReleaseGame`、挂候选集合与发牌结果探针。测试期间部署到游戏 `mods/`，结束后已移入回收站。共 25 个文件，含 `bin/`、`obj/` 两个构建目录 | 是 |
| `自动测试-场景-过滤生效-godot.log` | 2026-11-17 19:56 | 否 | 228 | 场景测试第一次运行，默认设置（总开关开、11 组开）。`[index] groups=11 excluded=122`、三条 `[filter] candidates=599 removed=122 kept=477`、探针最终候选 391 张、发牌结果 `HEADBUTT,GRAVE_WARDEN,TURBO`、退出码 0 | 是 |
| `自动测试-场景-总开关关闭-godot.log` | 2026-11-17 19:57 | 否 | 231 | 场景测试第二次运行，总开关关闭，其余相同。无 `[filter]` 与 `[index]` 行，探针候选 513 张，发牌结果含奥斯提组的 `POKE`，退出码 0 | 是 |
| `场景-遗物发牌过滤.txt` | 2026-11-17 20:10 | 否 | 0.6 | 场景脚本。依次验证星系仪、遗失宝箱、玻璃眼、祈祷轮、白星、梦境捕手六个发牌入口 | 是 |
| `自动测试-场景-遗物发牌-godot.log` | 2026-11-17 20:21 | 否 | 401 | 遗物发牌场景测试，种子 `ELITE1`，退出码 0。六个入口共 48 次过滤调用，移除数全部等于 122 或其稀有度子集 | 是 |

本轮未验证项：0.111.0 的加载与发牌行为（用户将降级实测）；0.107.1 被 `min_game_version` 挡下的表现；界面渲染与豁免页；1000 次发牌的统计口径；遗物发牌入口。

### 0.0.10 自动测试结果

两次运行都在 0.113.1、种子 `GEMTEST1`、铁甲战士、`--fast-mode=Fast` 下完成，退出码 0。方法见 `3.DOCS/017-T-AutoSlayer场景测试接入.md`。

| 检查 | 默认设置（总开关开） | 总开关关闭 |
|---|---|---|
| `[filter]` 行 | 3 条，`candidates=599 removed=122 kept=477` | 无 |
| `[index]` 行 | `groups=11 excluded=122` | 无 |
| 探针最终候选 | 391 张 | 513 张 |
| 应排除的 122 张 | 0 张残留 | 122 张全部在 |
| 默认豁免的 8 张 | 全在 | 全在 |
| 关闭的 4 组（中毒、小刀、箭矢、多人） | 全部仍在 | 全部仍在 |
| 发牌结果 | `HEADBUTT,GRAVE_WARDEN,TURBO` | `PERFECTED_STRIKE,POKE,UPROAR` |

逐组数值与「成员数减默认豁免」完全一致，见 `3.DOCS/017` 第 5.3 节。391 与 513 之间的 86 张差额是另一个模组移除的 `MGR_MOD_CARD_*` 卡牌，与本模组无关。

### 0.0.10 遗物发牌补测（2026-11-17 20:21）

种子 `ELITE1`，一次跑完，退出码 0。

| 入口 | 奖励次数 | 过滤调用 | 候选 / 移除 / 保留 |
|---|---|---|---|
| 星系仪 `ORRERY` | 5 | 15 | 599 / 122 / 477 |
| 遗失宝箱 `LOST_COFFER` | 1 | 3 | 599 / 122 / 477 |
| 玻璃眼 `GLASS_EYE` | 5 | 15 | 140/35、245/56、174/31 |
| 祈祷轮 `PRAYER_WHEEL` | 2 | 6 | 599 / 122 / 477 |
| 白星 `WHITE_STAR` | 2 | 6 | 599 / 122 / 477 |
| 梦境捕手 `DREAM_CATCHER` | 1 | 3 | 599 / 122 / 477 |

六个入口的移除数都等于默认设置的 122，或该稀有度子集内的 35、56、31。细节与踩坑见 `3.DOCS/017` 第九节。

### 0.0.11

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `AutoSlayGate111/AutoSlayGate.csproj` | 2026-11-17 23:32 | 否 | 2 | 0.111.x 用的测试门模组工程文件。net9.0，引用游戏目录的 `sts2.dll`、`0Harmony.dll`、`GodotSharp.dll` | 是 |
| `AutoSlayGate111/Gate.cs` | 2026-11-17 23:37 | 否 | 6 | 门模组源码：翻转 `IsReleaseGame`、轮询开局后调 `RelicCmd.Obtain` 注入棱彩宝石、候选集合与发牌结果两个探针、`GATE_NO_GEM` 对照开关 | 是 |
| `AutoSlayGate111/mod_manifest.json` | 2026-11-17 23:32 | 否 | 1 | 门模组清单。标识仍是 `AutoSlayGate`，`affects_gameplay` 为 `false` | 否 |
| `自动测试-111-autoslay-棱彩宝石过滤生效-godot.log` | 2026-11-17 23:37 | 否 | 292 | 0.111.1 持宝石的对局日志，种子 `GEM111`，铁甲战士。6 条 `[filter] candidates=429 removed=100 kept=329`，发牌结果 `LETHALITY,BEAM_CELL,SKEWER` | 是 |
| `自动测试-111-autoslay-对照组无宝石-godot.log` | 2026-11-17 23:41 | 否 | 312 | 0.111.1 不持宝石的对照日志，同种子 `GEM111`，`GATE_NO_GEM=1`。探针 85 张（本职业卡池全部），`[filter]` 0 条，三手奖励全为铁甲战士牌 | 是 |
| `自检-0.0.11设置注册-godot.log` | 2026-11-18 19:24 | 否 | 125 | 0.0.11 首次无头启动日志。`[compat] fool=True`、`groups=11/15 exemptions=11 pages=16`、`ready. groups=15 entries=180 exemptions=11 schema=3` | 否 |
| `自检-0.0.11设置注册-2-godot.log` | 2026-11-18 19:26 | 否 | 192 | 0.0.11 最终构建的无头启动日志。类型优先判定生效：`[compat] fool=True source=type`，其余计数与首次一致 | 是 |
| `visibility-check/visibility-check.csproj` | 2026-11-18 19:28 | 否 | 1 | 0.111 可见性静态验证工程。net9.0 控制台，引用 0.0.11 DLL、0.111 `sts2.dll` 与游戏依赖 | 是 |
| `visibility-check/Program.cs` | 2026-11-18 19:28 | 否 | 3 | 反射读取 `GameFeatures.HasFool`、`CardGroups.All`、`CardGroups.Visible` 与各组成员，断言 0.111 的可见分组为 11、多人组为 9、四个愚者组不可见；并算出默认排除数 0.113 为 121、0.111 为 99，默认豁免 10 条且无分组前缀 | 是 |

`AutoSlayGate111/` 下的 `bin/`、`obj/` 是 `dotnet build` 的产物，不逐个登记，可随时删除后重建。`visibility-check/` 下的 `bin/`、`obj/` 同理。测试结束后 `mods/AutoSlayGate` 已移入回收站。

本轮未验证项：0.111.1 上的遗物发牌入口（无场景脚本，控制台命令未驱动）；两版本的界面渲染；多人对局；0.107.1 被 `min_game_version` 挡下的表现。

### 0.0.11 跨版本实测结果

同一份 DLL（`PrismaticGemFilter.dll`，SHA-256 与 `9.SRC/bin/Release` 产物相同），默认设置，铁甲战士。

| 判据 | 0.111.1 | 0.113.1 |
|---|---|---|
| 游戏版本行 | `Release Version: v0.111.1` | `Release Version: v0.113.1` |
| RitsuLib 分支 | `Host=v0.111.1; runtime=0.111.0` | `Host=v0.113.1; runtime=0.113.0` |
| 就绪行 | `ready. groups=15 entries=178 exemptions=8 schema=2 patches=1/1` | 同左 |
| 过滤行 | `candidates=429 removed=100 kept=329` | `candidates=599 removed=122 kept=477` |
| 探针最终候选 | 329 张 | 391 张 |
| 不持宝石时 | 85 张，0 条过滤行 | 未做同条件对照 |

100 与 122 的差额是 0.111.1 没有的 22 张愚者机制牌，逐张列在 `3.DOCS/018-T-跨版本实测-111与113.md` 第五节。

### 0.0.12（2026-11-18 20:01，游戏 0.111.1）

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-0.0.12-111设置注册-godot.log` | 2026-11-18 20:01 | 否 | 152 | 0.0.12 在 0.111.1 的无头启动日志。`reset. schema 3 -> 4`、`fool=False`、`groups=8/11 exemptions=10 pages=12`、`ready. groups=11 entries=143 exemptions=10 schema=4` | 是 |
| `自动测试-111-autoslay-0.0.12-godot.log` | 2026-11-18 20:02 | 否 | 258 | 0.0.12 在 0.111.1 的对局日志。`AutoSlayGate111` 注入棱彩宝石后，3 条 `[filter] candidates=429 removed=99 kept=330 groups=8` | 是 |
| `自动测试-111-autoslay-0.0.12-2-godot.log` | 2026-11-18 20:13 | 否 | 280 | 最终构建（含豁免页新文案）在 0.111.1 的对局日志。同样的 3 条 `[filter] candidates=429 removed=99 kept=330 groups=8` | 是 |

### 0.0.12 测试结果

游戏版本 `v0.111.1`，同一份 0.0.12 DLL，默认设置。

| 判据 | 数值 |
|---|---|
| `[compat]` | `fool=False source=type` |
| 设置注册 | `groups=8/11 exemptions=10 pages=12` |
| 就绪行 | `ready. groups=11 entries=143 exemptions=10 schema=4 patches=1/1` |
| 过滤行 | `candidates=429 removed=99 kept=330 poolGate=0 otherPool=344` |
| 过滤行次数 | 3 |

`removed=99` 的推导：0.111 可见启用组共 110 行，减去跨组重复的「铸剑者」1 条，再减去 10 个豁免成员行（9 张不同卡，大爆炸在铸造与辉星各一行）。`BRIGHTEN_UP` 在 0.111 不可见，不计入。详见 `3.DOCS/020-T-豁免跨组同步与111复测.md`。

未验证：双页豁免开关同步的实际界面操作、0.113.1 回归、0.111 的遗物发牌入口。

### 0.0.13（2026-11-18 20:40 至 21:31）

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `自检-0.0.13-111设置注册-godot.log` | 2026-11-18 20:40 | 否 | 133 | 0.0.13 在 0.111.1 的无头启动日志。`reset. schema 4 -> 5, filtered=121 explicitExemptions=10`、`registered ... groups=11 entries=143 filtered=99 stored=121 pages=12`、`ready ... filtered=121 schema=5` | 是 |
| `自动测试-111-autoslay-0.0.13-state-godot.log` | 2026-11-18 20:43 | 否 | 262 | 0.0.13 在 0.111.1 的对局日志。`[state]` 五行验证分组开关与单卡开关共用同一集合；`[index] groups=8 filtered=99`；3 条 `[filter] removed=99` | 是 |
| `state-check/Program.cs` | 2026-11-18 20:38 | 否 | 7 | 数据级状态代数测试。反射加载 DLL，验证默认集合、0.111/0.113 可见排除数、活跃组数、跨组卡一致性、分组批量写入、列表不可变与排序去重 | 是 |
| `state-check/state-check.csproj` | 2026-11-18 20:38 | 否 | 1.3 | 上面的测试工程文件，引用 0.111 `sts2.dll` 与 0.0.13 DLL | 是 |
| `state-check-result.txt` | 2026-11-18 20:58 | 否 | 1.4 | 数据级测试输出。16 项 PASS，`failures=0` | 是 |
| `自检-0.0.13-111设置注册-2-godot.log` | 2026-11-18 20:58 | 否 | 132 | 最终构建的无头启动日志。`registered ... groups=11 entries=143 filtered=99 stored=121 pages=12`、`ready ... filtered=121 schema=5` | 是 |
| `自动测试-111-autoslay-0.0.13-state-2-godot.log` | 2026-11-18 20:59 | 否 | 282 | 最终构建的对局日志。`[state]` 五行与首轮相同；`[index] groups=8 filtered=99`；3 条 `[filter] removed=99` | 是 |
| `自检-0.0.13-113设置注册-godot.log` | 2026-11-18 21:15 | 否 | 206 | 0.113.1 无头启动日志。按 0.113 程序集重新构建；`fool=True source=type`、`groups=15 entries=180 filtered=121 stored=121 pages=16`、`ready ... schema=5`、无重置行 | 是 |
| `state-check-result-113.txt` | 2026-11-18 21:15 | 否 | 1.4 | 0.113 构建下的数据级测试输出。16 项 PASS，`failures=0` | 是 |
| `自动测试-113-autoslay-0.0.13-godot.log` | 2026-11-18 21:16 | 否 | 267 | 0.113.1 默认设置机械人对局，种子 `GEM113`，缺陷机器人。`[index] groups=11 filtered=121`；3 条 `[filter] candidates=513 removed=94 kept=419 poolGate=27 otherPool=427`；绑定探针五行 | 是 |
| `场景-113-forge-off-godot.log` | 2026-11-18 21:22 | 否 | 212 | 0.113.1 分组关闭场景，铁甲战士。设置 `filtered=111`；`[index] groups=10 filtered=111`；3 条 `[filter] removed=111` | 是 |
| `场景-113-master-off-godot.log` | 2026-11-18 21:22 | 否 | 215 | 0.113.1 总开关关闭场景，铁甲战士。`master=False`；无 `[index]` 与 `[filter]`；候选 513 张，发牌含其他职业牌 | 是 |
| `场景-113-遗物发牌-godot.log` | 2026-11-18 21:24 | 否 | 386 | 0.113.1 六个遗物发牌入口场景，种子 `ELITE1`。48 次过滤、16 手奖励；移除数 121、34、56、31 | 是 |
| `自动测试-113-autoslay-0.0.13-forge-off-2-godot.log` | 2026-11-18 21:27 | 否 | 263 | 0.113.1 分组关闭的干净复测，缺陷机器人。设置 `filtered=111`；`[index] groups=10 filtered=111`；3 条 `[filter] removed=84 poolGate=27`；探针恢复 `count=111` | 是 |
| `自检-0.0.13-113设置恢复-godot.log` | 2026-11-18 21:31 | 否 | 215 | 收尾后的最终无头启动。设置已还原为 121 张，`groups=15 entries=180 filtered=121 stored=121`，无 `[gate]`，无重置行，测试门已移出 | 是 |

### 0.0.13 测试结果

游戏版本 `v0.111.1`，最终构建的 0.0.13 DLL，默认设置。最终构建证据 `8.TEST/0.0.13/自检-0.0.13-111设置注册-2-godot.log` 与 `8.TEST/0.0.13/自动测试-111-autoslay-0.0.13-state-2-godot.log`；首轮重置证据 `8.TEST/0.0.13/自检-0.0.13-111设置注册-godot.log`。

| 判据 | 数值 |
|---|---|
| `[compat]` | `fool=False source=type` |
| 设置重置（首轮 20:40） | `schema 4 -> 5, filtered=121 explicitExemptions=10` |
| 设置注册 | `groups=11 entries=143 filtered=99 stored=121 pages=12` |
| 就绪行 | `ready. groups=11 entries=143 filtered=121 schema=5 patches=1/1` |
| 索引 | `[index] groups=8 filtered=99` |
| 过滤行 | `candidates=429 removed=99 kept=330 poolGate=0 otherPool=344` |
| 过滤行次数 | 3 |
| 绑定探针 | `forgeOff` 后 `smithExempt=True bigBangExempt=True forge=False star=True forgeCount=0/11 starCount=24/27`；`forgeOn` 后 `smithExempt=False bigBangExempt=False forge=True star=True forgeCount=11/11 starCount=26/27` |
| 设置恢复 | 与测试前 `settings.json` 逐字节一致 |

`filtered=99` 是 0.111 可见分组的排除数；`stored=121` 是落盘集合总数，多出的 22 条是隐藏愚者卡，保证切到 0.113 后默认状态正确。

未验证：双页开关的实际界面视觉效果、0.111 的遗物发牌入口。

### 0.0.13 在 0.113.1 的测试结果

游戏版本 `v0.113.1`，按 0.113 程序集重新构建的 0.0.13 DLL，SHA-256 `6a706cee7f590b32a648c2662a08775c94431e4ab4a67ed03bcc1cc027cf0774`。设置文件测试前与恢复后 SHA-256 相同：`06aba9caef51a9723fbb88629fd9fec28f1f8e3bbfcdd25baa247279c514980e`。

| 判据 | 数值 |
|---|---|
| `[compat]` | `fool=True source=type` |
| 设置注册 | `groups=15 entries=180 filtered=121 stored=121 pages=16` |
| 就绪行 | `ready. groups=15 entries=180 filtered=121 schema=5 patches=1/1` |
| 重置行 | 无 |
| 默认索引 | `[index] groups=11 filtered=121` |
| 默认过滤（缺陷机器人） | `candidates=513 removed=94 kept=419 poolGate=27 otherPool=427`，3 次 |
| 默认发牌 | `PIERCING_WAIL,TURBO,SUCKER_PUNCH`（三个职业） |
| 分组关闭索引 | `[index] groups=10 filtered=111` |
| 分组关闭过滤（缺陷机器人） | `removed=84 poolGate=27`，3 次 |
| 分组关闭过滤（铁甲战士场景） | `removed=111 poolGate=0`，3 次 |
| 总开关关闭 | 0 条 `[index]`、0 条 `[filter]`；候选 513 张 |
| 遗物发牌 | 48 次过滤、16 手奖励；移除数 `121 / 34 / 56 / 31` |
| 113 愚者默认排除 | 22 张，默认探针中 0 张 |
| 显式默认豁免 | 10 张，默认探针中全部存在 |
| 绑定探针 | `forgeOff` 后 `forge=False star=True forgeCount=0/11 starCount=24/27`；`forgeOn` 后 `forgeCount=11/11 starCount=26/27`；恢复 `count=121` / `111` |

`poolGate=27` 是缺陷机器人本职业卡池中的充能球与集中牌。默认集合 121 张中有 27 张属于本职业，按 SP-004 放行；其余 94 张跨职业牌排除。分组关闭后排除集合为 111 张，本职业仍放行 27 张，因此跨职业移除数降到 84。

详细结论、入口分类和未验证项见 `3.DOCS/022-T-0.113全量回归.md`。

### 0.0.14（2026-11-19 01:30 至 01:48）

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `state-check/Program.cs` | 2026-11-19 01:32 | 否 | 9.1 | 数据级分层状态测试。验证默认池、0.111 可见池、13 张 1 豁免、跨组 `BIG_BANG`、分组与豁免互不写入 | 是 |
| `state-check/state-check.csproj` | 2026-11-19 01:32 | 否 | 2.2 | 数据级测试工程。引用 0.0.14 DLL、0.113.1 游戏程序集与 RitsuLib | 是 |
| `state-check-result.txt` | 2026-11-19 01:33 | 否 | 1 | 数据级测试输出。23 项 PASS、0 项 FAIL | 是 |
| `自检-0.0.14-113设置注册-godot.log` | 2026-11-19 01:35 | 否 | 211 | 0.113.1 无头启动日志。schema 5 到 6 重置、默认池 121、无头注册与补丁应用 | 是 |
| `headless-014-console.log` | 2026-11-19 01:34 | 否 | 213 | 无头启动的控制台输出。首次运行因 70 秒超时结束，仅作过程留档 | 否 |
| `场景-014-默认-godot.log` | 2026-11-19 01:39 | 否 | 141 | 0.113.1 默认场景。3 条 `removed=121`、发牌结果与场景完成行 | 是 |
| `scenario-default-console.log` | 2026-11-19 01:39 | 否 | 143 | 默认场景的控制台输出 | 否 |
| `场景-014-默认-最终构建-godot.log` | 2026-11-19 01:48 | 否 | 141 | 最终 0.0.14 DLL 的默认场景复测。3 条 `removed=121`，场景完成、退出码 0 | 是 |
| `scenario-final-console.log` | 2026-11-19 01:48 | 否 | 143 | 最终构建默认场景的控制台输出 | 否 |
| `场景-014-forge-off-cross-godot.log` | 2026-11-19 01:40 | 否 | 142 | 0.113.1 跨组场景。forge 关闭、`BIG_BANG` 取消豁免、`THE_SMITH` 豁免；`filtered=112`、3 条 `removed=112` | 是 |
| `scenario-forge-off-console.log` | 2026-11-19 01:40 | 否 | 143 | 跨组场景的控制台输出 | 否 |

`state-check/` 下的 `bin/` 与 `obj/` 是 `dotnet run` 产物，整体随目录保留，不逐个登记。

### 0.0.14 测试结果

| 判据 | 数值 |
|---|---|
| 数据级测试 | 23 项 PASS，`failures=0`；默认池 121、0.111 可见 99、13 张 1 豁免组开 12/13、组关本组 0/13、跨组 1/13 |
| 无头重置 | `reset. schema 5 -> 6, groups=11/15 exemptions=10 filtered=121` |
| 无头注册 | `groups=11/15 entries=180 filtered=121 exemptions=10 schema=6 pages=16` |
| 默认索引 | `[index] groups=11 filtered=121` |
| 默认过滤 | `[filter] groups=11 candidates=513 removed=121 kept=392 poolGate=0 otherPool=428`，3 次 |
| 默认发牌 | `HEADBUTT,GRAVEBLAST,TURBO` |
| 跨组索引 | `[index] groups=10 filtered=112` |
| 跨组过滤 | `[filter] groups=10 candidates=513 removed=112 kept=401`，3 次 |
| 跨组发牌 | `HEADBUTT,FEAR,SWEEPING_BEAM` |
| 设置恢复 | 测试前与恢复后 SHA-256 均为 `9c0831ed93123de80e0c1de81eea0ac0a3426c834de36fc2aeb890784076f928` |
| 最终产物 | `PrismaticGemFilter.dll` SHA-256 `d7ff07526d505247919f6acd7bd1e625f8527b23a7b44427d79a3cd37c304e7d`；最终默认场景通过 |

跨组场景的设置：`Groups.forge=false`，`Exemptions` 移除 `BIG_BANG`、加入 `THE_SMITH`。forge 的 9 张非豁免牌退出池；`BIG_BANG` 因 star 仍开启且未豁免重新入池；`THE_SMITH` 保持豁免。池从 121 变成 112，证明关闭一组不会误删其他开启组仍需要的跨组牌。

详细结论与踩坑见 `3.DOCS/023-T-分组开关与卡牌豁免分层.md`。

### 0.0.15（2026-11-24 01:02 至 22:39）

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `state-check/Program.cs` | 2026-11-24 01:02 | 否 | 10 | 数据级 30 项测试：默认池 126、0.111 可见池 104、灵魂组 9 张、默认豁免 3 张、跨组 `DIRGE` | 是 |
| `state-check/state-check.csproj` | 2026-11-24 01:02 | 否 | 3 | 测试工程文件，引用 0.113.1 游戏程序集与 0.0.15 DLL | 是 |
| `state-check-result.txt` | 2026-11-24 01:02 | 否 | 2 | 数据级测试输出，30 项 PASS，`failures=0` | 是 |
| `自检-0.0.15-111设置注册-godot.log` | 2026-11-24 01:04 | 否 | 394 | 0.0.15 在 0.111.1 的无头启动日志。`reset. schema 6 -> 7, groups=12/16 exemptions=13 filtered=126`、`registered ... groups=9/12 entries=154 filtered=104 exemptions=13 schema=7 pages=13`、`ready ... groups=12 entries=154 filtered=104` | 是 |
| `自动测试-111-autoslay-0.0.15-灵魂牌-godot.log` | 2026-11-24 01:11 | 否 | 910 | 0.111.1 对局日志，种子 `SOUL15`，铁甲战士。3 条 `[filter] candidates=429 removed=70`；候选保留 3 张默认豁免灵魂牌，6 张未豁免灵魂牌不在；自动局 300 秒超时终止 | 是 |
| `场景-113-0.0.15-灵魂牌-godot.log` | 2026-11-24 01:38 | 否 | 474 | 0.113.1 场景日志，种子 `GEMTEST1`，铁甲战士。`Release Version: v0.113.1`、`reset. schema 6 -> 7 ... filtered=126`、`registered ... groups=12/16 entries=191 filtered=126 pages=17`；3 条 `[filter] candidates=678 removed=126`；3 次探针均 466 张，灵魂牌结果与 0.111.1 一致；场景成功退出 0 | 是 |
| `场景-113-console.log` | 2026-11-24 01:38 | 否 | 478 | 0.113.1 场景的控制台输出。退出码 0；结尾的字体、ObjectDB、资源泄漏警告来自游戏退出清理 | 否 |
| `场景-113-无宝石.txt` | 2026-11-24 22:36 | 否 | 0.3 | RQ-005 的 0.113.1 对照组脚本。不注入棱彩宝石，只打一场战斗并读候选 | 是 |
| `场景-113-无宝石-godot.log` | 2026-11-24 22:37 | 否 | 431 | 0.113.1 无宝石对照日志。3 次探针均 85 张，`[filter]` 0 条，奖励 `ARMAMENTS,SETUP_STRIKE,THUNDERCLAP`，场景退出 0 | 是 |
| `场景-113-无宝石-console.log` | 2026-11-24 22:37 | 否 | 435 | 无宝石对照的控制台输出。退出码 0；结尾清理警告与上一场景相同 | 否 |

### 0.0.15 测试结果

| 判据 | 数值 |
|---|---|
| 数据级测试 | 30 项 PASS，`failures=0`；默认池 126、0.111 可见池 104、灵魂组 9 张、默认豁免 3 张 |
| 0.111.1 无头重置 | `reset. schema 6 -> 7, groups=12/16 exemptions=13 filtered=126` |
| 0.111.1 无头注册 | `groups=9/12 entries=154 filtered=104 exemptions=13 schema=7 pages=13` |
| 0.111.1 就绪行 | `ready. groups=12 entries=154 filtered=104 exemptions=13 schema=7 patches=1/1` |
| 0.111.1 对局过滤 | `[filter] groups=9 candidates=429 removed=70 kept=359 poolGate=34 otherPool=343`，3 次 |
| 0.113.1 无头重置 | `reset. schema 6 -> 7, groups=12/16 exemptions=13 filtered=126` |
| 0.113.1 无头注册 | `groups=12/16 entries=191 filtered=126 exemptions=13 schema=7 pages=17` |
| 0.113.1 就绪行 | `ready. groups=16 entries=191 filtered=126 exemptions=13 schema=7 patches=1/1` |
| 0.113.1 对局过滤 | `[filter] groups=12 candidates=678 removed=126 kept=552 poolGate=0 otherPool=593`，3 次 |
| 0.113.1 候选探针 | 3 次均为 466 张；3 张默认豁免灵魂牌在集合中，6 张未豁免灵魂牌不在 |
| 0.113.1 场景结果 | `Scenario completed successfully`；退出码 0 |
| 0.113.1 无宝石对照 | 3 次探针均 85 张；`[filter]` 0 条；奖励 `ARMAMENTS,SETUP_STRIKE,THUNDERCLAP`；退出码 0 |
| 默认豁免灵魂牌 | `GRAVE_WARDEN`、`CAPTURE_SPIRIT`、`SEANCE` 在候选集合中 |
| 未豁免灵魂牌 | `DEVOUR_LIFE`、`DIRGE`、`HAUNT`、`REAVE`、`SEVERANCE`、`SOUL_STORM` 不在候选集合中 |
| 多人灵魂牌 | `GLIMPSE_BEYOND`、`SOULBOUND` 不在候选集合中；多人组默认关闭 |
| 自动局状态 | 300 秒超时终止，未完整跑完；过滤证据已取得 |
| 设置恢复 | 测试前与恢复后 SHA-256 均为 `0aeccaee47fc69da98fd92b65d7db4d58a91b8dca73a1a2874442d48ab2ddc1f` |

`removed=70` 小于默认池 126，是因为 0.111.1 的候选集合不包含过滤池全部卡牌，且本职业门控排除了部分候选。不能用 `removed=104` 作为对局期望值；应检查具体灵魂牌是否在候选集合中。`0.113.1` 的 `removed=126` 与默认池一致，因为铁甲战士候选集合包含全部默认排除池。

## 测试方法

方法、判据、结束进程与失败对照见 `3.DOCS/015-T-无头启动测试方法.md`。本节只留最短流程。

```
SteamAppId=2868840 "<游戏目录>/SlayTheSpire2.exe" --headless
```

日志路径 `%APPDATA%\SlayTheSpire2\logs\godot.log`。日志每次启动轮转覆盖，所以等到 `ready.` 行后先复制到本目录的版本子文件夹，再结束进程。

游戏会把 `Debug` 级别的日志整个丢掉，判据要打在 `Info` 或更高等级。

### 对局内场景测试

先部署临时门模组 `AutoSlayGate`（源码在本目录），再带 `--scenario` 启动：

```
SteamAppId=2868840 "<游戏目录>/SlayTheSpire2.exe" --headless --scenario="<脚本路径>" --seed=GEMTEST1 --fast-mode=Fast
```

细节与坑见 `3.DOCS/017-T-AutoSlayer场景测试接入.md`。测试后必须把 `mods/AutoSlayGate` 移入回收站，并确认日志里不再出现 `[gate]` 行。

0.111.x 没有场景运行器，`--scenario` 会被静默忽略。该版本改用机器人开局，门模组源码在 `8.TEST/0.0.11/AutoSlayGate111/`：

```
SteamAppId=2868840 "<游戏目录>/SlayTheSpire2.exe" --headless --autoslay --seed=GEM111
```

机器人开局的角色由种子决定，做对照必须用同一个种子。示例的 `GEM111` 是铁甲战士，`GEM112` 会开到第三方模组的角色。加环境变量 `GATE_NO_GEM=1` 跑不注入宝石的对照组。细节见 `3.DOCS/018-T-跨版本实测-111与113.md`。

## 计划

端到端测试在 `2.PLANS/001-G-项目大纲.md` 的 P6 阶段补齐。对局内的发牌验证已改用游戏自带的场景脚本，见 `2.PLANS/011-S-接入自动测试.md`；统计口径（1000 次发牌）与界面目视仍待补。0.111.1 的遗物发牌入口需要在没有场景脚本的前提下另行驱动控制台命令。本地门禁脚本放入 `8.TEST/CI-CD/`。
