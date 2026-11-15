# 自写脚本登记

本目录只放本项目自行编写的独立脚本。下载的工具另见上级 `TOOLS.md`。

| 文件名 | 放入时间 | 用途 | 依赖 | 输入 | 输出 | 版本 |
|---|---|---|---|---|---|---|
| `build-multiplayer-card-list.py` | 2026-11-15 07:38 | 从反编译源码与本地化文件生成多人模式限定卡牌的中文清单 | Python 3.14 | 反编译单文件 `sts2.decompiled.cs`、`card-index-eng-zhs.json`、`localization/zhs` | Markdown 表格 | 1.0 |
| `build-card-group-table.py` | 2026-11-15 23:10 | 把 13 个分组的卡牌名单 CSV 转成源码里的数据表，并可核对源码与 CSV 是否同步 | Python 3.14，无第三方库 | `TMP/0.0.1/卡牌名单/卡牌分组名单.csv`、`多人卡牌组.csv` | `9.SRC/src/CardGroupTable.cs` | 1.0 |
| `check-localization-keys.py` | 2026-11-15 23:20 | 检查三份界面文案 JSON 的键集合是否一致，以及源码引用的键是否齐全、有无多余 | Python 3.14，无第三方库 | `9.SRC/localization/*.json`、`9.SRC/src/*.cs` | 控制台结论，退出码非零表示失败 | 1.0 |

## build-card-group-table.py

### 用法

```powershell
python TOOLS\scripts\build-card-group-table.py          # 生成并写盘
python TOOLS\scripts\build-card-group-table.py --check  # 只比对，不写盘
```

### 做法

1. 读两份名单 CSV，按脚本内登记的「中文分组名 → 分组标识」映射换成标识。
2. 类名转卡牌标识：在每个大写字母前插下划线再整体转大写，即 `BoneShards` → `BONE_SHARDS`。
3. 按固定的分组顺序输出 `("分组", "卡牌标识", "卡池类名")` 三元组。
4. 同一分组内出现重复标识时报错退出。
5. 核对每个标识在游戏 `cards` 表里都有 `<标识>.title`。该表是模组运行时取卡名的地方，因此这一步同时验证「类名转标识」没转错。参考文件不在本机时只提示，不算失败。

### 已知限制

- 类名转标识只处理无缩写的 PascalCase。若卡牌类名含连续大写（例如 `XYSomething`），生成的标识会错，需要人工确认。
- 分组标识一旦发布不得改动，否则玩家存档里的键失效。

## check-localization-keys.py

### 用法

```powershell
python TOOLS\scripts\check-localization-keys.py
```

### 做法

1. 读三份 JSON，要求都是合法 JSON 且值都是字符串。
2. 要求三份文件的键集合完全相同。
3. 从源码里提取写成字符串字面量的 `pgf.*` 键。
4. 从 `CardGroupTable.cs` 提取分组标识、从 `CardGroups.cs` 提取职业标识，展开运行时拼接的键。
5. 比对源码引用与 JSON 键，报告缺失与多余。

### 已知限制

- 正则只识别双引号里、以 `pgf.` 开头且以字母数字下划线结尾的键。新增拼接方式时需同步调整正则。

## build-multiplayer-card-list.py

### 用法

```powershell
python TOOLS\scripts\build-multiplayer-card-list.py `
  --decompiled "TMP\0.0.1\调研-反编译\game_full\sts2.decompiled.cs" `
  --card-index "C:\Users\13167\Desktop\Mod\STS2-Reference\4.STS2-113.1Reference\card-index-eng-zhs.json" `
  --loc "C:\Users\13167\Desktop\Mod\STS2-Reference\4.STS2-113.1Reference\localization\zhs" `
  --out "TMP\0.0.1\多人限定卡牌\多人限定卡牌.md"
```

### 做法

1. 扫描反编译源码，找出覆写了 `MultiplayerConstraint` 且值为 `MultiplayerOnly` 的类。
2. 反向定位每个类的声明与类体，从构造函数取费用、类型、稀有度。
3. 从 `CanonicalKeywords` 取关键词，从 `CanonicalVars` 取未升级数值。
4. 从卡池类的 `GenerateAllCards` 反查每张卡属于哪个卡池，据此判定职业。
5. 取 `cards.json` 的中文描述，把 `{变量:格式器()}` 占位替换成未升级数值。
6. 按职业、稀有度排序，输出 Markdown 表格。

### 已知限制

- 依赖反编译产物的类声明格式。若 `ilspycmd` 版本变化导致输出格式改变，正则需同步调整。
- 少量卡牌的效果文本含战斗内实时数值，无法从静态数据推出，写在 `DESC_OVERRIDES` 里逐条处理，每条附注释。
- 只处理中文（`zhs`）。换语言需改 `--loc` 与 `card-index` 的来源。
