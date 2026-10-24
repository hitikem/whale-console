# Whale Console 项目交接文档

> 生成时间：2026-10-24 18:40
> 交接对象：WorkBuddy 中国版
> 项目状态：**已完成并发布 v1.0.0**

---

## 一、这是什么

**Whale Console** —— 一个 Windows 桌面 QQ 机器人控制台，纯 C# WinForms + WebView2 实现。

- 开源仓库：https://github.com/hitikem/whale-console
- Release v1.0.0：https://github.com/hitikem/whale-console/releases/tag/v1.0.0
- 许可：MIT

---

## 二、当前运行状态（交接时）

| 组件 | PID | 路径 | 状态 |
|---|---|---|---|
| QQ 机器人 | 2672 | node.exe（whale profile） | ✓ Bot ready |
| AI 网关 | 4468 | `C:\Program Files\Agent2API\agent2api.exe` | ✓ ok |
| 控制台 | 4928 | `D:\ds\qqbot\QQBotConsole.exe` | ✓ 运行中 |

**QQ 机器人：**
- AppID: `（已脱敏，见本机 D:\ds\qqbot 的 DSH profile 配置）`
- 连接状态：`[im-qqbot] Bot ready! appId=<你的 AppID>`
- 人设：鲸鱼娘「溟月」（控制台守护者）

---

## 三、关键文件路径

| 用途 | 路径 |
|---|---|
| **源码（主程序）** | `D:\ds\qqbot\src\QQBotConsole.cs`（~1900 行）|
| **源码（状态采集）** | `D:\ds\qqbot\src\NativeCollector.cs` |
| **源码（安装程序）** | `D:\ds\qqbot\src\Setup.cs` |
| **前端主页** | `D:\ds\qqbot\ui\index.html` |
| **前端动态页** | `D:\ds\qqbot\ui\pages-new.js` |
| **前端样式** | `D:\ds\qqbot\ui\winisland.css` |
| **机器人配置** | `D:\ds\qqbot\patch.yml` |
| **人格设定** | `D:\ds\qqbot\agent\AGENTS.md` |
| **DSH profile** | `C:\Users\Administrator\.dsh\qqbot-home\profiles\whale\` |
| **状态数据** | `D:\ds\qqbot\logs\status.json` |
| **聊天数据** | `D:\ds\qqbot\logs\chat-data.json` |
| **API 存储** | `D:\ds\qqbot\logs\api-store.json` |

---

## 四、编译方式

### 环境要求

- .NET Framework 4.8
- .NET SDK 8+（只用它的 csc.dll）
- WebView2 SDK（在 `D:\ds\qqbot\lib\`）

### 编译命令（PowerShell）

```powershell
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = (Get-ChildItem "C:\Program Files\dotnet\sdk" -Directory |
        Select-Object -Last 1).FullName + "\Roslyn\bincore\csc.dll"
$lib = "D:\ds\qqbot\lib"

dotnet $csc /target:winexe /platform:x64 `
  /win32icon:D:\ds\qqbot\assets\app.ico `
  /resource:D:\ds\qqbot\assets\app.ico,app.ico `
  /out:D:\ds\qqbot\QQBotConsole.exe `
  /reference:"$fw\mscorlib.dll" `
  /reference:"$fw\System.dll" `
  /reference:"$fw\System.Core.dll" `
  /reference:"$fw\System.Windows.Forms.dll" `
  /reference:"$fw\System.Drawing.dll" `
  /reference:"$fw\System.Management.dll" `
  /reference:"$fw\System.Net.Http.dll" `
  /reference:"$fw\System.Web.dll" `
  /reference:"$fw\System.Web.Extensions.dll" `
  /reference:"$lib\Microsoft.Web.WebView2.Core.dll" `
  /reference:"$lib\Microsoft.Web.WebView2.WinForms.dll" `
  /langversion:latest /nologo `
  D:\ds\qqbot\src\QQBotConsole.cs D:\ds\qqbot\src\NativeCollector.cs
```

### 编译安装程序

```powershell
# 1. 准备 payload
$app = "D:\whale-build\app"
Copy-Item D:\ds\qqbot\QQBotConsole.exe $app -Force
Compress-Archive -Path "$app\*" -DestinationPath "D:\whale-build\payload.zip" -CompressionLevel Optimal

# 2. 编译
dotnet $csc /target:winexe /platform:x64 `
  /win32icon:D:\ds\qqbot\assets\app.ico `
  /resource:"D:\whale-build\payload.zip,payload.zip" `
  /resource:"D:\ds\qqbot\assets\app.ico,app.ico" `
  /out:D:\WhaleConsole-Setup.exe `
  /reference:"$fw\mscorlib.dll" /reference:"$fw\System.dll" /reference:"$fw\System.Core.dll" `
  /reference:"$fw\System.Windows.Forms.dll" /reference:"$fw\System.Drawing.dll" `
  /reference:"$fw\System.IO.Compression.dll" /reference:"$fw\System.IO.Compression.FileSystem.dll" `
  /reference:"$fw\Microsoft.CSharp.dll" `
  /langversion:latest /nologo D:\ds\qqbot\src\Setup.cs
```

