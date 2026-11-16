# -*- coding: utf-8 -*-
"""提交门禁：每次提交必须带来至少一条新的用户 prompt。

为什么不看 git 的暂存区：`1.PRD/用户prompt/` 在 `.gitignore` 里，git 看不到这些文件。
门禁只能读文件系统，并把「已被提交消费过的文件名」记在 `.git` 目录下，不进入版本库。

用法（由钩子调用，也可以手动跑）：

    python 8.TEST/CI-CD/check-user-prompt.py --mode pre       # 提交前检查，不通过则非零退出
    python 8.TEST/CI-CD/check-user-prompt.py --mode post      # 提交成功后记账
    python 8.TEST/CI-CD/check-user-prompt.py --mode show      # 只看状态，不改任何文件
    python 8.TEST/CI-CD/check-user-prompt.py --mode baseline  # 把现有文件全部记为已消费

状态文件：`<git-dir>/user-prompt-gate.json`。
豁免：在 `<git-dir>/user-prompt-gate.skip` 写一行理由，门禁放行一次并记入
`<git-dir>/user-prompt-gate.log`，随后删除该文件。这是唯一的绕过方式，不要用 --no-verify。

退出码：0 放行，1 拒绝。
"""

import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path

PROMPT_DIR_REL = "1.PRD/用户prompt"
STATE_NAME = "user-prompt-gate.json"
SKIP_NAME = "user-prompt-gate.skip"
LOG_NAME = "user-prompt-gate.log"


def repo_root() -> Path:
    """优先问 git，问不到就按脚本位置推。"""
    try:
        out = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            capture_output=True, text=True, encoding="utf-8", check=True)
        candidate = Path(out.stdout.strip())
        if (candidate / ".git").exists():
            return candidate
    except (subprocess.CalledProcessError, OSError):
        pass
    return Path(__file__).resolve().parents[2]


def git_dir(root: Path) -> Path:
    try:
        out = subprocess.run(
            ["git", "rev-parse", "--git-dir"],
            cwd=root, capture_output=True, text=True, encoding="utf-8", check=True)
        path = Path(out.stdout.strip())
        return path if path.is_absolute() else root / path
    except (subprocess.CalledProcessError, OSError):
        return root / ".git"


def prompt_files(root: Path) -> list:
    folder = root / PROMPT_DIR_REL
    if not folder.is_dir():
        return []
    return sorted(p.name for p in folder.glob("*.md"))


def load_state(path: Path):
    if not path.exists():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        consumed = data.get("consumed", [])
        return [str(item) for item in consumed]
    except (json.JSONDecodeError, OSError, AttributeError):
        return None


def save_state(path: Path, consumed, present) -> None:
    """保存时顺手清掉已经不存在于目录里的名字。"""
    alive = sorted({name for name in consumed if name in present})
    payload = {"consumed": alive, "updated": datetime.now().strftime("%Y-%m-%d %H:%M")}
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def has_staged_change(root: Path) -> bool:
    """暂存区相对 HEAD 是否有改动。用来放行「只改提交信息」的 amend。"""
    try:
        result = subprocess.run(
            ["git", "diff", "--cached", "--quiet", "HEAD"],
            cwd=root, capture_output=True)
        return result.returncode != 0
    except OSError:
        return True


def head_commit_time(root: Path):
    """HEAD 的提交时间戳（秒）。没有提交时返回 None。"""
    try:
        out = subprocess.run(
            ["git", "log", "-1", "--format=%ct"],
            cwd=root, capture_output=True, text=True, encoding="utf-8")
        text = out.stdout.strip()
        return int(text) if text.isdigit() else None
    except (OSError, ValueError):
        return None


def baseline_split(root: Path, present):
    """首次启用时的基线：比 HEAD 早的算已消费，比 HEAD 晚的留作待消费。

    返回 (已消费集合, 待消费列表)。没有提交时把所有文件都算作已消费。
    """
    head_time = head_commit_time(root)
    folder = root / PROMPT_DIR_REL

    if head_time is None:
        return set(present), []

    consumed, pending = set(), []
    for name in present:
        try:
            mtime = (folder / name).stat().st_mtime
        except OSError:
            consumed.add(name)
            continue
        if mtime <= head_time:
            consumed.add(name)
        else:
            pending.append(name)
    return consumed, pending


