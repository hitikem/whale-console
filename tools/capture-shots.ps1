# ═══════════════════════════════════════════════════════════
#  逐页截图（带页面验证）
# ═══════════════════════════════════════════════════════════
param(
    [string]$OutDir = 'D:\ds\qqbot\docs\screenshots',
    [int]$Width = 1400
)

$pages = @(
    @('overview', '01-overview', '概览'),
    @('control',  '02-control',  '控制'),
    @('persona',  '03-persona',  '人格'),
    @('log',      '04-log',      '日志'),
    @('check',    '05-check',    '自检'),
    @('ai',       '06-ai',       'AI'),
    @('auto',     '07-auto',     '自动化'),
    @('chat',     '08-chat',     '聊天'),
    @('token',    '09-token',    'Token'),
    @('api',      '10-api',      'API'),
    @('setting',  '11-setting',  '设置')
)

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

foreach ($p in $pages) {
    $page = $p[0]
    $name = $p[1]

    Write-Host "  [$name] 打开 $page ..." -NoNewline

    Get-Process QQBotConsole -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 900
    # 清缓存，避免旧 hash 残留
    Remove-Item 'D:\ds\qqbot\logs\wv2' -Recurse -Force -ErrorAction SilentlyContinue

    $log = 'D:\ds\qqbot\logs\console.log'
    Remove-Item $log -ErrorAction SilentlyContinue

    cmd /c start "" "D:\ds\qqbot\QQBotConsole.exe" --page=$page

    # 等页面加载 + 数据请求完成
    $ok = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        $txt = Get-Content $log -ErrorAction SilentlyContinue -Raw
        if ($txt) {
            # 动态页看是否请求了对应 API；静态页看 status 请求
            if ($page -in @('chat','token')) {
                if ($txt -match 'api/chat') { $ok = $true; break }
            } elseif ($page -eq 'api') {
                if ($txt -match 'api/apis') { $ok = $true; break }
            } else {
                if ($txt -match 'api/status') { $ok = $true; break }
            }
        }
    }

    # 再多等数据渲染
    Start-Sleep -Seconds 3

    $out = Join-Path $OutDir "$name.jpg"
    & 'D:\ds\qqbot\shot-console.ps1' -OutFile $out -TargetWidth $Width | Out-Null

    if (Test-Path $out) {
        $kb = [math]::Round((Get-Item $out).Length / 1KB)
        Write-Host " OK $($kb)KB$(if(-not $ok){' (数据请求未确认)'})" -ForegroundColor Green
    } else {
        Write-Host " FAIL" -ForegroundColor Red
    }
}

Copy-Item 'D:\ds\qqbot\assets\app.png' (Join-Path $OutDir 'icon.png') -Force
Write-Host ""
Write-Host "截图完成" -ForegroundColor Cyan