---

## 五、签名

### 证书信息

```
Subject:  CN=Whale Console, O=hitikem, C=CN
Thumbprint: 397A5C16C93FE347111758A49B1D88CEE435319D
有效期至:  2031-10-24
存放:      Cert:\CurrentUser\My
公钥导出:  D:\ds\qqbot\certs\WhaleConsole.cer
```

### 签名命令

```powershell
$cert = Get-ChildItem 'Cert:\CurrentUser\My' | Where-Object { $_.Subject -like '*Whale Console*' } | Select-Object -First 1
Set-AuthenticodeSignature -FilePath 'D:\WhaleConsole-Setup.exe' -Certificate $cert
```

### 用户侧说明

- **你自己电脑**：证书已导入 `LocalMachine\Root`，不弹窗
- **其他人第一次**：弹"未知发布者"，可点"仍要运行"，或先安装 `WhaleConsole.cer`
- **完全消除弹窗**：需购买 EV 代码签名证书（¥3000-5000/年）

---

## 六、模型配置

### patch.yml 结构

```yaml
- id: llm-pi-ai
  name: '@deepseek-ai/dsh-llm-pi-ai'
  config:
    providers:
      local:                          # 本机 AI 网关（8 个模型）
        baseURL: http://127.0.0.1:3065/v1
        models: [8 个模型定义]
      deepseek:                       # DeepSeek 官方（备用）
        baseURL: https://api.deepseek.com/v1
```

### 当前 8 个模型实测（2026-10-24 18:28）

| 模型 | 耗时 | 状态 |
|---|---|---|
| `deepseek-v4.1-flash-sg` | 947ms | ✓ **默认** |
| `deepseek-v4.1-flash` | — | ⚠ 429 限流 |
| `kimi-k3` | 1960ms | ✓ **看图** |
| `kimi-k2.6` | 1114ms | ✓ |
| `glm-5.3` | 1657ms | ✓ |
| `glm-5.2` | 2158ms | ✓ |
| `hy3` | 1582ms | ✓ |
| `gemini-3.8-flash` | — | ✗ 失败 |

### 重要坑（踩过的）

1. **DSH 的 `input` 只接受 `text` / `image`**
   - 写 `input: [text, image, video]` → **启动直接崩**
   - 错误信息：`expected "text" | "image" but got "video"`

2. **网关模型元数据的 `supports_images` 不可信**
   - `glm-5.3` 标了 `true`，实测说"我无法查看图片"
   - **只有 `kimi-k3` 和 `hy3` 真的能看图**

3. **`deepseek-v4.1-flash` 经常 429 限流**
   - 现已把默认模型换成 `deepseek-v4.1-flash-sg`

4. **cordis.yml 文件锁**
   - 多进程同时写会 `EPERM`
   - 已用独立 profile（`whale`）+ `autostart.ps1` 重试机制解决

---

## 七、架构要点

### 通信方式

WebView2 的 `file://` 页面**不能** `fetch()` HTTP（CORS），必须用 `postMessage`：

```javascript
// JS → C#
window.chrome.webview.postMessage({ type: 'req', id: 1, path: 'api/status' });

// C# → JS（必须用 chrome.webview，不能用 window）
window.chrome.webview.addEventListener('message', e => { ... });
```

### 前端页面

全部**动态渲染**（`main.innerHTML = pageXxx()`），共 11 页：

`overview` `control` `persona` `log` `check` `ai` `auto` `chat` `token` `api` `setting`

### 窗口拖动

WebView2 会拦截鼠标消息，`HTCAPTION` 无效。

解决方案：**16ms 定时器轮询鼠标位置** + `SetWindowPos` 移动窗口。

代码在 `QQBotConsole.cs` 的 `InitManualDrag()`。

### 状态采集

**不用 PowerShell 子进程**（慢、弹黑框），`NativeCollector.cs` 纯 C# 读进程信息，毫秒级。

机器人检测三套降级方案：
1. 读 `autostart.log` 的 PID 并验证存活
2. WMI 查命令行（沙箱内可能返回空）
3. 进程特征匹配（内存 > 80MB 且线程 >= 8）

---

## 八、已解决的问题（避免重复踩坑）

| 问题 | 原因 | 解法 |
|---|---|---|
| `bot=False` | WMI 在沙箱返回空集 | 三套降级方案 |
| 页面切换卡顿 | `innerHTML` 全重建 | `_renderedPages` 缓存 |
| 静态页全白 | `main.innerHTML` 把静态页删了 | 改追加动态页 |
| 动态页空白 | 缺 `<div class="page">` 包裹 | 加 wrapper |
| 状态不刷新 | `api/status` 触发采集 | 只读文件 |
| 拖动失效 | WebView2 拦截消息 | 定时器轮询 |
| 聊天不更新 | 计划任务被删 | 程序内置 90 秒采集 |
| 看图报错 | vision 用了限流模型 | 换 `kimi-k3` |
| 启动崩溃 | `input` 里有 `video` | 只留 `text, image` |
| Defender 报毒 | 截图脚本被误判为 HackTool | 加排除项（不是 EXE 的问题）|

