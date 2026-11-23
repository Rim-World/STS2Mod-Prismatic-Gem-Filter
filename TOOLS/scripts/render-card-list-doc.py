# -*- coding: utf-8 -*-
"""从名单 CSV 与 ExclusionTable.cs 生成 3.DOCS/024-T-卡牌过滤名单.md。

用法：
    python TOOLS/scripts/render-card-list-doc.py            # 生成并写盘
    python TOOLS/scripts/render-card-list-doc.py --check    # 只比对，不写盘

输入：
    3.DOCS/024-卡牌名单/卡牌分组名单.csv
    3.DOCS/024-卡牌名单/多人卡牌组.csv
    3.DOCS/024-卡牌名单/Token卡牌附表.csv
    9.SRC/src/ExclusionTable.cs    （只读，用来取默认豁免集合）

输出：
    3.DOCS/024-T-卡牌过滤名单.md
"""

import csv
import re
import sys
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LIST_DIR = ROOT / "3.DOCS" / "024-卡牌名单"
OUTPUT = ROOT / "3.DOCS" / "024-T-卡牌过滤名单.md"
EXCLUSION = ROOT / "9.SRC" / "src" / "ExclusionTable.cs"

DOC_DATE = "2026-11-24"
VERSION = "0.0.15"

# 分组标识、中文界面名、判据、默认值。成员与张数从 CSV 读。
GROUPS = OrderedDict([
    ("osty", ("奥斯提攻击牌", "`CardTag.OstyAttack`", "亡灵契约师")),
    ("summon", ("召唤牌", "`SummonVar`", "亡灵契约师")),
    ("doom", ("灾厄牌", "`DoomPower`", "亡灵契约师")),
    ("soul", ("灵魂牌", "源码引用 `Soul`", "亡灵契约师")),
    ("orb", ("充能球牌", "引用 `OrbCmd`", "故障机器人")),
    ("focus", ("集中牌", "`FocusPower`", "故障机器人")),
    ("sly", ("奇巧牌", "`CardKeyword.Sly`", "静默猎手")),
    ("shiv", ("小刀牌", "引用 `Shiv`", "静默猎手")),
    ("poison", ("中毒牌", "`PoisonPower`", "静默猎手")),
    ("forge", ("铸造牌", "`ForgeVar` 或 `ForgeCmd`", "储君")),
    ("star", ("辉星牌", "`StarsVar` 或 `CanonicalStarCost`", "储君")),
    ("arrow", ("箭矢牌", "`CardTag.Shoot`", "愚者")),
    ("impale", ("扎穿牌", "`ImpaledPower`", "愚者")),
    ("multiplayer", ("多人专属牌", "类名枚举", "跨 5 职业")),
    ("bugged", ("现版本有 bug 的牌", "类名枚举", "其他")),
    ("sadness", ("悲伤相关牌", "源码引用 `Sadness`", "愚者")),
])

CSV_GROUP_TO_ID = {
    "奥斯提牌": "osty",
    "召唤牌": "summon",
    "灾厄牌": "doom",
    "灵魂牌": "soul",
    "球牌（含激发、球位）": "orb",
    "集中牌": "focus",
    "箭矢牌": "arrow",
    "扎穿牌": "impale",
    "奇巧牌": "sly",
    "刀牌": "shiv",
    "毒牌": "poison",
    "铸剑牌": "forge",
    "星牌": "star",
    "多人卡牌": "multiplayer",
    "有bug的牌": "bugged",
    "悲伤牌": "sadness",
}

DESCRIPTIONS = {
    "osty": "没有奥斯提则无效",
    "summon": "召唤或强化奥斯提",
    "doom": "施加或消耗灾厄",
    "soul": "生成或利用灵魂",
    "orb": "生成、激发充能球，或增加栏位",
    "focus": "获得或施加集中",
    "sly": "带奇巧关键字",
    "shiv": "生成或强化小刀",
    "poison": "施加或消耗中毒",
    "forge": "铸造，强化君王之剑",
    "star": "消耗或产生辉星",
    "arrow": "发射箭矢",
    "impale": "施加或消耗扎穿",
    "multiplayer": "只在多人模式中出现",
    "bugged": "别的职业获取时效果异常",
    "sadness": "生成、消耗或转化悲伤",
}


