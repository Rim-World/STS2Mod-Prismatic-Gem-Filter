# 自写脚本登记

本目录只放本项目自行编写的独立脚本。下载的工具另见上级 `TOOLS.md`。

| 文件名 | 放入时间 | 用途 | 依赖 | 输入 | 输出 | 版本 |
|---|---|---|---|---|---|---|
| `build-multiplayer-card-list.py` | 2026-11-15 07:38 | 从反编译源码与本地化文件生成多人模式限定卡牌的中文清单 | Python 3.14 | 反编译单文件 `sts2.decompiled.cs`、`card-index-eng-zhs.json`、`localization/zhs` | Markdown 表格 | 1.0 |

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
