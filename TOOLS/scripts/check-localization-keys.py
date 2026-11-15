# -*- coding: utf-8 -*-
"""检查界面文案的三份 JSON 是否完整、键是否与源码引用一致。

用法：
    python TOOLS/scripts/check-localization-keys.py

检查项：
    1. 三份文件都是合法 JSON，且值是字符串。
    2. 三份文件的键集合完全相同。缺键时玩家会看到英文回退，本项把这种偏差挡在构建前。
    3. 脚本从源码里提取写死的 pgf.* 键，逐一确认存在。
    4. 从 CardGroupTable.cs 提取分组标识、从 CardGroups.cs 提取职业标识，
       展开动态拼出的键，逐一确认存在。

退出码非零表示有问题。本脚本只读，不改任何文件。
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "9.SRC" / "src"
LOC = ROOT / "9.SRC" / "localization"
LANGS = ["eng", "zhs", "zht"]


def load(path):
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        sys.exit(f"{path.name} 不是合法 JSON：{error}")
    bad = [key for key, value in data.items() if not isinstance(value, str)]
    if bad:
        sys.exit(f"{path.name} 有非字符串的值：{bad}")
    return data


def source_keys():
    """源码里写成字符串字面量的 pgf.* 键。"""
    keys = set()
    for path in SRC.glob("*.cs"):
        text = path.read_text(encoding="utf-8")
        # 键必须以字母数字或下划线结尾，这样 "pgf.group." 这类拼接前缀不会被当成键。
        keys.update(re.findall(r'"(pgf\.[A-Za-z0-9_.]*[A-Za-z0-9_])"', text))
    return keys


def group_ids():
    text = (SRC / "CardGroupTable.cs").read_text(encoding="utf-8")
    return sorted(set(re.findall(r'^\s*\("([a-z_]+)",', text, re.MULTILINE)))


def character_ids():
    text = (SRC / "CardGroups.cs").read_text(encoding="utf-8")
    block = re.search(r"Characters\s*=\s*\[(.*?)\];", text, re.S)
    if not block:
        sys.exit("CardGroups.cs 里找不到 Characters 数组。")
    return sorted(set(re.findall(r'"([a-z_]+)"', block.group(1))))


def main():
    problems = []

    tables = {lang: load(LOC / f"{lang}.json") for lang in LANGS}

    base = set(tables["eng"])
    for lang in LANGS[1:]:
        missing = sorted(base - set(tables[lang]))
        extra = sorted(set(tables[lang]) - base)
        if missing:
            problems.append(f"{lang}.json 缺 {len(missing)} 个键：{missing}")
        if extra:
            problems.append(f"{lang}.json 多 {len(extra)} 个键：{extra}")

    referenced = set(source_keys())

    groups = group_ids()
    if not groups:
        problems.append("CardGroupTable.cs 里没有解析出分组标识。")
    for group in groups:
        referenced.add(f"pgf.group.{group}.name")
        referenced.add(f"pgf.group.{group}.desc")

    for character in character_ids():
        referenced.add(f"pgf.char.{character}")

    unknown = sorted(referenced - base)
    if unknown:
        problems.append(f"源码引用了 {len(unknown)} 个不存在的键：{unknown}")

    unused = sorted(base - referenced)
    if unused:
        problems.append(f"有 {len(unused)} 个键没有任何源码引用：{unused}")

    if problems:
        print("检查失败：")
        for item in problems:
            print("  - " + item)
        sys.exit(1)

    print(f"检查通过：3 种语言各 {len(base)} 个键，源码引用 {len(referenced)} 个，分组 {len(groups)} 个。")


if __name__ == "__main__":
    main()
