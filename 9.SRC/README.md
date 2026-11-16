# Prismatic Gem Filter

Slay the Spire 2 模组。玩家持有遗物「棱彩宝石」时，按玩家勾选的开关，把跨角色发牌中该职业专属、其他职业无法使用的卡牌排除出候选。

仓库：`https://github.com/Rim-World/STS2Mod-Prismatic-Gem-Filter`
许可：MIT

## 状态

0.0.5 具备完整的开关体系：13 个分组开关按职业分节，一个总开关，每个分组一个豁免子页。设置界面提供英语、简体中文、繁体中文三种文案。

名单只收录能进入战斗奖励候选的卡牌：初始牌与先古牌已经剔除，多人限定卡只出现在「多人卡牌」一组。升级到 0.0.5 时存档里的设置会重置为默认值，见「设置」一节的说明。

过滤范围与原版棱彩宝石的作用面严格相等：只有在棱彩宝石确实加宽本次发牌时才排除候选牌。万花筒、海玻璃、色彩哲学家、垃圾堆、普通商店、战斗内生成的飞溅与恋人都不受影响。设置页里可以把判定放宽为「只要出现其他职业的牌就生效」。

跨版本实测（0.107.1、0.111.0）安排在功能全部完成之后。

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
| `src/CardCreationPatch.cs` | 候选牌枚举处的后置补丁，含成员自检 |
| `src/CardGroups.cs` | 13 个分组的定义 |
| `src/CardGroupTable.cs` | 分组与卡牌标识的对照表，由脚本生成，不要手改 |
| `src/ExclusionTable.cs` | 默认豁免清单 |
| `src/FilterIndex.cs` | 排除索引，设置变更后重建 |
| `src/FilterMode.cs` | 判定模式的枚举 |
| `src/Localization.cs` | 界面文案取词 |
| `src/ModSettings.cs` | 设置模型、绑定、设置页与持久化 |
| `localization/*.json` | 三份界面文案，作为嵌入资源打进 DLL |
| `mod_manifest.json` | 模组清单 |

## 设置

游戏设置界面里有「棱彩宝石过滤」一页，分三部分。

**生效范围**：总开关，以及一个两取值的下拉。

| 取值 | 含义 |
|---|---|
| 只在棱彩宝石加宽本次发牌时生效 | 默认。与原版棱彩宝石的作用面相等 |
| 只要出现其他职业的牌就生效 | 任何来源的跨职业候选都会被排除 |

**分组开关**：按职业分节，共 13 项。首次启动勾选 9 项：奥斯提牌、召唤牌、灾厄牌、充能球牌、集中牌、奇巧牌、扎穿牌、铸剑牌、星牌。其余 4 项默认不勾：箭矢牌、刀牌、毒牌、多人卡牌。

**豁免清单**：每个分组后有一个入口，打开后可以逐张勾选该组不得排除的牌。默认已豁免 7 条：精神过载（灾厄）、聚变与暴涨（球）、大爆炸（铸剑与星）、汇流（星）、超能光束（集中）。

改动即时生效，不需要重启。设置落盘在 `mod_data/PrismaticGemFilter/settings.json`，按 Steam 账号隔离。

存档里带一个架构版本号。版本号与模组当前版本不一致时，启动会把全部设置重置为默认值并写回存档。0.0.4 及更早的存档没有这个字段，因此升级后首次启动会重置一次，之后不再重置。

界面文案随游戏语言变化：简体中文与繁体中文见汉语，其余语言见英语。卡名随游戏自身的语言显示。

## 许可

MIT。
