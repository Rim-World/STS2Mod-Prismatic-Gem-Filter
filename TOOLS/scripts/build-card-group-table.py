# -*- coding: utf-8 -*-
"""从卡牌名单 CSV 生成 9.SRC/src/CardGroupTable.cs。

用法：
    python TOOLS/scripts/build-card-group-table.py            # 生成并写盘
    python TOOLS/scripts/build-card-group-table.py --check     # 只比对，不写盘

输入：
    TMP/0.0.1/卡牌名单/卡牌分组名单.csv   12 个按标记判定的组
    TMP/0.0.1/卡牌名单/多人卡牌组.csv     第 13 组，显式名单

输出：
    9.SRC/src/CardGroupTable.cs

设计说明：
    1. CSV 是名单的唯一来源。表格只由本脚本生成，不手改。
    2. 卡牌标识按游戏规则从类名转换：PascalCase 逐字前插下划线转大写，
       即 BoneShards -> BONE_SHARDS。该转换已由 0.0.3 的运行时自检确认过 12 条。
    3. 生成结果按分组、再按 CSV 原顺序排列，便于人工比对。
"""

import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LIST_DIR = ROOT / "TMP" / "0.0.1" / "卡牌名单"
SOURCES = [LIST_DIR / "卡牌分组名单.csv", LIST_DIR / "多人卡牌组.csv"]
OUTPUT = ROOT / "9.SRC" / "src" / "CardGroupTable.cs"

# CSV 里的中文分组名 -> 落盘用的分组标识。标识一旦发布不得更改。
GROUP_IDS = {
    "奥斯提牌": "osty",
    "召唤牌": "summon",
    "灾厄牌": "doom",
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
}

# 输出顺序。按职业排，便于设置页按职业分节。
GROUP_ORDER = [
    "osty", "summon", "doom",
    "orb", "focus",
    "sly", "shiv", "poison",
    "forge", "star",
    "arrow", "impale",
    "multiplayer",
]


def to_entry(class_name):
    """BoneShards -> BONE_SHARDS。"""
    return re.sub(r"(?<!^)(?=[A-Z])", "_", class_name).upper()


def load_rows():
    rows = []
    for path in SOURCES:
        if not path.exists():
            sys.exit(f"找不到输入文件：{path}")
        with path.open(encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                group_cn = row["分组"].strip()
                if group_cn not in GROUP_IDS:
                    sys.exit(f"未知分组「{group_cn}」，请先在脚本的 GROUP_IDS 里登记。")
                rows.append({
                    "group": GROUP_IDS[group_cn],
                    "entry": to_entry(row["类名"].strip()),
                    "pool": row["卡池"].strip(),
                    "class": row["类名"].strip(),
                })
    return rows


def render(rows):
    by_group = {}
    for row in rows:
        by_group.setdefault(row["group"], []).append(row)

    lines = [
        "// 本文件由 TOOLS/scripts/build-card-group-table.py 生成，不要手改。",
        "// 输入：TMP/0.0.1/卡牌名单/卡牌分组名单.csv、多人卡牌组.csv",
        "",
        "namespace PrismaticGemFilter;",
        "",
        "/// <summary>",
        "///     分组与卡牌标识的对照表。每行是「分组标识、卡牌标识、卡池类名」。",
        "///     <para>",
        "///         卡池类名只用于启动自检与文档核对，运行期的归属判定按玩家卡池的实际成员集合进行。",
        "///     </para>",
        "/// </summary>",
        "internal static class CardGroupTable",
        "{",
        "    /// <summary>全部行。顺序与 <see cref=\"CardGroups.All\" /> 的分组顺序一致。</summary>",
        "    public static readonly (string Group, string Entry, string Pool)[] Rows =",
        "    [",
    ]

    for group_id in GROUP_ORDER:
        group_rows = by_group.get(group_id, [])
        if not group_rows:
            continue
        lines.append(f"        // {group_id}：{len(group_rows)} 张")
        for row in group_rows:
            lines.append(f'        ("{row["group"]}", "{row["entry"]}", "{row["pool"]}"),')
        lines.append("")

    if lines[-1] == "":
        lines.pop()

    lines += [
        "    ];",
        "}",
        "",
    ]
    return "\n".join(lines)


def main():
    rows = load_rows()
    text = render(rows)

    duplicates = {}
    for row in rows:
        duplicates.setdefault(row["group"], []).append(row["entry"])
    problems = []
    for group_id, entries in duplicates.items():
        seen = set()
        for entry in entries:
            if entry in seen:
                problems.append(f"{group_id} 内重复：{entry}")
            seen.add(entry)
    if problems:
        sys.exit("名单有问题：\n  " + "\n  ".join(problems))

    if "--check" in sys.argv:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if current == text:
            print(f"一致：{OUTPUT}（{len(rows)} 行）")
            return
        sys.exit(f"不一致：{OUTPUT} 与 CSV 生成结果不同，需要重新生成。")

    OUTPUT.write_text(text, encoding="utf-8", newline="\n")
    unique = len({row["entry"] for row in rows})
    print(f"已生成 {OUTPUT}")
    print(f"行数 {len(rows)}，去重后 {unique} 张，分组 {len({row['group'] for row in rows})} 个")


if __name__ == "__main__":
    main()
