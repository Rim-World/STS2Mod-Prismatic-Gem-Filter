<#
安装 prompt 门禁的两个钩子。

用法：
    powershell -ExecutionPolicy Bypass -File 8.TEST/CI-CD/install-hooks.ps1

做的事：
    1. 在 .git/hooks/ 下写 pre-commit 与 post-commit。
    2. 已有同名钩子时先改名为 *.bak-<时间戳>，不覆盖。
    3. 两个钩子都只调用 8.TEST/CI-CD/check-user-prompt.py，不做别的事。

钩子用 LF 换行、不带 BOM 写出。带 BOM 时 sh 无法识别第一行的 shebang。
#>

$ErrorActionPreference = 'Stop'

$root = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if (-not $root) { throw '找不到仓库根目录。' }

$hooks = Join-Path (Join-Path $root '.git') 'hooks'
if (-not (Test-Path $hooks)) { $null = New-Item -ItemType Directory -Path $hooks }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$utf8 = New-Object System.Text.UTF8Encoding($false)

$pre = @"
#!/bin/sh
# 由 8.TEST/CI-CD/install-hooks.ps1 安装，$stamp
# 门禁：每次提交必须带来至少一条新的用户 prompt。规则见 1.PRD/用户prompt/ 与 8.TEST/TEST.md。
command -v python >/dev/null 2>&1 || { echo "[门禁] 找不到 python，本次跳过 prompt 门禁。" >&2; exit 0; }
exec python 8.TEST/CI-CD/check-user-prompt.py --mode pre
"@ -replace "`r`n", "`n"

$post = @"
#!/bin/sh
# 由 8.TEST/CI-CD/install-hooks.ps1 安装，$stamp
# 提交成功后把本次带入的 prompt 记为已消费。失败不影响提交结果。
python 8.TEST/CI-CD/check-user-prompt.py --mode post >/dev/null 2>&1 || true
exit 0
"@ -replace "`r`n", "`n"

function Install-Hook {
    param([string]$Name, [string]$Body)

    $target = Join-Path $hooks $Name
    if (Test-Path $target) {
        $backup = "$target.bak-$stamp"
        Move-Item -LiteralPath $target -Destination $backup
        Write-Output "已备份原钩子：$backup"
    }

    [System.IO.File]::WriteAllText($target, $Body, $utf8)
    Write-Output "已写入：$target（$([System.Text.Encoding]::UTF8.GetByteCount($Body)) 字节）"
}

Install-Hook -Name 'pre-commit' -Body $pre
Install-Hook -Name 'post-commit' -Body $post

Write-Output ''
Write-Output '门禁已安装。下一次 git commit 起生效。'
Write-Output "状态文件：$hooks\..\user-prompt-gate.json"
