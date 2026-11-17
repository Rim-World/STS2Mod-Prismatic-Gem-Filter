# 007-T RitsuLib 兼容包与安装形态

- 建立时间：2026-11-15 16:58
- 结论：所谓「对应兼容包」，指 RitsuLib 针对某一版游戏 API 单独编译的那一份运行库。玩家装错版本时，框架会拒绝加载，并连带挡住依赖它的模组。

> 2026-11-17 补注：本机安装的 RitsuLib 已升到 0.6.14。兼容包结构未变，仍是 `compat/0.107.1`、`compat/0.111.0`、`compat/0.113.0` 三个分支，加上 `shared/` 与 `assets.zip`。最低依赖跟随上游最新的策略见 [ADR-010](../4.ADR/010-依赖版本跟随RitsuLib最新发布.md)。

## 一、为什么框架要分版本编译

游戏本体是 .NET 程序集 `sts2.dll`。每次游戏更新，只要公开类型或方法签名变化，任何编译期引用它的程序集都要重新编译。

RitsuLib 大量直接引用游戏类型。它无法只靠反射绕开差异，所以采取另一条路：**同一份源码，按不同游戏 API 各编译一次**。

RitsuLib 的兼容目标写在 `build/RitsuLib.Compatibility.props`：

```
<RitsuLibCompatTargets>0.113.0;0.111.0;0.107.1</RitsuLibCompatTargets>
```

这三个值叫兼容目标，注意两点：

1. 它指的是**API 签名版本**，不是游戏补丁号。游戏实际版本是 0.113.1，兼容目标写的是 0.113.0。
2. 目标集不含 0.113.1、0.111.1 这类补丁号。补丁号由运行时的匹配规则吸收。

源码里用条件编译区分版本。`build/RitsuLib.CompatDefines.targets` 按当前兼容目标生成宏：

```xml
<DefineConstants Condition="$([MSBuild]::VersionGreaterThanOrEquals('$(Sts2ApiCompat)', '0.107.1'))">$(DefineConstants);STS2_AT_LEAST_0_107_1</DefineConstants>
<DefineConstants Condition="$([MSBuild]::VersionGreaterThanOrEquals('$(Sts2ApiCompat)', '0.111.0'))">$(DefineConstants);STS2_AT_LEAST_0_111_0</DefineConstants>
```

宏的名字表示「宿主 API 至少是这个版本」。业务代码据此选择调用哪套签名。

## 二、一份安装目录长什么样

RitsuLib 的运行时不是单个 DLL，而是一个带子目录的文件夹。构建脚本 `build/RitsuLib.Deployment.targets` 生成如下结构：

```
mods/STS2-RitsuLib/
├── mod_manifest.json           mod 清单，id 为 STS2-RitsuLib
├── STS2-RitsuLib.dll           加载器，游戏只认它
├── ritsulib-variants.manifest  模块清单，含每个 DLL 的 SHA-256
├── assets.zip                  图片、多语言、内置主题
├── viewer/                     调试日志查看器
├── RitsuLib.References.props   供子模组以目录方式引用
├── shared/
│   ├── STS2-RitsuLib.Shared.dll
│   ├── STS2-RitsuLib.Ui.dll
│   └── STS2-RitsuLib.Settings.dll
└── compat/
    └── 0.113.0/
        ├── STS2-RitsuLib.dll           注意与根目录同名，这是运行时本体
        ├── STS2-RitsuLib.Runtime.dll
        └── compat-target.txt
```

`shared/` 放跨版本通用的代码，只留一份。`compat/<目标>/` 放随游戏版本变化的代码，可以有多个。

这就解释了两类发布包的区别：

| 包 | `compat/` 里有几个目录 | 大小（v0.6.13） |
|---|---|---|
| 单版本包，如 `STS2.RitsuLib.0.6.13.github.zip` | 1 个（0.113.0） | 5,462,919 字节 |
| 兼容包，如 `STS2.RitsuLib.Compat.0.107.1.0.6.13.github.zip` | 1 个（0.107.1） | 5,465,395 字节 |
| 变体包 `STS2-RitsuLib.0.6.13.variant-pack.zip` | 3 个（0.107.1、0.111.0、0.113.0） | 12,740,778 字节 |

