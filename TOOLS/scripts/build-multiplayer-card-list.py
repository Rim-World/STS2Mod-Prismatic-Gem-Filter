#!/usr/bin/env python3
"""从反编译源码生成「多人模式限定卡牌」中文清单。

输入：
  1. sts2.decompiled.cs   —— ilspycmd 生成的全量反编译单文件
  2. card-index-eng-zhs.json —— 卡牌标题与描述的 zhs 文本
  3. 本地化目录 zhs        —— 职业名、稀有度名、关键词名

输出：Markdown 表格，按职业分组。

用法：
  python build-multiplayer-card-list.py \
      --decompiled <sts2.decompiled.cs> \
      --card-index <card-index-eng-zhs.json> \
      --loc <localization/zhs> \
      --out <输出.md>
"""

import argparse
import json
import os
import re
import sys

CHARACTER_NAMES = {
    "IroncladCardPool": "铁甲战士",
    "SilentCardPool": "静默猎手",
    "DefectCardPool": "故障机器人",
    "RegentCardPool": "储君",
    "NecrobinderCardPool": "亡灵契约师",
    "FoolCardPool": "愚者",
    "ColorlessCardPool": "无色",
    "EventCardPool": "事件",
    "TokenCardPool": "衍生",
}

RARITY_NAMES = {
    "Common": "普通",
    "Uncommon": "罕见",
    "Rare": "稀有",
    "Basic": "初始",
    "Ancient": "远古",
    "Event": "事件",
    "Token": "衍生",
    "None": "无",
}

TYPE_NAMES = {
    "Attack": "攻击",
    "Skill": "技能",
    "Power": "能力",
    "Status": "状态",
    "Curse": "诅咒",
    "Quest": "任务",
}


def load_lines(path):
    with open(path, "r", encoding="utf-8") as fh:
        return fh.read().splitlines()


def find_class_at(lines, index):
    """从某一行往回找最近的类声明，返回 (类名, 起始行号)。"""
    pattern = re.compile(r"^\tpublic (?:sealed |partial |abstract )*class (\w+) : ")
    for i in range(index, max(index - 600, 0), -1):
        m = pattern.match(lines[i])
        if m:
            return m.group(1), i
    return None, None


def slice_class_body(lines, start):
    """从类声明行取到该类的一级闭合花括号。"""
    for i in range(start + 1, len(lines)):
        if lines[i] == "\t}":
            return lines[start:i + 1]
    return lines[start:]


def collect_multiplayer_cards(lines):
    names = []
    needle = "CardMultiplayerConstraint.MultiplayerOnly;"
    for i, line in enumerate(lines):
        if needle in line and "public override CardMultiplayerConstraint" in line:
            name, _ = find_class_at(lines, i)
            if name and name not in names:
                names.append(name)
    return names


def collect_pool_map(lines):
    """类名 -> 卡池类名。卡池的 GenerateAllCards 里用 ModelDb.Card<T>() 列举。"""
    pool_map = {}
    decl = re.compile(r"^\tpublic sealed class (\w+CardPool) : CardPoolModel")
    card = re.compile(r"ModelDb\.Card<([\w.]+)>\(\)")
    for i, line in enumerate(lines):
        m = decl.match(line)
        if not m:
            continue
        pool = m.group(1)
        for body_line in slice_class_body(lines, i):
            for ref in card.findall(body_line):
                pool_map.setdefault(ref.split(".")[-1], pool)
    return pool_map


VAR_PATTERN = re.compile(r"new\s+(\w+(?:<(\w+)>)?Var|<(\w+)>)\s*\(([^()]*)\)")
VAR_PATTERN2 = re.compile(r"new\s+(\w+Var)(?:<(\w+)>)?\(([^()]*)\)")


def parse_vars(body_text):
    """解析 CanonicalVars 里的动态数值，返回 占位名 -> 数值。"""
    block = extract_property_body(body_text, "CanonicalVars")
    if block is None:
        return {}

    values = {}
    for m in VAR_PATTERN2.finditer(block):
        kind, generic, args = m.group(1), m.group(2), m.group(3)
        numbers = re.findall(r"-?\d+(?:\.\d+)?m?", args)
        strings = re.findall(r'"([^"]+)"', args)
        if not numbers:
            continue
        raw = numbers[-1].rstrip("m")
        try:
            value = float(raw)
        except ValueError:
            continue

        if generic:
            name = generic
        elif kind == "DynamicVar" and strings:
            name = strings[0]
        elif kind == "StringVar":
            continue
        else:
            name = kind[:-3] if kind.endswith("Var") else kind
        values[name] = value

    # 计算型数值在未升级状态下等于基数，用基数兜底
    if "CalculationBase" in values:
        for name in ("CalculatedDamage", "CalculatedBlock"):
            values.setdefault(name, values["CalculationBase"])
    return values


