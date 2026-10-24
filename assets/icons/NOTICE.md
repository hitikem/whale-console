# 图标资源说明 / CREDITS

本目录下的图片资源均可直接使用。来源与授权如下，**公开分发时请保留本文件**。

---

## 1. `whale/` `vendor/deepseek-girl-*` — 鲸鱼娘形象

| 项 | 说明 |
|---|---|
| 来源 | [TreapGoGo/deepseek-whale-girl](https://github.com/TreapGoGo/deepseek-whale-girl) —— 鲸鱼娘开源资产库 |
| 原文件 | `characters/brand/whale-girl-logo-master.png` |
| 处理 | 裁切留白 → 生成 16~1024px 多尺寸；圆形头像、圆角应用图标、`.ico`、头部特写为派生版 |
| 授权 | **需注意**：该仓库的 MIT 许可仅覆盖网站源代码，角色设定图/素材「不包含在上述代码许可中」，须遵循原作者或来源授权。使用前请与仓库维护者确认，或在项目内注明出处 |
| 备注 | 鲸鱼娘是社区自发二创形象，非 DeepSeek 官方角色；DeepSeek 商标归其所有者 |

如果这条授权链不适合你的分发场景，把 `whale/` 和 `vendor/deepseek-girl-*` 换掉即可，其余图标不受影响。

---

## 2. `nav/` — 界面导航图标（14 个）

| 项 | 说明 |
|---|---|
| 来源 | [Lucide](https://github.com/lucide-icons/lucide) v1.61.0 |
| 授权 | **ISC**（可商用、可修改、可再分发，保留版权声明即可） |
| 规格 | 24×24 网格 · 描边 1.8 · 圆头圆角 · 透明背景 |
| 尺寸 | 16 / 24 / 32 / 48 px PNG + 原始 SVG（SVG 用 `currentColor`，跟随文字颜色） |

图标对照：`overview` activity · `control` power · `persona` user-round · `log` file-text ·
`check` list-checks · `ai` sparkles · `auto` clock · `chat` message-circle · `token` chart-column ·
`api` plug · `setting` settings · `refresh` rotate-cw · `camera` camera · `capture` scan-line

---

## 3. `vendor/` — 厂商图标（8 个）

| 项 | 说明 |
|---|---|
| 来源 | [LobeHub Icons](https://github.com/lobehub/lobe-icons)（`@lobehub/icons-static-svg`） |
| 授权 | **MIT** |
| 例外 | `custom`（齿轮）取自 Lucide，ISC |

| 文件前缀 | 厂商 | 原图标 |
|---|---|---|
| `deepseek` | DeepSeek | `deepseek-color` |
| `openai` | OpenAI | `openai` |
| `anthropic` | Anthropic / Claude | `claude-color` |
| `moonshot` | 月之暗面 Kimi | `kimi-color` |
| `zhipu` | 智谱 GLM | `zhipu-color` |
| `qwen` | 阿里通义千问 | `qwen-color` |
| `siliconflow` | 硅基流动 | `siliconcloud-color` |
| `custom` | 自定义 / 本地 | Lucide `settings` |

各厂商名称与标识为其各自所有者的商标，此处仅用于指代对应的服务。

---

## 尺寸与用法

| 用途 | 建议文件 |
|---|---|
| 侧边导航项（15px 显示） | `nav/<name>-24.png` 或内联 SVG |
| 厂商选择格（18px） | `vendor/<name>-glyph-24.png` |
| API 卡片图标（38px） | `vendor/<name>-tile-38.png`、`vendor/deepseek-girl-tile-38.png` |
| 品牌 Logo（28px） | `whale/whale-girl-circle-96.png` |
| 应用图标 / 安装包 | `whale/app.ico`、`whale/app-256.png` |
| 文档、README | `vendor/<name>-tile-128.png`、`preview-*.png` |

高 DPI 场景优先用 SVG，或使用 2 倍尺寸 PNG（如 38px 位置用 64px 文件）。