def to_entry(class_name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", class_name).upper()


def load_csv(path):
    if not path.exists():
        sys.exit(f"找不到输入文件：{path}")
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def load_rows():
    rows = []
    for path in (LIST_DIR / "卡牌分组名单.csv", LIST_DIR / "多人卡牌组.csv"):
        for raw in load_csv(path):
            group_cn = raw["分组"].strip()
            if group_cn not in CSV_GROUP_TO_ID:
                sys.exit(f"未知分组「{group_cn}」：{path.name}")
            rows.append({
                "group": CSV_GROUP_TO_ID[group_cn],
                "group_cn": group_cn,
                "job": raw["职业"].strip(),
                "default": raw["默认"].strip(),
                "zh": raw["中文名"].strip(),
                "en": raw["英文名"].strip(),
                "class": raw["类名"].strip(),
                "entry": to_entry(raw["类名"].strip()),
                "type": raw["类型"].strip(),
                "pool": raw["卡池"].strip(),
            })
    return rows


def load_exemptions():
    text = EXCLUSION.read_text(encoding="utf-8")
    match = re.search(r"ExplicitExemptionKeysValue\s*=\s*\[(.*?)\];", text, re.S)
    if not match:
        sys.exit("ExclusionTable.cs 里找不到 ExplicitExemptionKeysValue")
    keys = re.findall(r'"([A-Z0-9_]+)"', match.group(1))
    if not keys:
        sys.exit("ExclusionTable.cs 里没有解析到默认豁免键")
    return set(keys)


def validate(rows, exemptions):
    problems = []
    seen = {}
    for row in rows:
        key = (row["group"], row["entry"])
        if key in seen:
            problems.append(f"{row['group']} 内重复：{row['entry']}")
        seen[key] = True
        if row["group"] not in GROUPS:
            problems.append(f"未登记分组：{row['group']}")
    known_entries = {row["entry"] for row in rows}
    for entry in sorted(exemptions):
        if entry not in known_entries:
            problems.append(f"默认豁免不在任何分组：{entry}")
    if problems:
        sys.exit("名单校验失败：\n  " + "\n  ".join(problems))


def counts(rows, exemptions, has_fool):
    visible_groups = []
    visible_rows = 0
    for group_id in GROUPS:
        members = [row for row in rows if row["group"] == group_id]
        group_visible = [row for row in members if has_fool or row["pool"] != "FoolCardPool"]
        if group_visible:
            visible_groups.append(group_id)
            visible_rows += len(group_visible)
    enabled = [g for g in visible_groups if GLOBAL_DEFAULT[g]]
    pool = set()
    for group_id in enabled:
        for row in rows:
            if row["group"] != group_id:
                continue
            if not has_fool and row["pool"] == "FoolCardPool":
                continue
            if row["entry"] not in exemptions:
                pool.add(row["entry"])
    return {
        "rows": visible_rows,
        "groups": len(visible_groups),
        "enabled": len(enabled),
        "pool": len(pool),
        "visible_groups": visible_groups,
        "visible_exemptions": len({e for e in exemptions if any(
            row["entry"] == e and (has_fool or row["pool"] != "FoolCardPool") for row in rows)}),
    }


def table(rows, group_id, exemptions):
    headers = ["中文名", "英文名", "类名", "类型", "默认豁免"]
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join(["---"] * len(headers)) + "|"]
    for row in rows:
        if row["group"] != group_id:
            continue
        exempt = "是" if row["entry"] in exemptions else "否"
        lines.append(f"| {row['zh']} | {row['en']} | {row['class']} | {row['type']} | {exempt} |")
    return lines


GLOBAL_DEFAULT = {group_id: False for group_id in GROUPS}
for group_id, (_, _, _) in GROUPS.items():
    GLOBAL_DEFAULT[group_id] = group_id not in {"arrow", "shiv", "poison", "multiplayer"}