变体包约等于三份单版本包去掉重复的 `shared/`。它变大约 2.3 倍，换来一次安装通吃三个版本。

## 三、运行时怎么选

加载器 `STS2-RitsuLib.dll` 先读 `ritsulib-variants.manifest`，再按下面的规则选目录。逻辑在 `components/loader/BundleLayout.cs:44-48`：

```csharp
var compatible = candidates.Where(candidate => host == null || candidate.Version <= host)
    .OrderByDescending(candidate => candidate.Version).ToArray();
if (compatible.Length == 0)
    throw new NotSupportedException($"This RitsuLib bundle does not support game version {host}.");
var selected = compatible[0];
```

规则一句话：**在所有不高于当前游戏版本的兼容目标里，取最高的那个**。

| 游戏版本 | 变体包选中的目标 | 单版本包能否加载 |
|---|---|---|
| 0.107.1 | 0.107.1 | 只有 0.107.1 包可以 |
| 0.111.0 | 0.111.0 | 只有 0.111.0 包可以 |
| 0.113.1 | 0.113.0 | 只有 0.113.0 包可以 |

选中的目标会写进日志，形如：

```
[RitsuLib.Loader] Host=0.113.1; runtime=0.113.0.
```

核对玩家日志时看这一行即可。

## 四、校验与失败模式

加载器读清单后，会逐个校验模块的 SHA-256（`BundleLayout.cs:73-81`），并核对程序集名字。任何一项不符就抛异常。

三种常见装错：

| 情形 | 表现 | 玩家看到的 |
|---|---|---|
| 只复制了根目录的 `STS2-RitsuLib.dll`，没带子目录 | 找不到模块文件 | 框架初始化抛异常 |
| 用了 0.113.0 单版本包，但游戏是 0.107.1 | 没有不高于宿主的兼容目标 | `This RitsuLib bundle does not support game version ...` |
| 手工改过某个 DLL，或复制不完整 | 哈希不符 | `RitsuLib module hash mismatch` |

RitsuLib 失败后，依赖它的模组也会被挡下。游戏侧的判定在 `TMP/0.0.1/调研-反编译/game/ModManager.cs:723-727`：

```csharp
Mod mod2 = _mods.FirstOrDefault((Mod m) => m.manifest?.id == declaredDependency.id);
if (mod2 == null || mod2.state != ModLoadState.Loaded)
{
    list2.Add(declaredDependency.id);
}
```

`list2` 非空时，游戏报 `MOD_ERROR.MISSING_DEPENDENCY`，把本模组标为加载失败。玩家看到的是明确的依赖缺失提示，不是崩溃。这是本模组选择硬依赖的安全前提。

## 五、玩家从哪拿到正确的那一份

**创意工坊。** 发布脚本 `scripts/release_lib/workshop.py:168-171` 把整个变体包内容复制进工坊内容的 `content/` 目录。所以订阅创意工坊的玩家拿到的是变体包，不需要自己挑版本。

**手动安装。** 从 GitHub Release 下载 `STS2-RitsuLib.<版本>.variant-pack.zip`，解压后把整个文件夹放进 `Slay the Spire 2/mods/`。这份也通吃三个版本，是唯一不容易装错的选择。

单版本包主要给开发者和只想装一份的玩家。选错版本会触发上面的失败模式。

## 六、本模组要做什么

1. **运行期依赖**写在 `mod_manifest.json`，写明最低版本：

```json
{
  "dependencies": [
    { "id": "STS2-RitsuLib", "min_version": "0.6.13" }
  ]
}
```

字段名是 `min_version` 而不是 `minVersion`。游戏侧的 `ModDependency` 用 `[JsonPropertyName("min_version")]` 映射到 C# 字段 `minVersion`，见 `game_full/sts2.decompiled.cs:168363-168375`。

