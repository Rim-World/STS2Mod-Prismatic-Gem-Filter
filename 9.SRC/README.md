# Prismatic Gem Filter

Slay the Spire 2 模组。玩家持有遗物「棱彩宝石」时，按玩家勾选的开关，把跨角色发牌中该职业专属、其他职业无法使用的卡牌排除出候选。

仓库：`https://github.com/Rim-World/STS2Mod-Prismatic-Gem-Filter`
许可：MIT

## 状态

0.0.3 是最小工程加判定模式开关。只实现「奥斯提牌」一组，开关恒为开。

过滤范围与原版棱彩宝石的作用面严格相等：只有在棱彩宝石确实加宽本次发牌时才排除候选牌。万花筒、海玻璃、色彩哲学家、垃圾堆、普通商店、战斗内生成的飞溅与恋人都不受影响。设置页里可以把判定放宽为「只要出现其他职业的牌就生效」。

已勾选开关、豁免清单、其余 12 个分组在后续阶段补齐。

## 依赖

- Slay the Spire 2 0.113.1（目标兼容 0.107.1、0.111.0）
- RitsuLib 0.6.13，提供补丁器、设置页与设置持久化
- Harmony，游戏自带

## 构建

需要 .NET SDK 9 与本机游戏安装。

```
dotnet build PrismaticGemFilter.csproj -c Release
```

构建完成后自动把 `PrismaticGemFilter.dll` 与 `mod_manifest.json` 复制到游戏目录的 `mods/PrismaticGemFilter/`。

本机路径写在 csproj 里，换机器时覆盖即可：

| 属性 | 默认值 | 说明 |
|---|---|---|
| `Sts2Dir` | `C:\Game\Steam\steamapps\common\Slay the Spire 2` | 游戏安装目录 |
| `Sts2DataDir` | `$(Sts2Dir)\data_sts2_windows_x86_64` | 托管程序集所在目录 |
| `RitsuLibDeployRoot` | `C:\Game\Steam\steamapps\workshop\content\2868840\3747602295` | RitsuLib 创意工坊安装目录 |
| `RitsuLibCompatTarget` | `0.113.0` | RitsuLib 变体目录名 |
| `CopyModOnBuild` | `true` | 设为 `false` 时只产出，不部署 |

```
dotnet build PrismaticGemFilter.csproj -c Release "/p:Sts2Dir=D:\Steam\steamapps\common\Slay the Spire 2"
```

编译期绑定的是创意工坊里 RitsuLib 的 `compat/0.113.0/` 与 `shared/` 程序集，与游戏运行时实际加载的那一套一致。所有引用都是 `Private=false`，不复制进产物。

## 目录

| 路径 | 内容 |
|---|---|
| `src/Plugin.cs` | 模组入口 |
| `src/CardFilter.cs` | 判定规则，纯函数 |
| `src/CardCreationPatch.cs` | 候选牌枚举处的后置补丁 |
| `src/ExclusionTable.cs` | 排除名单 |
| `src/FilterMode.cs` | 判定模式的枚举 |
| `src/ModSettings.cs` | 设置项、设置页与持久化 |
| `mod_manifest.json` | 模组清单 |

## 设置

游戏设置界面里有「棱彩宝石过滤」一页，含一个下拉选项：

| 取值 | 含义 |
|---|---|
| 只在棱彩宝石加宽本次发牌时生效 | 默认。与原版棱彩宝石的作用面相等 |
| 只要出现其他职业的牌就生效 | 任何来源的跨职业候选都会被排除 |

取值落盘在 `mod_data/PrismaticGemFilter/settings.json`，按 Steam 账号隔离。

## 许可

MIT。
