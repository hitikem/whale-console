# Whale Console · 鲸鱼娘控制台

> 一个开源的 QQ 机器人桌面控制台。
> 一键启停 · 实时监控 · 故障自检 · AI 对话
> Windows 桌面应用 · 液态玻璃界面 · 单文件 342KB

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-blue" alt="platform">
  <img src="https://img.shields.io/badge/.NET-Framework%204.8-512BD4" alt="dotnet">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="license">
  <img src="https://img.shields.io/badge/version-1.0.0-orange" alt="version">
</p>

---

## 这是什么

**Whale Console** 是一个 Windows 桌面程序，用来管理和监控 QQ 机器人。

不用记命令、不用开终端。双击就能启动，点按钮就能控制。

### 它能做什么

| 功能 | 说明 |
|---|---|
| **一键启停** | 启动 / 停止 / 重启机器人 |
| **实时监控** | 进程 PID、运行时长、CPU、内存、连接数，每 3 秒自动刷新 |
| **故障自检** | 10 项检查：进程、网关、DNS、模型、日志 |
| **AI 对话** | 内置鲸鱼娘助手，用自然语言问系统状态 |
| **多模型支持** | 支持任何 OpenAI 兼容 API（DeepSeek / OpenAI / 月之暗面 / 智谱 / 通义 等）|
| **限流自动降级** | 模型 429 时自动切换到备用模型 |
| **聊天记录** | 查看 QQ 群聊和私聊消息 |
| **Token 统计** | 输入/输出 Token 用量，3 个扇形图 |
| **系统托盘** | 托盘图标，右键快速启停 |

---

## 系统要求

| 项 | 要求 | 说明 |
|---|---|---|
| 操作系统 | Windows 10 1809+ / Windows 11 | |
| .NET Framework | 4.8 | Win10 1903+ 自带 |
| WebView2 Runtime | 任意版本 | Win11 自带，Win10 需安装 |
| Python | 3.9+ | **可选**，只有聊天记录功能需要 |

### 依赖检查

安装脚本会自动检测环境。也可以手动检查：

```powershell
# .NET 4.8
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
# >= 528040 表示已安装 4.8

# WebView2
Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
```

WebView2 下载：https://go.microsoft.com/fwlink/p/?LinkId=2124703

---

## 安装

### 方式一：安装脚本（推荐）

1. 下载 `WhaleConsole-v1.0.0.zip`
2. 解压到任意目录，比如 `D:\WhaleConsole`
3. 右键 `install.ps1` → **用 PowerShell 运行**

安装脚本会：
- 检查运行环境
- 创建日志目录
- 注册后台采集任务
- 创建桌面快捷方式

### 方式二：手动

1. 解压到任意目录
2. 双击 `QQBotConsole.exe`

---

## 使用教程

### 第一次启动

双击 `WhaleConsole.exe`，会看到：

- **左侧导航**：11 个功能页
- **顶部**：窗口标题和刷新按钮
- **左下角**：机器人 / 网关状态灯

### 概览页

6 个指标实时刷新：

```
进程 PID     机器人进程号
运行时长     从启动到现在
CPU 占用     真实百分比
内存占用     XXX MB / 总内存
消息量       今日收发
出站连接     到服务器的连接数
```

### 控制页

三个大按钮：

- **启动** — 启动机器人（自动先启动网关）
- **停止** — 停止机器人
- **重启** — 先停后启

下面有操作记录。

### 人格页

管理机器人的人格（提示词）。

- 从预设列表选择
- 或直接编辑文本
- 保存后下一条消息立即生效

### 日志页

查看 4 种日志：

| 日志 | 内容 |
|---|---|
| `run-out.log` | 运行输出 |
| `run-err.log` | 错误 |
| `autostart.log` | 启动过程 |
| `console.log` | 控制台自身 |

**有缓存**：进页面立刻显示上次的内容，再异步加载最新。

### 自检页

10 项自动检查：

1. 机器人进程
2. 网关进程
3. 网关 HTTP 健康
4. 模型连通性
5. DNS 解析
6. DNS 劫持检测
7. 出站连接
8. 错误日志
9. 配置文件
10. 磁盘空间

点「开始自检」运行。

### AI 测试页

**跟鲸鱼娘聊天**，同时验证模型是否正常。

- 顶部显示当前人格
- 中间是对话区
- 底部是输入框

快捷测试按钮：

- 打招呼
- 累了
- 自我介绍
- 检查网关

**鲸鱼娘会自动检测真实系统状态并回答。**

### 自动化页

设置自动看护：

