# ═══════════════════════════════════════════════════════════════
#  Whale Console 安装程序
#  右键 → 用 PowerShell 运行
# ═══════════════════════════════════════════════════════════════

$ErrorActionPreference = 'Continue'
$Version = '1.0.0'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Clear-Host
Write-Host ""
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host "  |                                               |" -ForegroundColor Cyan
Write-Host "  |        Whale Console  v$Version                |" -ForegroundColor Cyan
Write-Host "  |        QQ 机器人控制台                        |" -ForegroundColor Cyan
Write-Host "  |                                               |" -ForegroundColor Cyan
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host ""
Write-Host "  安装目录: $root" -ForegroundColor Gray
Write-Host ""

# [1/6] 环境检查
Write-Host "[1/6] 检查运行环境" -ForegroundColor Yellow
$env_ok = $true

try {
    $net = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop
    if ($net.Release -ge 528040) {
        Write-Host "      .NET Framework  $($net.Version)          OK" -ForegroundColor Green
    } else {
        Write-Host "      .NET Framework  版本过低，需要 4.8+" -ForegroundColor Red
        Write-Host "      https://dotnet.microsoft.com/download/dotnet-framework/net48" -ForegroundColor Yellow
        $env_ok = $false
    }
} catch {
    Write-Host "      .NET Framework  未安装" -ForegroundColor Red
    $env_ok = $false
}

$wv2 = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' -ErrorAction SilentlyContinue
if ($wv2.pv) {
    Write-Host "      WebView2         $($wv2.pv)               OK" -ForegroundColor Green
} else {
    Write-Host "      WebView2         未安装" -ForegroundColor Red
    Write-Host "      https://go.microsoft.com/fwlink/p/?LinkId=2124703" -ForegroundColor Yellow
    $env_ok = $false
}

$py = Get-Command python -ErrorAction SilentlyContinue
if ($py) {
    $pyVer = (& python --version 2>&1) -replace 'Python ', ''
    Write-Host "      Python           $pyVer                   OK" -ForegroundColor Green
    & python -c "import zstandard" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "      安装 zstandard 依赖..." -ForegroundColor Gray
        & python -m pip install zstandard --quiet 2>&1 | Out-Null
    }
} else {
    Write-Host "      Python           未安装（聊天记录功能不可用）" -ForegroundColor Yellow
}

Write-Host ""
if (-not $env_ok) {
    Write-Host "  !! 环境不完整，请先安装缺少的组件" -ForegroundColor Red
    $c = Read-Host "  仍然继续？(y/n)"
    if ($c -ne 'y') { exit }
}
Write-Host ""

# [2/6] 创建目录
Write-Host "[2/6] 创建目录" -ForegroundColor Yellow
foreach ($d in @('logs', 'logs\wv2', 'docs\screenshots')) {
    $p = Join-Path $root $d
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
}
Write-Host "      OK" -ForegroundColor Green
Write-Host ""

# [3/6] 初始化配置
Write-Host "[3/6] 初始化配置" -ForegroundColor Yellow
$patchExample = Join-Path $root 'patch.yml.example'
$patchFile = Join-Path $root 'patch.yml'
if ((Test-Path $patchExample) -and -not (Test-Path $patchFile)) {
    Copy-Item $patchExample $patchFile
    Write-Host "      已创建 patch.yml" -ForegroundColor Green
} elseif (Test-Path $patchFile) {
    Write-Host "      patch.yml 已存在，跳过" -ForegroundColor Gray
}

$apiStore = Join-Path $root 'logs\api-store.json'
if (-not (Test-Path $apiStore)) {
    @'
[{"id":"default","name":"AI 网关","baseUrl":"http://127.0.0.1:3065","apiKey":"","model":"deepseek-v4.1-flash","isDefault":true}]
'@ | Out-File $apiStore -Encoding UTF8
    Write-Host "      已创建 api-store.json" -ForegroundColor Green
}
Write-Host ""

# [4/6] 桌面快捷方式
Write-Host "[4/6] 创建桌面快捷方式" -ForegroundColor Yellow
$desktop = [Environment]::GetFolderPath('Desktop')
$lnkPath = Join-Path $desktop 'Whale Console.lnk'
$exePath = Join-Path $root 'QQBotConsole.exe'
try {
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnkPath)
    $sc.TargetPath = $exePath
    $sc.IconLocation = "$exePath,0"
    $sc.WorkingDirectory = $root
    $sc.Description = 'Whale Console - QQ 机器人控制台'
    $sc.Save()
    [Runtime.InteropServices.Marshal]::ReleaseComObject($ws) | Out-Null
    Write-Host "      OK" -ForegroundColor Green
} catch {
    Write-Host "      失败: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# [5/6] 开机自启
Write-Host "[5/6] 开机自启" -ForegroundColor Yellow
Write-Host "      是否开机自动启动控制台？" -ForegroundColor Gray
$boot = Read-Host "      (y/n，默认 n)"
if ($boot -eq 'y') {
    $taskName = 'WhaleConsole-AutoStart'
    schtasks /Delete /TN $taskName /F 2>&1 | Out-Null
    schtasks /Create /TN $taskName /TR "`"$exePath`"" /SC ONLOGON /F 2>&1 | Out-Null
    Write-Host "      已设置开机自启" -ForegroundColor Green
} else {
    Write-Host "      跳过" -ForegroundColor Gray
}
Write-Host ""

# [6/6] 完成
Write-Host "[6/6] 安装完成" -ForegroundColor Green
Write-Host ""
Write-Host "  +-----------------------------------------------+" -ForegroundColor Cyan
Write-Host "  |                                               |" -ForegroundColor Cyan
Write-Host "  |   安装成功！                                  |" -ForegroundColor Cyan
Write-Host "  |   双击桌面「Whale Console」启动               |" -ForegroundColor Cyan
Write-Host "  |                                               |" -ForegroundColor Cyan
Write-Host "  +-----------------------------------------------+" -ForegroundColor Cyan
Write-Host ""
Write-Host "  下一步：" -ForegroundColor Yellow
Write-Host "    1. 打开控制台" -ForegroundColor Gray
Write-Host "    2. 到「API 管理」配置 API Key" -ForegroundColor Gray
Write-Host "    3. 到「设置」调整参数" -ForegroundColor Gray
Write-Host "    4. 点「启动」" -ForegroundColor Gray
Write-Host ""
Write-Host "  遇到问题见 README.md 的「故障排查」" -ForegroundColor Gray
Write-Host ""

$open = Read-Host "  现在打开控制台？(y/n)"
if ($open -eq 'y') {
    Start-Process $exePath
}
