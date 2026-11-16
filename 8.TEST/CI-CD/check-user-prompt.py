# -*- coding: utf-8 -*-
"""提交门禁：改动源码或配置的提交，必须带来至少一条新的用户 prompt。

规则：
    1. 暂存区里有「非文档」路径（源码、项目文件、文案 JSON、脚本、清单、配置）时，
       必须出现一个新的用户 prompt 文件，否则拒绝提交。
    2. 暂存区里全是文档与测试日志（`.md`、`8.TEST` 下的 `.log`）时，放行，不消耗 prompt。
       文档回填、版本日志补记、测试日志登记都属于这一类。
    3. 只改提交信息的 amend 放行，不看 prompt。

为什么不看 git 的暂存区里的 prompt 文件：`1.PRD/用户prompt/` 在 `.gitignore` 里，git 看不到。
因此 prompt 侧只能读文件系统，并把「已被提交消费过的文件名」记在 `.git` 目录下，不进入版本库。

文档与代码的判定见 `is_documentation`。原则是宁可误拦不可漏放：
只有明确属于文档或测试产物的路径才算文档，其余一律按代码处理。

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

# 源码目录里只认这两个文档文件，其余一律按代码处理。
SRC_DOC_FILES = ("9.SRC/README.md", "9.SRC/CHANGELOG.md")


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


def is_documentation(path: str) -> bool:
    """这条路径是否属于「改了也不改行为」的文档或测试产物。

    判定顺序有意如此，宁可误拦：
        - 门禁自身所在目录先排除，脚本改动必须带 prompt。
        - `9.SRC/` 是源码目录，只认 README 与 CHANGELOG。
        - `TOOLS/` 下的脚本不算文档，只有 `.md` 说明算。
        - 其余位置只有 `.md` 算文档。
        - `8.TEST/` 下的 `.log` 属于测试产物，算文档。
    """
    p = path.replace("\\", "/")
    if p.startswith("./"):
        p = p[2:]
    if p in SRC_DOC_FILES:
        return True
    if p.startswith("8.TEST/CI-CD/"):
        return False
    if p.startswith("9.SRC/"):
        return False
    if p.startswith("TOOLS/"):
        return p.endswith(".md")
    if p.endswith(".md"):
        return True
    if p.startswith("8.TEST/") and p.endswith(".log"):
        return True
    return False


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


def staged_paths(root: Path):
    """暂存区相对 HEAD 的路径。没有改动时返回空列表，读不到时返回 None。"""
    try:
        result = subprocess.run(
            ["git", "diff", "--cached", "--name-only", "HEAD"],
            cwd=root, capture_output=True, text=True, encoding="utf-8")
        if result.returncode != 0:
            return None
        return [line.strip() for line in result.stdout.splitlines() if line.strip()]
    except OSError:
        return None


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


def describe_paths(paths, limit: int = 6) -> str:
    """把路径列表压成一行，用于提示信息。"""
    shown = paths[:limit]
    text = "、".join(shown)
    if len(paths) > limit:
        text += f" 等 {len(paths)} 个"
    return text


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
        paths = staged_paths(root) or []
        code = [p for p in paths if not is_documentation(p)]
        print(f"目录：{PROMPT_DIR_REL}")
        print(f"总条数：{len(present)}，最新：{present[-1]}")
        print(f"状态文件：{state_path}")
        print("未消费：" + ("、".join(pending) if pending else "无（下次提交会被拦下）"))
        if not paths:
            print("暂存区：空")
        elif code:
            print(f"暂存区：{len(paths)} 个路径，其中 {len(code)} 个要 prompt：{describe_paths(code)}")
        else:
            print(f"暂存区：{len(paths)} 个路径，全部是文档或测试日志，不需要 prompt")
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

    paths = staged_paths(root)
    if paths is None:
        print("[门禁] 读不到暂存区路径，按需要 prompt 处理。", file=sys.stderr)
    else:
        code = [p for p in paths if not is_documentation(p)]
        if not code:
            print(f"[门禁] 本次提交只改文档与测试日志（{len(paths)} 个路径），不需要新的用户 prompt。")
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

    code = [p for p in (paths or []) if not is_documentation(p)]
    print("", file=sys.stderr)
    print("[门禁] 本次提交改动了源码或配置，但没有新增用户 prompt，拒绝提交。", file=sys.stderr)
    print("", file=sys.stderr)
    if code:
        print(f"  涉及：{describe_paths(code)}", file=sys.stderr)
    print(f"  要求：{PROMPT_DIR_REL}/ 下必须出现一个新的 .md，一轮一条，四位递增编号。", file=sys.stderr)
    print(f"  现状：已登记 {len(present)} 条，最新 {present[-1]}，它们都已随此前的提交入库。", file=sys.stderr)
    print(f"  下一步：把本轮用户的原话写进 {PROMPT_DIR_REL}/{len(present) + 1:04d}.md 再提交。", file=sys.stderr)
    print("  只改文档与测试日志的提交不需要 prompt，可以直接提交。", file=sys.stderr)
    print(f"  确需跳过：在 {skip_path} 写一行理由，门禁会放行一次并记入日志。", file=sys.stderr)
    print("", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