def extract_property_body(text, prop):
    """取属性或字段的初始化表达式，返回花括号/括号内的文本。"""
    idx = text.find(prop)
    if idx < 0:
        return None
    tail = text[idx:]
    # 形如 => new ...(...);  或 { get { return ...; } }
    m = re.match(r"[^\n]*=>\s*(.*?);\s*\n", tail, re.S)
    if m:
        return m.group(1)
    m = re.search(r"\{\s*get\s*\{(.*?)\}\s*\}", tail, re.S)
    if m:
        return m.group(1)
    return None


KEYWORD_PATTERN = re.compile(r"CardKeyword\.(\w+)")


def parse_keywords(body_text):
    block = extract_property_body(body_text, "CanonicalKeywords")
    if not block:
        return []
    seen = []
    for kw in KEYWORD_PATTERN.findall(block):
        if kw != "None" and kw not in seen:
            seen.append(kw)
    return seen


CTOR_PATTERN = re.compile(
    r":\s*base\(\s*(-?\d+)\s*,\s*CardType\.(\w+)\s*,\s*CardRarity\.(\w+)\s*,"
)


def parse_ctor(body_text):
    m = CTOR_PATTERN.search(body_text)
    if not m:
        return None
    return {"cost": int(m.group(1)), "type": m.group(2), "rarity": m.group(3)}


STAR_COST_PATTERN = re.compile(r"CanonicalStarCost\s*=>\s*(\d+)")


def parse_star_cost(body_text):
    m = STAR_COST_PATTERN.search(body_text)
    return int(m.group(1)) if m else 0


# 少数卡牌的效果文本依赖运行时计算，无法从静态数值推出。
# 这里逐条改写，注释说明依据。
DESC_OVERRIDES = {
    # 卡牌文本第二行是战斗内的实时提示：「给予{CalculatedBlock}点格挡」，
    # 数值等于玩家当前格挡，第一行已说明，故整段删除。
    "DemonicShield": [(r"\{InCombat:.*?\|\}", "", re.S)],
}


def fmt_number(value):
    if value is None:
        return "?"
    if abs(value - round(value)) < 1e-9:
        return str(int(round(value)))
    return ("%g" % value)


def render_description(template, values):
    if not template:
        return ""

    def branch(text, value):
        """渲染分支内的 {:diff()} 占位，用于 choose 与 show 两种格式器。"""
        return re.sub(r"\{:diff\(?\)?\}", fmt_number(value), text)

    def repl(match):
        name = match.group(1)
        spec = (match.group(2) if match.lastindex and match.lastindex >= 2 else None) or ""
        value = values.get(name)

        # {Cards:choose(1):1张|{:diff()}张}  —— 等于该数值时取前半段，否则取后半段
        choose = re.match(r"choose\((-?\d+)\):([^{}]*)\|(.*)$", spec, re.S)
        if choose:
            if value is not None and value == int(choose.group(1)):
                return branch(choose.group(2), value)
            return branch(choose.group(3), value)

        # 未升级时不显示、升级后才显示，或升级前后更换措辞
        show = re.match(r"show:(.*?)\|(.*)$", spec, re.S)
        if show:
            return branch(show.group(2), value)

        if value is None:
            return ""
        if "percentMore" in spec:
            return str(int(round((value - 1) * 100)))
        if "percentLess" in spec:
            return str(int(round((1 - value) * 100)))
        if "energyIcons" in spec:
            return fmt_number(value) + "费"
        if "starIcons" in spec:
            return fmt_number(value) + "星"
        return fmt_number(value)

    # {Cards:choose(1):1张|{:diff()}张} —— 前半段带自己的 {:diff}，须先于通用格式器处理
    def repl_choose(match):
        name, pivot, first, second = match.group(1), int(match.group(2)), match.group(3), match.group(4)
        value = values.get(name)
        if value is not None and value == pivot:
            return branch(first, value)
        return branch(second, value)

    text = template
    text = re.sub(
        r"\{(\w+):choose\((-?\d+)\):([^{}]*)\|([^{}]*\{:diff[^{}]*\}[^{}]*)\}",
        repl_choose,
        text,
    )
    text = re.sub(r"\{(\w+):([^{}]*)\}", repl, text)
    text = re.sub(r"\{(\w+)\}", repl, text)
    text = re.sub(r"\[/?\w+\]", "", text)
    text = text.replace("\\n", "<br>")
    text = text.replace("\n", "<br>")
    text = re.sub(r"（\s*）|\(\s*\)", "", text)
    text = text.replace("<br><br>", "<br>")
    text = re.sub(r"(?<=[0-9]) (?=点|张|费|层|星)", "", text)
    return text.strip()