2. **编译期引用**按编译基线选包。本项目的交付形态是一个整合包（[ADR-002](../4.ADR/002-三版本交付形态.md)），编译基线是本机的 0.113.1 程序集，因此引用 `STS2.RitsuLib`（最新包），不引用 `STS2.RitsuLib.Compat.*`。

不要反过来做：拿 `STS2.RitsuLib.Compat.0.107.1` 去配 0.113.1 的 `sts2.dll`。兼容包内附的框架程序集是针对旧 API 编译的，与新版游戏类型混用会引入无谓的风险。

运行期不必担心选错变体。所有模块的程序集标识都是 `STS2-RitsuLib, Version=0.6.13.0`，加载器按玩家安装的变体提供同名同版本的模块，绑定即可成立。真正的风险是「我们调用了某一变体里不存在的成员」，见本节第 4 条。

3. **程序集标识不变**，这是单包可行的机制基础。所有模块的版本号都取自 `STS2-RitsuLib.csproj` 的 `<Version>`，当前为 0.6.13。兼容目标只以 `AssemblyMetadata("Sts2ApiCompat", ...)` 的形式记录，不参与程序集标识（`build/RitsuLib.Module.props`）。

4. **本项目已定稿为一个 DLL 通吃三个版本**（[ADR-002](../4.ADR/002-三版本交付形态.md)）。前提是用到的 RitsuLib 成员在三份变体里都存在。已核对三个入口：

| 入口 | 文件 | 条件编译 |
|---|---|---|
| `RitsuLibFramework` | `src/RitsuLibFramework.cs` | 仅 1 处，在 `Initialize()` 内部（433-435 行），不改变公开成员 |
| `ModDataStore` | `src/Data/ModDataStore.cs` | 无 |
| 设置与界面模块 | `components/settings`、`components/shared` | 无，且只编译一份供各变体共用 |

其余成员未逐一核对。若后续要用到新的 RitsuLib 入口，先按同样方法查一遍条件编译，再写代码。

## 七、未验证

- 未验证：创意工坊条目的分支范围元数据。`workshop.py` 只负责把变体包写进内容目录，分支可见性由上传时的 Steam 元数据决定，本次没有查到其取值。
- 未验证：本模组单 DLL 在 0.107.1 与 0.111.0 上的运行行为。可行性已由 [DOCS/008-E](008-E-三版本API对照.md) 的 API 对照与本文第 6 节第 4 条支持，运行层面未实测。确认方法：在三版本游戏内加载，读 `[RitsuLib.Loader]` 日志与补丁成功日志。
- 未验证：0.113.1 游戏运行 0.113.0 兼容模块的实际行为。代码层面匹配成立，运行层面未实测。

## 八、证据路径

| 内容 | 路径 |
|---|---|
| 兼容目标 | `STS2-RitsuLib-v0.6.13\build\RitsuLib.Compatibility.props:3-6` |
| 条件编译宏 | `STS2-RitsuLib-v0.6.13\build\RitsuLib.CompatDefines.targets:20-37` |
| 目录生成 | `STS2-RitsuLib-v0.6.13\build\RitsuLib.Deployment.targets:26-45` |
| 模块校验与选择 | `STS2-RitsuLib-v0.6.13\components\loader\BundleLayout.cs:18-88` |
| 发布包组成 | `STS2-RitsuLib-v0.6.13\scripts\release_lib\runtime_layout.py`、`bundle.py` |
| 工坊内容来源 | `STS2-RitsuLib-v0.6.13\scripts\release_lib\workshop.py:168-171` |
| 游戏侧依赖判定 | `TMP/0.0.1/调研-反编译/game/ModManager.cs:716-786` |

源码目录：`C:\Users\13167\Desktop\Mod\STS2-Reference\7.Mod-Sources\STS2-RitsuLib-v0.6.13`。