def build():
    rows = load_rows()
    exemptions = load_exemptions()
    validate(rows, exemptions)
    by_group = {group_id: [row for row in rows if row["group"] == group_id] for group_id in GROUPS}

    # 默认值以 CSV 首选行为准；同一组必须一致。
    for group_id, members in by_group.items():
        defaults = {row["default"] for row in members}
        if len(defaults) > 1:
            sys.exit(f"分组 {group_id} 的默认值不一致：{defaults}")
        GLOBAL_DEFAULT[group_id] = (next(iter(defaults)) == "勾选")

    c113 = counts(rows, exemptions, True)
    c111 = counts(rows, exemptions, False)
    duplicates = OrderedDict()
    for row in rows:
        duplicates.setdefault(row["entry"], []).append(row)
    duplicate_rows = [items for items in duplicates.values() if len(items) > 1]

    lines = [
        "# 棱彩宝石过滤：卡牌过滤名单",
        "",
        f"- 当前版本：{VERSION}（{DOC_DATE}）",
        "- 游戏版本：0.113.1（提交 `d09d48e5`，2026-11-14）",
        "- 数据来源：`C:\\Users\\13167\\Desktop\\Mod\\STS2-Reference\\4.STS2-113.1Reference`",
        "- 名单来源：`3.DOCS/024-卡牌名单/` 下的三份 CSV；本文件由 `TOOLS/scripts/render-card-list-doc.py` 生成，不要手改表格。",
        "- 默认豁免来源：`9.SRC/src/ExclusionTable.cs` 的 `ExplicitExemptionKeysValue`。",
        f"- 条目：{len(rows)} 行，去重 {len({row['entry'] for row in rows})} 张，跨组重复 {len(duplicate_rows)} 条；token 卡牌 {len(load_csv(LIST_DIR / 'Token卡牌附表.csv'))} 条单列于文末。",
        f"- 默认豁免：{len(exemptions)} 张（按卡牌去重）。",
        "",
        "> 0.111 没有愚者角色。设置页会隐藏只含愚者卡池成员的组；本表仍按 0.113.1 全量数据列出。",
        "",
        "## 汇总",
        "",
        "| 序 | 职业 | 开关 | 判据 | 可发牌数量 | 首次启动默认 | 默认豁免 |",
        "|---|---|---|---|---|---|---|",
    ]
    for index, (group_id, (title, criterion, job)) in enumerate(GROUPS.items(), 1):
        members = by_group[group_id]
        exempt_count = sum(1 for row in members if row["entry"] in exemptions)
        default = "勾选" if GLOBAL_DEFAULT[group_id] else "不勾"
        lines.append(f"| {index} | {job} | {title} | {criterion} | {len(members)} | {default} | {exempt_count} 张 |")

    lines += [
        "",
        f"> 0.113.1：可见分组 {c113['groups']} 个，默认勾选 {c113['enabled']} 个，可见成员 {c113['rows']} 行，默认过滤池 {c113['pool']} 张。",
        f"> 0.111.1：可见分组 {c111['groups']} 个，默认勾选 {c111['enabled']} 个，可见成员 {c111['rows']} 行，默认过滤池 {c111['pool']} 张；可见默认豁免 {c111['visible_exemptions']} 张。",
        "",
        "## 灵魂牌判定",
        "",
        "- 扫描 0.113.1 的 `MegaCrit.Sts2.Core.Models.Cards/*.cs`，取源码引用 `Soul` 的非 token 卡牌，共 11 张。",
        "- 其中 `GLIMPSE_BEYOND`（彼岸一瞥）与 `SOULBOUND`（灵魂绑定）是多人限定卡，按 SP-014 只放入「多人专属牌」，不放入「灵魂牌」。",
        "- 其余 9 张放入「灵魂牌」；`DIRGE`（挽歌）同时属于「召唤牌」与「灵魂牌」，是第三个允许跨组的卡牌。",
        "- `GRAVE_WARDEN`（守墓人）、`CAPTURE_SPIRIT`（捕捉灵魂）、`SEANCE`（降灵）默认豁免；其余 6 张默认过滤。",
        "- `SOUL`（灵魂）本身是 `Token` 稀有度，不进入发牌候选，只列在文末附表。",
        "",
    ]

    for group_id, (title, criterion, job) in GROUPS.items():
        members = by_group[group_id]
        if not members:
            continue
        exempt_count = sum(1 for row in members if row["entry"] in exemptions)
        lines += [
            f"## {title}",
            "",
            f"- 职业：{job}　判据：{criterion}　默认：{'勾选' if GLOBAL_DEFAULT[group_id] else '不勾'}　数量：{len(members)}　默认豁免：{exempt_count} 张",
            f"- 说明：{DESCRIPTIONS[group_id]}",
            "",
        ]
        lines += table(rows, group_id, exemptions)
        lines.append("")

    lines += [
        "## 跨组重复",
        "",
        "| 中文名 | 英文名 | 所属开关 | 默认豁免 |",
        "|---|---|---|---|",
    ]
    for items in duplicate_rows:
        titles = "、".join(GROUPS[item["group"]][0] for item in items)
        exempt = "是" if items[0]["entry"] in exemptions else "否"
        lines.append(f"| {items[0]['zh']} | {items[0]['en']} | {titles} | {exempt} |")

    lines += [
        "",
        "## 附表：Token 卡牌",
        "",
        "这些卡牌属于 `TokenCardPool`，由卡牌或能力在战斗中直接生成，不经过发牌链路。",
        "",
        "| 中文名 | 英文名 | 类名 | 类型 | 本来会落到的开关 | 默认豁免 |",
        "|---|---|---|---|---|---|",
    ]
    for row in load_csv(LIST_DIR / "Token卡牌附表.csv"):
        group_id = CSV_GROUP_TO_ID[row["分组"].strip()]
        lines.append(f"| {row['中文名']} | {row['英文名']} | {row['类名']} | {row['类型']} | {GROUPS[group_id][0]} | 不适用 |")

    lines += [
        "",
        "## 0.0.15 名单变化",
        "",
        f"- {DOC_DATE} 按用户要求新增「灵魂牌」组，默认勾选。",
        "- 9 张非多人灵魂牌进入新组；其中守墓人、捕捉灵魂、降灵默认豁免。",
        "- 彼岸一瞥、灵魂绑定加入「多人专属牌」组，该组由 10 张增至 12 张。",
        "- 挽歌同时出现在「召唤牌」与「灵魂牌」，跨组重复由 2 条增至 3 条。",
        "- 名单总行数由 180 增至 191，去重由 178 增至 188。",
        "- 默认豁免由 10 张增至 13 张。设置架构版本由 6 升到 7，旧档首次启动重置。",
        "",
        "## 名单变化历史",
        "",
        "- 0.0.5：剔除初始牌与先古牌；多人限定卡移入「多人卡牌」组；铸剑与辉星改为默认勾选。",
        "- 0.0.7：遮天蔽日移出箭矢组，单独立「有 bug 的牌」组。",
        "- 0.0.8：新增「悲伤相关牌」组，活跃气氛默认豁免。",
        "- 0.0.11：奥斯提组改名为「奥斯提攻击牌」并加入钙化；灾厄组加入血肉戏法；洁净与唤起默认豁免。",
        "- 0.0.12：暴涨移回充能球组；豁免改为按卡牌记录。",
        "- 0.0.15：新增灵魂牌组；彼岸一瞥与灵魂绑定加入多人组；守墓人、捕捉灵魂、降灵默认豁免。",
    ]
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    text = build()
    if "--check" in sys.argv:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if current == text:
            print(f"一致：{OUTPUT}")
        else:
            sys.exit(f"不一致：{OUTPUT} 需要重新生成。")
    else:
        OUTPUT.write_text(text, encoding="utf-8", newline="\n")
        print(f"已生成 {OUTPUT}")