def strip_tags(text):
    text = re.sub(r"\[/?\w+\]", "", text or "")
    return text.strip()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--decompiled", required=True)
    parser.add_argument("--card-index", required=True)
    parser.add_argument("--loc", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    lines = load_lines(args.decompiled)
    with open(args.card_index, "r", encoding="utf-8") as fh:
        index = json.load(fh)

    keyword_loc = {}
    kw_path = os.path.join(args.loc, "card_keywords.json")
    if os.path.exists(kw_path):
        with open(kw_path, "r", encoding="utf-8") as fh:
            raw = json.load(fh)
        for key, value in raw.items():
            if key.endswith(".title"):
                keyword_loc[key[: -len(".title")].upper()] = value

    names = collect_multiplayer_cards(lines)
    pool_map = collect_pool_map(lines)

    rows = []
    missing = []
    for cls in names:
        start = None
        pattern = re.compile(r"^\tpublic (?:sealed )?class " + cls + r" : ")
        for i, line in enumerate(lines):
            if pattern.match(line):
                start = i
                break
        if start is None:
            missing.append((cls, "未找到类声明"))
            continue
        body = "\n".join(slice_class_body(lines, start))
        ctor = parse_ctor(body)
        if ctor is None:
            missing.append((cls, "未找到构造函数"))
            continue

        var_values = parse_vars(body)
        keywords = parse_keywords(body)
        star_cost = parse_star_cost(body)

        # 中文名与描述：card-index 的键是英文标题的 SNAKE_CASE 形式
        entry = None
        joints = cls.upper()
        candidates = [joints]
        # BigBang -> BIGBANG / BIG_BANG 之类，逐个尝试
        for id_key in index:
            if id_key.replace("_", "").lower() == cls.lower():
                candidates.insert(0, id_key)
                break
        for key in candidates:
            if key in index:
                entry = index[key]
                break
        if entry is None:
            missing.append((cls, "未找到中文文本"))
            continue

        title = strip_tags(entry.get("title", {}).get("zhs", ""))
        template = entry.get("description", {}).get("zhs", "")
        for pattern, replacement, flags in DESC_OVERRIDES.get(cls, []):
            template = re.sub(pattern, replacement, template, flags=flags)
        desc = render_description(template, var_values)

        pool = pool_map.get(cls, "")
        rows.append({
            "class": cls,
            "title": title,
            "pool": pool,
            "character": CHARACTER_NAMES.get(pool, pool),
            "rarity": RARITY_NAMES.get(ctor["rarity"], ctor["rarity"]),
            "rarity_order": ["Basic", "Common", "Uncommon", "Rare", "Ancient"].index(ctor["rarity"])
            if ctor["rarity"] in ["Basic", "Common", "Uncommon", "Rare", "Ancient"] else 99,
            "type": TYPE_NAMES.get(ctor["type"], ctor["type"]),
            "cost": ctor["cost"],
            "star_cost": star_cost,
            "keywords": "、".join(keyword_loc.get(kw.upper(), kw) for kw in keywords),
            "desc": desc,
        })

    char_order = ["铁甲战士", "静默猎手", "故障机器人", "储君", "亡灵契约师", "愚者", "无色", "事件", "衍生"]
    rows.sort(key=lambda r: (char_order.index(r["character"]) if r["character"] in char_order else 99,
                             r["rarity_order"], r["title"]))

    out = []
    out.append("# 多人模式限定卡牌清单")
    out.append("")
    out.append("适用于游戏 0.113.1。只收游戏内标记为「多人模式限定」的卡牌，数值取未升级状态。")
    current = None
    for row in rows:
        if row["character"] != current:
            current = row["character"]
            count = sum(1 for r in rows if r["character"] == current)
            out.append("")
            out.append(f"## {current}（{count} 张）")
            out.append("")
            out.append("| 卡牌名字 | 稀有度 | 费用 | 卡面标签 | 效果 |")
            out.append("|---|---|---|---|---|")
        cost = "X" if row["cost"] < 0 else str(row["cost"])
        if row["star_cost"]:
            cost = f"{cost}（另需 {row['star_cost']} 星）"
        if row["cost"] == -1:
            cost = "X"
        out.append(
            "| {title} | {rarity} | {cost} | {kw} | {desc} |".format(
                title=row["title"], rarity=row["rarity"], cost=cost,
                kw=row["keywords"] or "—", desc=row["desc"] or "—",
            )
        )

    out.append("")
    out.append(f"合计 {len(rows)} 张。卡面标签取自卡牌关键词，含消耗、虚无、固有、保留、奇巧、不能被打出、永恒。")
    out.append("")

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")

    print(f"写入 {args.out}")
    print(f"卡牌 {len(rows)} 张")
    if missing:
        print("未能解析：")
        for cls, why in missing:
            print(f"  {cls}: {why}")
    leftovers = [r["title"] for r in rows if "{" in r["desc"]]
    if leftovers:
        print("描述中残留占位符：" + "、".join(leftovers))
    return 0


if __name__ == "__main__":
    sys.exit(main())