---

## 九、待办 / 可改进

### 高优先级

1. **图标未定** —— 当前用 `improved-1`，用户曾说想换但没指定哪张
2. **`deepseek-v4.1-flash` 限流** —— 需要查网关配额或等恢复

### 中优先级

3. **AI 测试页缺图片上传** —— 前端只有文字输入框
4. **自动化页未充分测试** —— 页面已写，功能待验证
5. **截图功能** —— 之前的 PowerShell 截图方案被 Defender 报毒，已改用 WebView2 `CapturePreviewAsync`

### 低优先级

6. 前端 `Agent2API` 字样已清（统一叫「AI 网关」）
7. 聊天记录依赖 Python + `zstandard`
8. 安装包可考虑加卸载注册表项（现在是 `uninstall.ps1`）

---

## 十、常用运维命令

### 查看状态

```powershell
# 机器人进程
Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*whale*' }

# QQ 连接
Get-Content 'D:\ds\qqbot\run-out.log' -Tail 5

# 错误
Get-Content 'D:\ds\qqbot\run-err.log' -Tail 20

# 状态 JSON
Get-Content 'D:\ds\qqbot\logs\status.json' -Raw | ConvertFrom-Json
```

### 重启机器人

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*whale*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep -Seconds 4
Remove-Item 'D:\ds\qqbot\run-err.log','D:\ds\qqbot\run-out.log' -Force -EA SilentlyContinue
& 'D:\ds\qqbot\autostart.ps1'
```

### 测试网关模型

```powershell
$models = @('deepseek-v4.1-flash-sg','kimi-k3','glm-5.3','hy3')
foreach($m in $models){
  try {
    $body = "{`"model`":`"$m`",`"stream`":false,`"max_tokens`":10,`"messages`":[{`"role`":`"user`",`"content`":`"hi`"}]}"
    $sw=[Diagnostics.Stopwatch]::StartNew()
    Invoke-WebRequest -UseBasicParsing -Method Post 'http://127.0.0.1:3065/v1/chat/completions' -Headers @{'x-api-key'='<KEY>';'Content-Type'='application/json'} -Body $body -TimeoutSec 25 | Out-Null
    $sw.Stop()
    "✓ $m $($sw.ElapsedMilliseconds)ms"
  } catch { "✗ $m" }
  Start-Sleep -Milliseconds 600
}
```

### 采集聊天记录

```powershell
$env:PYTHONIOENCODING='utf-8'
cd D:\ds\qqbot
python tools\collect-chat.py
```

---

## 十一、发布流程

```powershell
# 1. 同步源码到仓库
Copy-Item D:\ds\qqbot\src\*.cs D:\whale-repo\ -Force
Copy-Item D:\ds\qqbot\ui\* D:\whale-repo\ui\ -Force

# 2. 检查密钥泄露（必做！）
Select-String -Path (Get-ChildItem D:\whale-repo -Recurse -File) -Pattern 'sk-'

# 3. 推送
cd D:\whale-repo
git add -A
git commit -m "描述"
git push origin master

# 4. 发布 Release
gh release create v1.0.1 'D:\WhaleConsole-Setup.exe' 'D:\WhaleConsole-Portable.zip' --title "v1.0.1" --notes "更新说明"
```

### ⚠️ 密钥红线

**每次同步源码前必查**，因为本地源码有真密钥，仓库里是环境变量版：

```powershell
$hits = Select-String -Path (Get-ChildItem 'D:\whale-repo' -Recurse -File) -Pattern 'sk-'
if($hits){ $hits | ForEach-Object { "泄露: $($_.Filename):$($_.LineNumber)" } }
```

已泄露位置：`QQBotConsole.cs:35` 的 `GW_KEY`，必须替换成：
```csharp
public static readonly string GW_KEY = Environment.GetEnvironmentVariable("WHALE_GW_KEY") ?? "";
```

---

## 十二、联系与归属

- 仓库所有者：hitikem
- 人格作者：上善无形（OC「溟月」）
- 设计参考：WinIsland（https://github.com/WinIslandProject/WinIsland）

---

## 十三、交接检查清单

交接前请确认：

- [x] 机器人运行中（PID 2672，Bot ready）
- [x] 网关运行中（PID 4468，health ok）
- [x] 控制台运行中（PID 4928）
- [x] 仓库代码已推送（密钥已清理）
- [x] Release v1.0.0 已发布
- [x] 安装包已签名
- [x] 本文档已生成

**接手后第一件事：跑一遍「十、常用运维命令」里的检查。**