| 选项 | 说明 |
|---|---|
| 掉线自动重启 | 检测到机器人挂了自动拉起 |
| 网关掉线自动重启 | 网关不在就重启 |
| DNS 劫持自动修复 | 检测到 198.18.x.x 自动关 TUN |
| 模型限流自动切换 | 429 时换模型 |
| 巡检间隔 | 多久做一次完整检查 |
| 开机自启 | Windows 启动时自动打开 |

### 聊天页

显示 QQ 群聊和私聊消息。

可以筛选：
- 全部消息
- 只看群聊
- 只看私聊
- 只看用户发的
- 只看机器人回的

### Token 页

3 个扇形图：

| 图 | 内容 |
|---|---|
| Token 构成 | 输入 vs 输出 |
| 每日用量 | 每天消耗 |
| 模型占比 | 各模型占比 |

### API 页

管理 AI 厂商和密钥。

**添加厂商：**

1. 点「+ 添加厂商」
2. 从 10 个预设中选一个（DeepSeek / OpenAI / Anthropic / 月之暗面 / 智谱 / 通义 / MiniMax / 硅基流动 / 自定义）
3. Base URL 和模型列表会自动填好
4. 填 API Key
5. 点「测试连接」验证
6. 点「保存」

**支持任何 OpenAI 兼容端点**，不限于预设列表。

### 设置页

管理机器人参数：

- 模型
- 群里是否必须 @ 才回复
- 流式输出
- 显示工具结果
- 调试模式

网关检测：

- 网关地址
- 检测间隔
- 掉线自动重启
- 自动修 DNS 劫持
- 立即检测按钮

---

## 兼容性说明

### 这是一个通用控制台

**Whale Console 不绑定任何特定的 QQ 机器人框架。**

它通过以下方式工作：

1. **进程检测** — 扫描系统中的 Node.js / Python 进程，识别可能的 QQ 机器人
2. **状态文件** — 读取 `logs/status.json`（由控制台自己写入）
3. **OpenAI 协议** — 通过标准 `/v1/chat/completions` 调用 AI 模型

### 支持的范围

| 场景 | 支持情况 |
|---|---|
| DeepSeek Harness + QQ Bot | ✅ 完整支持（开发环境） |
| 任何 OpenAI 兼容 API | ✅ 完整支持 |
| 其他 Node.js 机器人框架 | ⚠️ 可检测进程，但配置文件需手动指定 |
| 腾讯官方 QQ 机器人 API | ⚠️ 可检测连接，但不能管理 |

### 不支持的

- ❌ 手机 QQ 客户端本身（这是桌面控制台，不是手机 App）
- ❌ 非 OpenAI 协议的 API
- ❌ 云端部署的机器人

---

## 故障排查

### 界面空白

按 `F12` 打开开发者工具看控制台错误。

### 机器人启动失败

看 **日志页** → `run-err.log`。

常见错误：

| 错误 | 原因 | 解决 |
|---|---|---|
| `EPERM: operation not permitted` | 配置文件被其他进程占用 | 用独立 profile，或关掉其他实例 |
| `401` | API Key 无效 | 到 API 页测试 |
| `429` | 限流 | 自动降级到其他模型 |
| `ECONNREFUSED` | 网关没启动 | 点「启动」会自动拉起 |

### 模型调不通

1. 到 **API 页** 点「测试」
2. 看返回的信息
3. 如果是 429，换一个模型
4. 如果是 401，检查 Key

### 托盘图标不见了

Windows 可能折叠到隐藏区域，点任务栏 `^` 展开。

### 数据不刷新

检查右下角时间戳。如果不动：

1. 重启控制台
2. 检查 `logs/status.json` 是否存在

---

## 从源码构建

### 环境

```
- .NET Framework 4.8
- .NET SDK 8+（只为 csc.dll）
- WebView2 SDK（已在 lib/）
```

### 编译

```powershell
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = (Get-ChildItem "C:\Program Files\dotnet\sdk" -Directory |
        Select-Object -Last 1).FullName + "\Roslyn\bincore\csc.dll"

dotnet $csc /target:winexe /platform:x64 `
  /win32icon:assets\app.ico `
  /resource:assets\app.ico,app.ico `
  /out:QQBotConsole.exe `
  /reference:"$fw\mscorlib.dll" `
  /reference:"$fw\System.dll" `
  /reference:"$fw\System.Core.dll" `
  /reference:"$fw\System.Windows.Forms.dll" `
  /reference:"$fw\System.Drawing.dll" `
  /reference:"$fw\System.Management.dll" `
  /reference:"$fw\System.Net.Http.dll" `
  /reference:"$fw\System.Web.dll" `
  /reference:"$fw\System.Web.Extensions.dll" `
  /reference:"lib\Microsoft.Web.WebView2.Core.dll" `
  /reference:"lib\Microsoft.Web.WebView2.WinForms.dll" `
  /langversion:latest /nologo `
  src\QQBotConsole.cs src\NativeCollector.cs
```