def write_log(path: Path, message: str) -> None:
    try:
        with path.open("a", encoding="utf-8") as handle:
            handle.write(f"{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}  {message}\n")
    except OSError:
        pass


def number_hint(present) -> str:
    """软提示：最新一条不是四位递增编号时提醒一句，不拦提交。"""
    if not present:
        return ""
    stem = present[-1][: -len(".md")]
    if len(stem) == 4 and stem.isdigit():
        return ""
    return f"提示：最新一条的文件名「{present[-1]}」不是四位递增编号，按规范应为 {len(present):04d}.md。"


def main() -> int:
    mode = "pre"
    if "--mode" in sys.argv:
        mode = sys.argv[sys.argv.index("--mode") + 1]

    root = repo_root()
    store = git_dir(root)
    state_path = store / STATE_NAME
    skip_path = store / SKIP_NAME

    present = prompt_files(root)
    if not present:
        print(f"[门禁] 找不到 {PROMPT_DIR_REL} 下的任何 .md，跳过 prompt 门禁。", file=sys.stderr)
        return 0

    state = load_state(state_path)

    if mode == "baseline":
        save_state(state_path, present, present)
        print(f"[门禁] 基线已重建：{len(present)} 条记为已消费。")
        return 0

    if mode == "show":
        consumed = set(state or [])
        pending = [name for name in present if name not in consumed]
        print(f"目录：{PROMPT_DIR_REL}")
        print(f"总条数：{len(present)}，最新：{present[-1]}")
        print(f"状态文件：{state_path}")
        print("未消费：" + ("、".join(pending) if pending else "无（下次提交会被拦下）"))
        return 0

    if state is None:
        # 首次启用：按 HEAD 的提交时间切分基线，比 HEAD 晚的算作本次待消费。
        consumed, fresh = baseline_split(root, present)
        save_state(state_path, consumed, present)
        note = "、".join(fresh) if fresh else "无"
        print(f"[门禁] 首次启用，基线 {len(consumed)} 条；比 HEAD 更新的 prompt：{note}")
        if mode == "post":
            return 0
        if fresh:
            print(f"[门禁] 通过。本次提交带入新的用户 prompt：{note}")
            return 0
    else:
        consumed = set(state)

    if mode == "post":
        fresh = [name for name in present if name not in consumed]
        if fresh:
            save_state(state_path, consumed | set(fresh), present)
        return 0

    pending = [name for name in present if name not in consumed]
    if pending:
        print(f"[门禁] 通过。本次提交带入新的用户 prompt：{'、'.join(pending)}")
        hint = number_hint(present)
        if hint:
            print(hint)
        return 0

    # 单纯改提交信息的 amend 不算新提交，放行。
    if not has_staged_change(root):
        print("[门禁] 暂存区相对 HEAD 没有改动（只改提交信息），放行。")
        return 0

    if skip_path.exists():
        reason = ""
        try:
            for line in skip_path.read_text(encoding="utf-8").splitlines():
                if line.strip():
                    reason = line.strip()
                    break
        except OSError:
            pass
        if not reason:
            print("[门禁] 豁免文件存在但没写理由，按拒绝处理。", file=sys.stderr)
            return 1
        skip_path.unlink(missing_ok=True)
        write_log(store / LOG_NAME, "跳过 prompt 门禁：" + reason)
        print(f"[门禁] 已使用一次性豁免并记入日志：「{reason}」")
        return 0

    print("", file=sys.stderr)
    print("[门禁] 本次提交没有新增用户 prompt，拒绝提交。", file=sys.stderr)
    print("", file=sys.stderr)
    print(f"  要求：{PROMPT_DIR_REL}/ 下必须出现一个新的 .md，一轮一条，四位递增编号。", file=sys.stderr)
    print(f"  现状：已登记 {len(present)} 条，最新 {present[-1]}，它们都已随此前的提交入库。", file=sys.stderr)
    print(f"  下一步：把本轮用户的原话写进 {PROMPT_DIR_REL}/{len(present) + 1:04d}.md 再提交。", file=sys.stderr)
    print(f"  确需跳过：在 {skip_path} 写一行理由，门禁会放行一次并记入日志。", file=sys.stderr)
    print("", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