### 前端开发

纯 HTML/CSS/JS，**无框架、无构建步骤**：

```
ui/
  index.html        主界面 + 内联 JS
  winisland.css     设计系统
  pages-new.js      所有动态页面
  liquid-glass.js   液态玻璃
  fonts/            SF Pro WOFF2
```

改完按 `Ctrl+R` 刷新，不用编译。

---

## 技术细节

### 架构

```
QQBotConsole.exe (C# WinForms)
├─ MainForm         窗口、托盘、拖动
├─ Backend          API 路由、进程管理
└─ NativeCollector  纯 C# 状态采集

WebView2 (渲染前端)
└─ ui/index.html + pages-new.js
```

### 通信

WebView2 的 `file://` 页面**不能** `fetch()` HTTP，所以用 `postMessage`：

```js
// JS → C#
window.chrome.webview.postMessage({
  type: 'req', id: 1, path: 'api/status'
});

// C# → JS（必须用 chrome.webview.addEventListener）
window.chrome.webview.addEventListener('message', e => {
  console.log(e.data);
});
```

### 为什么用纯 C# 采集

启动 PowerShell 子进程有两个问题：
1. 每次要 2.5 秒，阻塞 UI
2. 会弹出黑框

`NativeCollector.cs` 用 .NET 原生 API 直接读进程信息，**毫秒级，零开销**。

### 窗口拖动

WebView2 会拦截鼠标消息，`HTCAPTION` 失效。

所以用**定时器轮询**：
- 每 16ms 检查鼠标位置和按下状态
- 如果在可拖区域（非控件区），就 `SetWindowPos` 移动窗口

### 性能

| 操作 | 耗时 |
|---|---|
| 页面切换 | 0.3-1.2ms |
| 状态采集 | < 10ms |
| 首次加载 | ~1.5s |

---

## 更新日志

### v1.0.0 (2026-10-24)

首个版本。

- 液态玻璃界面（11 个页面）
- 一键启停 + 实时监控
- 10 项故障自检
- AI 对话（鲸鱼娘人格）
- 多厂商 API 管理
- 限流自动降级
- 聊天记录 + Token 统计
- 系统托盘
- 无边框圆角窗口，整窗可拖动
- 页面切换 0.3-1.2ms

---

## 免责声明

### 使用风险

**本软件按「现状」提供，不提供任何明示或暗示的担保。**

使用本软件的风险由使用者自行承担。作者不对以下情况负责：

- 因使用本软件造成的任何直接或间接损失
- 数据丢失或损坏
- 机器人账号被封禁或限制
- 因配置错误导致的任何问题

### 合规提醒

使用本软件时，请遵守：

1. **腾讯 QQ 服务条款** — 包括但不限于《QQ 软件许可及服务协议》
2. **相关法律法规** — 包括《网络安全法》《个人信息保护法》等
3. **AI 服务商条款** — 各 API 提供商的使用协议

**禁止用途：**

- 发送垃圾信息或骚扰他人
- 传播违法或有害内容
- 侵犯他人隐私或知识产权
- 任何违反当地法律的行为

### 内容责任

机器人的人格设定、回复内容由使用者自行配置。**作者不对机器人生成的任何内容负责。**

使用者应当确保：

- 机器人不会生成违法有害内容
- 不会冒充他人
- 不会误导用户

### 第三方组件

本软件使用了以下第三方组件：

| 组件 | 许可 | 用途 |
|---|---|---|
| Microsoft Edge WebView2 | 微软许可 | 渲染引擎 |
| .NET Framework | MIT | 运行时 |
| SF Pro 字体 | Apple 许可 | 界面字体 |

---

## 许可

**代码部分：MIT License**

详见 [LICENSE](LICENSE) 文件。

---

## 致谢

- [DeepSeek Harness](https://deepseek.com) — 机器人框架
- [WinIsland](https://github.com/WinIslandProject/WinIsland) — 液态玻璃设计参考
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — 微软浏览器内核

---

## 贡献

欢迎提 Issue 和 PR。

提 PR 前请确保：

1. 代码能编译通过
2. 不引入新依赖（除非必要）
3. 保持界面一致性

---

<p align="center">
  <sub>Made with 🐳 by the Whale Console contributors</sub>
</p>
