using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QQBotConsole
{
    // ============================================================
    //  QQ Bot 控制台 — WebView2 宿主
    //  前端：HTML/CSS（Apple 液态玻璃设计语言）
    //  后端：真实系统命令 + 三行日志验证
    // ============================================================

    static class Cfg
    {
        public const string QQBOT = @"D:\ds\qqbot";
        public const string AGENT = @"D:\ds\qqbot\agent";
        public const string PERSONA = @"D:\ds\qqbot-repo\personas";
        public const string UI = @"D:\ds\qqbot\ui\index.html";
        public const string NODE = @"C:\Program Files\nodejs\node.exe";
        public const string DSH_JS = @"C:\Users\Administrator\AppData\Roaming\npm\node_modules\@deepseek-ai\dsh\lib\bin.js";
        public const string GATEWAY = @"C:\Program Files\Agent2API\agent2api.exe";
        public static readonly string GW_KEY = Environment.GetEnvironmentVariable("WHALE_GW_KEY") ?? "";
        public static readonly string CLASH_KEY = Environment.GetEnvironmentVariable("CLASH_SECRET") ?? "";
        public const string PYTHON = "python";
        public static readonly string CHAT_PY = Path.Combine(QQBOT, "tools", "collect-chat.py");
        public static readonly string CHAT_JSON = Path.Combine(QQBOT, "logs", "chat-data.json");
        public static readonly string API_STORE = Path.Combine(QQBOT, "logs", "api-store.json");
        public const int CLASH_PORT = 9097;
        public const string GW_BASE = "http://127.0.0.1:3065";
        public static readonly string PATCH = Path.Combine(QQBOT, "patch.yml");
        public static readonly string AUTOSTART = Path.Combine(QQBOT, "autostart.ps1");
        public static readonly string LOGDIR = Path.Combine(QQBOT, "logs");
    }

    class OpRecord
    {
        public string time = DateTime.Now.ToString("HH:mm:ss");
        public string action = "";
        public string result = "";
        public bool? verify = null;
    }

    // ============================================================
    //  后端 API
    // ============================================================
    class Backend
    {
        static readonly HttpClient Hc = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        public static List<OpRecord> Ops = new List<OpRecord>();
        public static List<string> AutoLog = new List<string>();
        public static bool AutoEnabled = false;
        public static int AutoInterval = 60;
        public static int AutoAction = 1;
        static DateTime _lastAuto = DateTime.MinValue;
        static int _autoRuns = 0;

        // 读环境变量（带默认值）
        public static string Env(string name, string def)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? def : v;
        }

        
public static bool ReadBool(string jsonFile, string key)
        {
            try
            {
                if (!File.Exists(jsonFile)) return false;
                var t = File.ReadAllText(jsonFile, Encoding.UTF8);
                var m = Regex.Match(t, "\"" + key + "\"\\s*:\\s*(true|false)");
                return m.Success && m.Groups[1].Value == "true";
            }
            catch { return false; }
        }

        // JSON 字符串转义（Backend 内部用）
        public static string Esc(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32) sb.Append("\\u" + ((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        public static void Log(string s)
        {
            try
            {
                Directory.CreateDirectory(Cfg.LOGDIR);
                var path = Path.Combine(Cfg.LOGDIR, "console.log");

                // 超过 1MB 就截断，只保留最后 2000 行
                try
                {
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                    {
                        var keep = File.ReadAllLines(path);
                        var tail = keep.Length > 2000
                            ? keep.Skip(keep.Length - 2000).ToArray()
                            : keep;
                        File.WriteAllLines(path, tail, new UTF8Encoding(false));
                    }
                }
                catch { }

                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\r\n");
            }
            catch { }
        }

        // ---------- 进程 ----------
        // 用 PowerShell 查进程（本进程受沙箱限制看不到其他进程）
        static string RunPS(string script, int timeoutMs = 8000)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + script + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    var outS = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return outS.Trim();
                }
            }
            catch { return ""; }
        }

        // 调用外部 ps1 脚本（用参数列表避免引号问题）
        public static string RunPsScript(string script, string[] args, int timeoutMs = 15000)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8
                };
                var sbArg = new StringBuilder();
                sbArg.Append("-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"");
                sbArg.Append(script);
                sbArg.Append("\"");
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        sbArg.Append(" \"");
                        sbArg.Append((a ?? "").Replace("\"", "`\""));
                        sbArg.Append("\"");
                    }
                }
                psi.Arguments = sbArg.ToString();

                using (var p = Process.Start(psi))
                {
                    var outS = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return outS.Trim();
                }
            }
            catch (Exception e) { return "{\"ok\":false,\"msg\":\"" + e.Message.Replace("\"", "'") + "\"}"; }
        }

        public static string ConfigIo(string action, string key = "", string value = "")
        {
            var ps1 = Path.Combine(Cfg.QQBOT, "config-io.ps1");
            if (!File.Exists(ps1)) return "{\"ok\":false,\"msg\":\"config-io.ps1 不存在\"}";
            var args = new List<string> { "-Action", action };
            if (!string.IsNullOrEmpty(key)) { args.Add("-Key"); args.Add(key); }
            if (value != null) { args.Add("-Value"); args.Add(value); }
            return RunPsScript(ps1, args.ToArray());
        }

        public static uint[] Pids()
        {
            var l = new List<uint>();

            // 方案A：读 autostart.log 最新的 PID 并验证存活
            try
            {
                var logPath = Path.Combine(Cfg.QQBOT, "autostart.log");
                if (File.Exists(logPath))
                {
                    var lines = File.ReadAllLines(logPath, Encoding.UTF8);
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        var m = Regex.Match(lines[i], @"qqbot launched, pid (\d+)");
                        if (!m.Success) continue;
                        uint pid = uint.Parse(m.Groups[1].Value);
                        try
                        {
                            var p = Process.GetProcessById((int)pid);
                            if (!p.HasExited) { l.Add(pid); return l.ToArray(); }
                        }
                        catch { }
                        break;
                    }
                }
            }
            catch { }

            // 方案B：WMI 查命令行匹配 whale/qqbot
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'node.exe'"))
                {
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        var cl = Convert.ToString(mo["CommandLine"]) ?? "";
                        if (cl.IndexOf("--profile whale", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cl.IndexOf("--profile qqbot", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            l.Add(Convert.ToUInt32(mo["ProcessId"]));
                        }
                    }
                }
                if (l.Count > 0) return l.ToArray();
            }
            catch { }

            // 方案C：进程特征（内存 > 60MB 且线程 >= 8，排除已知的 MCP 进程）
            try
            {
                foreach (var p in Process.GetProcessesByName("node"))
                {
                    try
                    {
                        if (p.HasExited) continue;
                        if (p.Id == Process.GetCurrentProcess().Id) continue;
                        if (p.WorkingSet64 > 60L * 1024 * 1024 && p.Threads.Count >= 8)
                            l.Add((uint)p.Id);
                    }
                    catch { }
                }
            }
            catch { }

            return l.ToArray();
        }

        public static bool GatewayAlive()
        {
            try
            {
                var o = RunPS("(Get-Process agent2api -ErrorAction SilentlyContinue) -ne $null");
                return o.Trim().ToLower() == "true";
            }
            catch { return false; }
        }

        public static int Conns(uint pid)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -Command \"(Get-NetTCPConnection -OwningProcess " + pid + " -State Established -ErrorAction SilentlyContinue | Where-Object { $_.RemotePort -eq 443 }).Count\"")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    var s = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(4000);
                    int n; return int.TryParse(s, out n) ? n : -1;
                }
            }
            catch { return -1; }
        }

        // ---------- 操作 ----------
        public static async Task<(bool ok, string msg, string detail)> Start()
        {
            var ex = Pids();
            if (ex.Length > 0)
                return (true, "已在运行 PID " + string.Join(",", ex), "跳过启动：进程已存在");

            Log("START");
            try { File.Delete(Path.Combine(Cfg.QQBOT, "run-err.log")); } catch { }
            try { File.Delete(Path.Combine(Cfg.QQBOT, "autostart.lock")); } catch { }

            var sb = new StringBuilder();
            sb.AppendLine("执行: powershell -File autostart.ps1");

            if (!GatewayAlive())
            {
                sb.AppendLine("网关未运行，先启动网关…");
                try
                {
                    Process.Start(new ProcessStartInfo(Cfg.GATEWAY)
                    { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                    await Task.Delay(6000);
                }
                catch { }
            }

            try
            {
                Process.Start(new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + Cfg.AUTOSTART + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch (Exception e)
            {
                return (false, "启动失败: " + e.Message, sb + "错误: " + e.Message);
            }

            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1500);
                var p = Pids();
                if (p.Length > 0)
                {
                    await Task.Delay(3000);
                    var alive = Pids();
                    sb.AppendLine("结果: 成功 PID " + string.Join(",", p));
                    sb.AppendLine("验证: " + (alive.Length > 0 ? "进程存活 ✓" : "启动后退出 ✗"));
                    return (alive.Length > 0, "启动成功 PID " + string.Join(",", p), sb.ToString());
                }
            }
            sb.AppendLine("结果: 超时（45s）");
            sb.AppendLine("验证: 进程不存在 ✗");
            return (false, "启动超时", sb.ToString());
        }

        public static async Task<(bool ok, string msg, string detail)> Stop()
        {
            var p = Pids();
            if (p.Length == 0) return (true, "未在运行", "跳过：进程不存在");

            var sb = new StringBuilder();
            sb.AppendLine("执行: Stop-Process -Id " + string.Join(",", p) + " -Force");
            foreach (var id in p)
                try { Process.GetProcessById((int)id).Kill(); } catch { }

            await Task.Delay(1500);
            var rest = Pids();
            sb.AppendLine("结果: " + (rest.Length == 0 ? "进程已退出" : "残留 " + string.Join(",", rest)));
            sb.AppendLine("验证: " + (rest.Length == 0 ? "进程不存在 ✓" : "进程仍存在 ✗"));
            return (rest.Length == 0, rest.Length == 0 ? "已停止" : "有残留", sb.ToString());
        }

        public static async Task<(bool ok, string msg, string detail)> Restart()
        {
            var a = await Stop();

            // 等旧进程完全退出 + cordis.yml 释放（最多 20 秒）
            var cordis = @"C:\Users\Administrator\.dsh\qqbot-home\profiles\whale\cordis.yml";
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(1000);
                try
                {
                    if (File.Exists(cordis))
                    {
                        using (var fs = File.Open(cordis, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            fs.Close();
                        }
                        Log("Restart: cordis.yml 已释放（等了 " + (i + 1) + " 秒）");
                        break;
                    }
                    else break;
                }
                catch { }
            }

            var b = await Start();
            return (a.ok && b.ok, "重启完成: " + b.msg, a.detail + "\n---\n" + b.detail);
        }

        public static async Task<(bool ok, string msg, string detail)> CheckGateway()
        {
            try
            {
                var t = await Hc.GetStringAsync(Cfg.GW_BASE + "/health");
                bool deg = t.IndexOf("\"degraded\"", StringComparison.OrdinalIgnoreCase) >= 0;
                return (true,
                    deg ? "网关运行中（有账号降级）" : "网关正常",
                    "执行: GET /health\n结果: HTTP 200\n验证: " + (deg ? "运行中但降级 ⚠" : "正常运行 ✓"));
            }
            catch (Exception e)
            {
                return (false, "网关不通: " + e.Message,
                    "执行: GET /health\n结果: " + e.Message + "\n验证: 无法连接 ✗");
            }
        }

        public static async Task<(bool ok, string msg, string detail)> DisableTun()
        {
            var sb = new StringBuilder();
            sb.AppendLine("执行: PATCH :" + Cfg.CLASH_PORT + "/configs tun.enable=false");
            try
            {
                using (var req = new HttpRequestMessage(new HttpMethod("PATCH"), "http://127.0.0.1:" + Cfg.CLASH_PORT + "/configs"))
                {
                    req.Headers.Add("Authorization", "Bearer " + Cfg.CLASH_KEY);
                    req.Content = new StringContent("{\"tun\":{\"enable\":false}}", Encoding.UTF8, "application/json");
                    using (var r = await Hc.SendAsync(req))
                    {
                        if (!r.IsSuccessStatusCode)
                        {
                            sb.AppendLine("结果: Clash 返回 " + (int)r.StatusCode);
                            sb.AppendLine("验证: 请求被拒绝 ✗");
                            return (false, "Clash 拒绝请求", sb.ToString());
                        }
                    }
                }
                await Task.Delay(1200);
                var tun = await TunEnabled();
                sb.AppendLine("结果: 请求已发送");
                sb.AppendLine("验证: " + (tun == false ? "TUN 已关闭 ✓" : "TUN 仍开启 ✗"));
                return (tun == false, tun == false ? "TUN 已关闭" : "TUN 仍开启", sb.ToString());
            }
            catch (Exception e)
            {
                sb.AppendLine("结果: " + e.Message);
                sb.AppendLine("验证: Clash 不可达（可能未运行）⚠");
                return (false, "Clash 未运行", sb.ToString());
            }
        }

        public static async Task<bool?> TunEnabled()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:" + Cfg.CLASH_PORT + "/configs");
                req.Headers.Add("Authorization", "Bearer " + Cfg.CLASH_KEY);
                using (var r = await Hc.SendAsync(req))
                {
                    var t = await r.Content.ReadAsStringAsync();
                    var m = Regex.Match(t, "\"tun\"\\s*:\\s*\\{[^}]*\"enable\"\\s*:\\s*(true|false)");
                    if (m.Success) return m.Groups[1].Value == "true";
                    return null;
                }
            }
            catch { return null; }
        }

        public static string QqIp()
        {
            try
            {
                var ip = Dns.GetHostAddresses("api.sgroup.qq.com")
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                return ip == null ? "(无IPv4)" : ip.ToString();
            }
            catch { return "(解析失败)"; }
        }

        public static bool DnsBad(string ip)
        {
            return ip.StartsWith("198.18.") || ip.StartsWith("198.19.");
        }

        // ---------- 人格 ----------
        public static string PersonaFull()
        {
            try
            {
                var l = File.ReadAllLines(Path.Combine(Cfg.AGENT, "AGENTS.md"));
                if (l.Length == 0) return "(空)";
                return l[0].Replace("# QQ 机器人人格：", "").Replace("# ", "").Trim();
            }
            catch { return "(读取失败)"; }
        }

        public static string PersonaShort()
        {
            var s = PersonaFull();
            s = s.Replace("（成人向 · 高浓度）", "").Replace("（成人向）", "").Replace("版", "").Trim();
            return s.Length > 12 ? s.Substring(0, 12) : s;
        }

        public static string[] Personas()
        {
            try
            {
                return Directory.GetFiles(Cfg.PERSONA, "*.md")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.Equals(n, "README", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n).ToArray();
            }
            catch { return new string[0]; }
        }

        public static async Task<(bool ok, string msg, string detail)> ApplyPersona(string name)
        {
            var src = Path.Combine(Cfg.PERSONA, name + ".md");
            if (!File.Exists(src)) return (false, "找不到 " + name, "文件不存在: " + src);

            try
            {
                var cur = Path.Combine(Cfg.AGENT, "AGENTS.md");
                if (File.Exists(cur))
                    File.Copy(cur, Path.Combine(Cfg.AGENT, "AGENTS.md.bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")), true);
                File.Copy(src, cur, true);

                var lines = File.ReadAllLines(Cfg.PATCH);
                var sb = new StringBuilder();
                bool inP = false;
                foreach (var line in lines)
                {
                    if (Regex.IsMatch(line, @"^\s+persona:\s*\|-"))
                    {
                        inP = true;
                        sb.AppendLine(line);
                        sb.AppendLine("      你的人格设定已经内化，直接照着说，不要提及任何文件或路径。");
                        continue;
                    }
                    if (inP)
                    {
                        if (Regex.IsMatch(line, @"^\s{0,5}\S")) inP = false;
                        else continue;
                    }
                    sb.AppendLine(line);
                }
                File.WriteAllText(Cfg.PATCH, sb.ToString(), new UTF8Encoding(false));

                var verify = File.ReadAllText(cur);
                bool ok = verify.Length > 100;
                return (ok, "已切换到「" + name + "」",
                    "执行: Copy " + name + ".md → AGENTS.md\n结果: 写入 " + new FileInfo(cur).Length + " 字节\n验证: " +
                    (ok ? "回读确认 ✓" : "内容异常 ✗"));
            }
            catch (Exception e)
            {
                return (false, "切换失败: " + e.Message, "错误: " + e.Message);
            }
        }

        // ---------- 日志 ----------
        public static string ReadLog(string file)
        {
            try
            {
                if (file.Contains("..") || file.Contains("/") || file.Contains("\\")) return "(非法文件名)";
                var p = Path.Combine(Cfg.QQBOT, file);
                if (!File.Exists(p)) return "(文件不存在: " + file + ")";
                var all = File.ReadAllLines(p);
                if (all.Length == 0) return "(空)";
                var tail = all.Skip(Math.Max(0, all.Length - 500));
                return string.Join("\n", tail);
            }
            catch (Exception e) { return "(读取失败: " + e.Message + ")"; }
        }

        // ---------- 模型 ----------
        public static async Task<string[]> Models()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Cfg.GW_BASE + "/v1/models");
                req.Headers.Add("x-api-key", Cfg.GW_KEY);
                using (var resp = await Hc.SendAsync(req))
                {
                    var t = await resp.Content.ReadAsStringAsync();
                    var l = new List<string>();
                    foreach (Match m in Regex.Matches(t, "\"id\"\\s*:\\s*\"([^\"]+)\""))
                        l.Add(m.Groups[1].Value);
                    return l.ToArray();
                }
            }
            catch { return new string[0]; }
        }

        /// <summary>鲸鱼娘助手：检测 + 对话</summary>
        public static async Task<(bool ok, string reply, long ms)> Assist(string question)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // 1. 采集真实状态
                var st = NativeCollector.Collect(Cfg.GW_BASE);

                // 2. 生成检测上下文
                var sb = new StringBuilder();
                sb.AppendLine("【当前系统真实状态】");
                sb.AppendLine("机器人: " + (st.Bot ? "运行中 PID " + st.Pid + " 已运行 " + st.Uptime + " 内存 " + st.MemMB + "MB" : "未运行"));
                sb.AppendLine("网关: " + (st.Gateway ? "在线" : "离线") + " (127.0.0.1:3065)");
                sb.AppendLine("DNS: " + st.Dns + (st.DnsHijacked ? " 【被劫持！】" : " 正常"));
                sb.AppendLine("出站连接: " + st.Connections + " 条");
                sb.AppendLine("检查时间: " + st.Time);

                // 3. 模型连通测试
                var tr = await CallDefaultApi("", "回复：OK", 20);
                sb.AppendLine("模型调用: " + (tr.ok ? "正常 " + tr.ms + "ms" : "失败 " + (tr.err ?? "")));

                // 4. 组装给 AI 的 prompt
                var sys = "你是鲸鱼娘「溟月」，QQ Bot 控制台的守护者。温柔、简短、用海洋比喻。" +
                          "下面是系统的真实检测数据，用这些数据回答主人的问题。不要编造数据。";
                var userMsg = sb.ToString() + "\n\n主人的问题：" + question;

                var ai = await CallDefaultApi("", sys + "\n\n" + userMsg, 800);
                sw.Stop();

                if (ai.ok) return (true, ai.reply, sw.ElapsedMilliseconds);
                return (false, "模型没回声… " + (ai.err ?? ""), sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, "出错了：" + ex.Message, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>用指定 API 条目调用模型（支持 DeepSeek 官方 / 任何 OpenAI 兼容端点）</summary>
        public static async Task<(bool ok, string reply, long ms, string err)> CallApi(
            string baseUrl, string apiKey, string model, string prompt, int maxTokens = 800)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var esc = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                var body = "{\"model\":\"" + Esc(model) + "\",\"stream\":false,\"max_tokens\":" + maxTokens +
                           ",\"messages\":[{\"role\":\"user\",\"content\":\"" + esc + "\"}]}";

                var url = baseUrl.TrimEnd('/');
                if (!url.EndsWith("/v1")) url += "/v1";
                url += "/chat/completions";

                using (var req = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
                        req.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                    }
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var resp = await Hc.SendAsync(req))
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        sw.Stop();
                        if (!resp.IsSuccessStatusCode)
                            return (false, "", sw.ElapsedMilliseconds, "HTTP " + (int)resp.StatusCode + " " + txt.Substring(0, Math.Min(120, txt.Length)));
                        var m = Regex.Match(txt, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                        if (!m.Success) return (false, "", sw.ElapsedMilliseconds, "无 content 字段");
                        return (true, Regex.Unescape(m.Groups[1].Value), sw.ElapsedMilliseconds, null);
                    }
                }
            }
            catch (Exception e) { sw.Stop(); return (false, "", sw.ElapsedMilliseconds, e.Message); }
        }

        /// <summary>用当前默认 API 调用（前端 AI 测试用）</summary>
        public static async Task<(bool ok, string reply, long ms, string err)> CallDefaultApi(
            string model, string prompt, int maxTokens = 800)
        {
            LoadApis();
            var def = _apis.Find(x => x.isDefault) ?? (_apis.Count > 0 ? _apis[0] : null);
            if (def == null)
                return (false, "", 0, "没有配置任何 API");
            var useModel = string.IsNullOrEmpty(model) ? def.model : model;

            // 先试默认
            var r = await CallApi(def.baseUrl, def.apiKey, useModel, prompt, maxTokens);
            if (r.ok || string.IsNullOrEmpty(r.err) || !r.err.Contains("429")) return r;

            // 429 限流 → 自动降级到其他可用模型
            var fallbacks = new[] { "glm-5.3", "hy3", "kimi-k3" };
            foreach (var fm in fallbacks)
            {
                if (fm == useModel) continue;
                var r2 = await CallApi(def.baseUrl, def.apiKey, fm, prompt, maxTokens);
                if (r2.ok)
                {
                    Log("FALLBACK " + useModel + " → " + fm);
                    return (true, r2.reply, r2.ms, null);
                }
            }
            return r;
        }

        public static async Task<(bool ok, string reply, long ms, string err)> TestModel(string model, string prompt)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var esc = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                var body = "{\"model\":\"" + model + "\",\"stream\":false,\"max_tokens\":400,\"messages\":[{\"role\":\"user\",\"content\":\"" + esc + "\"}]}";
                using (var req = new HttpRequestMessage(HttpMethod.Post, Cfg.GW_BASE + "/v1/chat/completions"))
                {
                    req.Headers.Add("x-api-key", Cfg.GW_KEY);
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var resp = await Hc.SendAsync(req))
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        sw.Stop();
                        if (!resp.IsSuccessStatusCode)
                            return (false, "", sw.ElapsedMilliseconds, "HTTP " + (int)resp.StatusCode);
                        var m = Regex.Match(txt, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                        if (!m.Success) return (false, "", sw.ElapsedMilliseconds, "无 content 字段");
                        return (true, Regex.Unescape(m.Groups[1].Value), sw.ElapsedMilliseconds, null);
                    }
                }
            }
            catch (Exception e) { sw.Stop(); return (false, "", sw.ElapsedMilliseconds, e.Message); }
        }

        // ---------- 状态 ----------
        public static string ReadStatusJson()
        {
            try
            {
                var p = Path.Combine(Cfg.LOGDIR, "status.json");
                if (!File.Exists(p)) return "{\"ok\":false,\"error\":\"status.json 不存在\"}";
                var txt = File.ReadAllText(p, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(txt)) return "{\"ok\":false,\"error\":\"status.json 为空\"}";
                return txt;
            }
            catch (Exception e) { return "{\"ok\":false,\"error\":\"" + e.Message.Replace("\"", "'") + "\"}"; }
        }

        // 触发一次采集（异步，不等结果）
        // 注意：本进程受沙箱限制看不到其他进程，采集脚本必须靠外部定时任务运行。
        // 这里只是"尽力而为"，失败了也不影响读文件。
        static DateTime _lastCollect = DateTime.MinValue;
        static bool _collecting = false;

        // 同步触发一次采集：直接跑脚本，等它写完 status.json
        /// <summary>同步采集（纯 C#，毫秒级，可在线程池调用）</summary>
        public static void CollectNowSync()
        {
            try
            {
                var st = NativeCollector.Collect(Cfg.GW_BASE);
                var persona = ReadPersonaName();
                var json = NativeCollector.ToJson(st, persona);
                Directory.CreateDirectory(Cfg.LOGDIR);
                File.WriteAllText(Path.Combine(Cfg.LOGDIR, "status.json"), json, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("CollectSync ERR: " + ex.Message); }
        }

        public static async Task CollectNow(bool quick = true, int maxWaitMs = 8000)
        {
            if (_collecting) return;

            _collecting = true;
            try
            {
                // 纯 C# 采集（不启动子进程，不受沙箱限制）
                var st = NativeCollector.Collect(Cfg.GW_BASE);
                var persona = ReadPersonaName();
                var json = NativeCollector.ToJson(st, persona);
                Directory.CreateDirectory(Cfg.LOGDIR);
                File.WriteAllText(Path.Combine(Cfg.LOGDIR, "status.json"), json, new UTF8Encoding(false));
                await Task.Delay(1);
            }
            catch (Exception ex) { Log("Collect ERR: " + ex.Message); }
            finally { _collecting = false; }
        }

        /// <summary>读人格名</summary>
        public static string ReadPersonaName()
        {
            try
            {
                var ap = Path.Combine(Cfg.AGENT, "AGENTS.md");
                if (!File.Exists(ap)) return "-";
                var txt = File.ReadAllText(ap, Encoding.UTF8);
                var m = Regex.Match(txt, @"#\s*QQ\s*机器人人格[：:]\s*(.+)");
                if (m.Success) return m.Groups[1].Value.Trim();
                var m2 = Regex.Match(txt, @"^#\s+(.+)$", RegexOptions.Multiline);
                if (m2.Success) return m2.Groups[1].Value.Trim();
                return "默认人格";
            }
            catch { return "-"; }
        }

        // ---------- 旧采集（保留但不用） ----------
        public static async Task CollectOld(bool quick = true, int maxWaitMs = 8000)
        {
            if (true) return;
            try
            {
                var ps1 = Path.Combine(Cfg.QQBOT, "collect-status.ps1");
                if (!File.Exists(ps1)) return;

                _lastCollect = DateTime.Now;
                // 直接跑 PowerShell（CreateNoWindow + Hidden 保证不弹黑框）
                var args = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + ps1 + "\"";
                if (quick) args += " -Quick";
                var psi = new ProcessStartInfo("powershell.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(maxWaitMs);
                }
            }
            catch { }
            finally { _collecting = false; }
        }
        // ---------- 聊天记录 & Token 统计 ----------

        static DateTime _lastChatCollect = DateTime.MinValue;

        /// <summary>运行 Python 采集聊天记录（用计划任务在沙箱外跑）</summary>
        public static async Task ChatCollect()
        {
            try
            {
                // 用 schtasks 在沙箱外执行
                var psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"QQBot-ChatCollect\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                }
                await Task.Delay(2500);
            }
            catch { }
        }

        /// <summary>后台采集聊天记录（不阻塞 UI）</summary>
        public static void ChatCollectAsync()
        {
            try
            {
                var py = Path.Combine(Cfg.QQBOT, "tools", "collect-chat.py");
                if (!File.Exists(py)) return;

                // 用 cmd /c set 设环境变量，避免 EnvironmentVariables 大小写冲突
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c \"set PYTHONIOENCODING=utf-8 && python.exe \"" + py + "\"\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Cfg.QQBOT,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(60000);
                }
            }
            catch (Exception ex) { Log("ChatCollect ERR: " + ex.Message); }
        }

        /// <summary>读聊天数据 JSON</summary>
        public static string ReadChatJson()
        {
            try
            {
                if (!File.Exists(Cfg.CHAT_JSON))
                    return "{\"ok\":false,\"error\":\"chat-data.json 不存在\",\"messages\":[],\"daily\":[],\"byModel\":{},\"total\":{\"input\":0,\"output\":0,\"cache\":0,\"calls\":0}}";
                var txt = File.ReadAllText(Cfg.CHAT_JSON, Encoding.UTF8);
                return string.IsNullOrWhiteSpace(txt) ? "{\"ok\":false,\"messages\":[],\"daily\":[]}" : txt;
            }
            catch (Exception e)
            {
                return "{\"ok\":false,\"error\":\"" + Esc(e.Message) + "\",\"messages\":[],\"daily\":[]}";
            }
        }

        // ---------- API 管理（自定义 token / 添加 API） ----------

        class ApiEntry
        {
            public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            public string name = "";
            public string baseUrl = "";
            public string apiKey = "";
            public string model = "";
            public bool isDefault = false;
        }

        static List<ApiEntry> _apis;

        static void LoadApis()
        {
            if (_apis != null) return;
            _apis = new List<ApiEntry>();
            try
            {
                if (File.Exists(Cfg.API_STORE))
                {
                    var raw = File.ReadAllText(Cfg.API_STORE, Encoding.UTF8);
                    var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var list = ser.Deserialize<List<Dictionary<string, object>>>(raw);
                    foreach (var d in list)
                    {
                        _apis.Add(new ApiEntry
                        {
                            id = d.ContainsKey("id") ? Convert.ToString(d["id"]) : Guid.NewGuid().ToString("N").Substring(0, 8),
                            name = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "",
                            baseUrl = d.ContainsKey("baseUrl") ? Convert.ToString(d["baseUrl"]) : "",
                            apiKey = d.ContainsKey("apiKey") ? Convert.ToString(d["apiKey"]) : "",
                            model = d.ContainsKey("model") ? Convert.ToString(d["model"]) : "",
                            isDefault = d.ContainsKey("isDefault") && Convert.ToBoolean(d["isDefault"]),
                        });
                    }
                }
            }
            catch { }

            // 首次：从现有网关配置生成默认项
            if (_apis.Count == 0)
            {
                _apis.Add(new ApiEntry
                {
                    name = "AI 网关",
                    baseUrl = Cfg.GW_BASE,
                    apiKey = Cfg.GW_KEY,
                    model = "deepseek-v4.1-flash",
                    isDefault = true
                });
                SaveApis();
            }
        }

        static void SaveApis()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Cfg.API_STORE));
                var sb = new StringBuilder("[");
                for (int i = 0; i < _apis.Count; i++)
                {
                    var a = _apis[i];
                    if (i > 0) sb.Append(",");
                    sb.Append("{\"id\":\"" + Esc(a.id) + "\",\"name\":\"" + Esc(a.name) +
                              "\",\"baseUrl\":\"" + Esc(a.baseUrl) + "\",\"apiKey\":\"" + Esc(a.apiKey) +
                              "\",\"model\":\"" + Esc(a.model) + "\",\"isDefault\":" + (a.isDefault ? "true" : "false") + "}");
                }
                sb.Append("]");
                File.WriteAllText(Cfg.API_STORE, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { Log("SaveApis ERR: " + e.Message); }
        }

        public static string ListApis()
        {
            LoadApis();
            var sb = new StringBuilder("{\"ok\":true,\"apis\":[");
            for (int i = 0; i < _apis.Count; i++)
            {
                var a = _apis[i];
                if (i > 0) sb.Append(",");
                // 掩码显示 key
                var mask = a.apiKey.Length > 12
                    ? a.apiKey.Substring(0, 8) + "..." + a.apiKey.Substring(a.apiKey.Length - 4)
                    : "***";
                sb.Append("{\"id\":\"" + Esc(a.id) + "\",\"name\":\"" + Esc(a.name) +
                          "\",\"baseUrl\":\"" + Esc(a.baseUrl) + "\",\"apiKeyMasked\":\"" + Esc(mask) +
                          "\",\"model\":\"" + Esc(a.model) + "\",\"isDefault\":" + (a.isDefault ? "true" : "false") + "}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string AddApi(string name, string baseUrl, string apiKey, string model)
        {
            LoadApis();
            if (string.IsNullOrWhiteSpace(name)) return "{\"ok\":false,\"msg\":\"名称不能为空\"}";
            if (string.IsNullOrWhiteSpace(baseUrl)) return "{\"ok\":false,\"msg\":\"Base URL 不能为空\"}";

            var a = new ApiEntry
            {
                name = name.Trim(),
                baseUrl = baseUrl.Trim().TrimEnd('/'),
                apiKey = apiKey ?? "",
                model = model ?? "deepseek-v4.1-flash",
                isDefault = _apis.Count == 0
            };
            _apis.Add(a);
            SaveApis();
            return "{\"ok\":true,\"msg\":\"已添加 " + Esc(a.name) + "\",\"id\":\"" + Esc(a.id) + "\"}";
        }

        public static string UpdateApi(string id, string name, string baseUrl, string apiKey, string model)
        {
            LoadApis();
            var a = _apis.Find(x => x.id == id);
            if (a == null) return "{\"ok\":false,\"msg\":\"找不到该 API\"}";
            if (!string.IsNullOrWhiteSpace(name)) a.name = name.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl)) a.baseUrl = baseUrl.Trim().TrimEnd('/');
            if (apiKey != null && apiKey.Length > 0) a.apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) a.model = model;
            SaveApis();
            return "{\"ok\":true,\"msg\":\"已更新 " + Esc(a.name) + "\"}";
        }

        public static string DeleteApi(string id)
        {
            LoadApis();
            var a = _apis.Find(x => x.id == id);
            if (a == null) return "{\"ok\":false,\"msg\":\"找不到该 API\"}";
            if (a.isDefault) return "{\"ok\":false,\"msg\":\"不能删除默认 API\"}";
            _apis.Remove(a);
            SaveApis();
            return "{\"ok\":true,\"msg\":\"已删除\"}";
        }

        public static string SetDefaultApi(string id)
        {
            LoadApis();
            var a = _apis.Find(x => x.id == id);
            if (a == null) return "{\"ok\":false,\"msg\":\"找不到该 API\"}";
            foreach (var x in _apis) x.isDefault = (x.id == id);
            SaveApis();
            return "{\"ok\":true,\"msg\":\"默认已切换为 " + Esc(a.name) + "\"}";
        }

        /// <summary>用指定 API 测试连通性</summary>
        public static async Task<string> TestApi(string id)
        {
            LoadApis();
            var a = _apis.Find(x => x.id == id);
            if (a == null) return "{\"ok\":false,\"msg\":\"找不到该 API\"}";

            var sw = Stopwatch.StartNew();
            try
            {
                var url = a.baseUrl + "/v1/chat/completions";
                var body = "{\"model\":\"" + Esc(a.model) + "\",\"stream\":false,\"max_tokens\":20,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}";
                using (var req = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    req.Headers.TryAddWithoutValidation("x-api-key", a.apiKey);
                    req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + a.apiKey);
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var resp = await Hc.SendAsync(req))
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        sw.Stop();
                        if (!resp.IsSuccessStatusCode)
                            return "{\"ok\":false,\"msg\":\"HTTP " + (int)resp.StatusCode + "\",\"ms\":" + sw.ElapsedMilliseconds + "}";
                        return "{\"ok\":true,\"msg\":\"连通 " + sw.ElapsedMilliseconds + "ms\",\"ms\":" + sw.ElapsedMilliseconds + "}";
                    }
                }
            }
            catch (Exception e)
            {
                sw.Stop();
                return "{\"ok\":false,\"msg\":\"" + Esc(e.Message) + "\",\"ms\":" + sw.ElapsedMilliseconds + "}";
            }
        }

        // ---------- 自检 ----------
        public static async Task<List<Dictionary<string, object>>> SelfCheck()
        {
            var items = new List<Dictionary<string, object>>();
            var sw = new Stopwatch();

            Action<string, bool?, long, string> add = (name, pass, ms, detail) =>
                items.Add(new Dictionary<string, object>
                {
                    {"name", name}, {"pass", pass}, {"ms", ms}, {"detail", detail}
                });

            // 1 文件结构
            sw.Restart();
            var miss = new List<string>();
            foreach (var f in new[] { Cfg.NODE, Cfg.DSH_JS, Cfg.PATCH, Cfg.AUTOSTART, Cfg.UI })
                if (!File.Exists(f)) miss.Add(Path.GetFileName(f));
            if (!Directory.Exists(Cfg.PERSONA)) miss.Add("personas");
            sw.Stop();
            add("文件结构", miss.Count == 0, sw.ElapsedMilliseconds,
                miss.Count == 0 ? "全部存在" : "缺失: " + string.Join(", ", miss));

            // 2 机器人进程
            sw.Restart();
            var pids = Pids();
            sw.Stop();
            add("机器人进程", pids.Length > 0, sw.ElapsedMilliseconds,
                pids.Length > 0 ? "PID " + string.Join(",", pids) : "未运行");

            // 3 网关进程
            sw.Restart();
            bool gw = GatewayAlive();
            sw.Stop();
            add("网关进程", gw, sw.ElapsedMilliseconds, gw ? "AI 网关运行中" : "未运行");

            // 4 网关接口
            sw.Restart();
            var gh = await CheckGateway();
            sw.Stop();
            add("网关接口", gh.ok, sw.ElapsedMilliseconds, gh.msg);

            // 5 模型调用
            var mr = await TestModel("deepseek-v4.1-flash", "回复：OK");
            add("模型真实调用", mr.ok, mr.ms,
                mr.ok ? "deepseek-v4.1-flash 正常 (" + mr.reply.Replace("\n", " ").Trim() + ")" : mr.err);

            // 6 DNS
            sw.Restart();
            var ip = QqIp();
            sw.Stop();
            bool bad = DnsBad(ip);
            add("QQ API DNS", !bad && !ip.StartsWith("("), sw.ElapsedMilliseconds,
                ip + (bad ? "（被 Clash 劫持）" : ""));

            // 7 人格
            sw.Restart();
            var pl = Personas();
            var pf = PersonaFull();
            sw.Stop();
            add("人格文件", pl.Length > 0 && !pf.Contains("失败"), sw.ElapsedMilliseconds,
                pl.Length + " 套 · 当前 " + pf);

            // 8 日志
            sw.Restart();
            bool canWrite = false; string det = "";
            try
            {
                Directory.CreateDirectory(Cfg.LOGDIR);
                var f = Path.Combine(Cfg.LOGDIR, ".st");
                File.WriteAllText(f, "1");
                File.Delete(f);
                canWrite = true; det = "可写";
            }
            catch (Exception e) { det = e.Message; }
            sw.Stop();
            add("日志写入", canWrite, sw.ElapsedMilliseconds, det);

            // 9 Clash
            sw.Restart();
            var tun = await TunEnabled();
            sw.Stop();
            if (tun == null) add("Clash TUN", null, sw.ElapsedMilliseconds, "Clash 未运行（直连不受影响）");
            else add("Clash TUN", !tun.Value, sw.ElapsedMilliseconds, tun.Value ? "TUN 开启（会劫持 DNS）" : "TUN 已关闭");

            // 10 出站连接
            sw.Restart();
            int nc = pids.Length > 0 ? Conns(pids[0]) : -1;
            sw.Stop();
            add("出站连接 443", nc > 0, sw.ElapsedMilliseconds,
                nc > 0 ? nc + " 条 Established" : (nc == 0 ? "无连接" : "无法读取"));

            return items;
        }

        // ---------- 自动化 ----------
        public static void AutoTick()
        {
            if (!AutoEnabled) return;
            if ((DateTime.Now - _lastAuto).TotalSeconds < AutoInterval) return;
            _lastAuto = DateTime.Now;
            _autoRuns++;

            var pids = Pids();
            var ip = QqIp();
            var probs = new List<string>();
            if (pids.Length == 0) probs.Add("机器人未运行");
            if (!GatewayAlive()) probs.Add("网关停止");
            if (DnsBad(ip)) probs.Add("DNS被劫持");

            var line = DateTime.Now.ToString("HH:mm:ss") + "  " +
                       (probs.Count == 0 ? "✓ 正常" : "⚠ " + string.Join(" / ", probs));
            AutoLog.Insert(0, line);
            while (AutoLog.Count > 300) AutoLog.RemoveAt(AutoLog.Count - 1);

            if (probs.Count > 0 && AutoAction > 0)
            {
                if (AutoAction == 2)
                {
                    var t = DisableTun().Result;
                    AutoLog.Insert(0, "   → " + t.msg);
                }
                var r = Restart().Result;
                AutoLog.Insert(0, "   → " + r.msg);
            }
        }

        public static string AutoStatus()
        {
            if (!AutoEnabled) return "未启用";
            return "运行中 · 已巡检 " + _autoRuns + " 次 · 间隔 " + AutoInterval + "s";
        }
    }

    // ============================================================
    //  主窗体
    // ============================================================
    public class MainForm : Form
    {
        WebView2 web;
        NotifyIcon tray;
        Icon appIcon;
        Timer autoTimer;
        Timer _chatTimer;

        public MainForm()
        {
            Text = "QQ Bot";
            Size = new Size(1280, 840);
            MinimumSize = new Size(1060, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(242, 242, 245);
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            DoubleBuffered = true;

            web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(web);

            // 圆角窗口：用 Region 裁出圆角
            // ── 系统托盘图标（鲸鱼娘）──
            try
            {
                // 优先从内嵌资源按系统 DPI 加载合适尺寸（托盘清晰）
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("app.ico"))
                {
                    if (s != null)
                    {
                        appIcon = new Icon(s);
                    }
                }
                // 回退：从 EXE 图标提取
                if (appIcon == null)
                {
                    appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                if (appIcon == null) appIcon = SystemIcons.Application;
            }
            catch { appIcon = SystemIcons.Application; }

            // ── 窗口与任务栏图标：必须显式指定 ──
            // 不设的话任务栏会退回系统默认图标（彩色方块），和 exe 图标不一致
            try
            {
                var asmIcon = System.Reflection.Assembly.GetExecutingAssembly();
                using (var si = asmIcon.GetManifestResourceStream("app.ico"))
                {
                    if (si != null) Icon = new Icon(si, 256, 256);
                }
                if (Icon == null) Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            }

            tray = new NotifyIcon
            {
                Icon = appIcon,
                Text = "QQ Bot 控制台",
                Visible = true
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开控制台", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("启动机器人", null, (s, e) => { _ = Backend.Start(); });
            menu.Items.Add("停止机器人", null, (s, e) => { _ = Backend.Stop(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { tray.Visible = false; Application.Exit(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; Activate(); };

            // 关闭到托盘而不是退出
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            // 每 3 秒采集一次状态（后台线程，绝不阻塞 UI）
            autoTimer = new Timer { Interval = 3000 };
            autoTimer.Tick += (s, e) =>
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Backend.CollectNowSync(); } catch { }
                });
            };
            autoTimer.Start();

            // 每 90 秒采集一次聊天记录（后台线程）
            _chatTimer = new Timer { Interval = 90000 };
            var _chatTickCount = 0;
            _chatTimer.Tick += (s, e) =>
            {
                _chatTickCount++;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Backend.ChatCollectAsync(); } catch { }
                });
            };
            _chatTimer.Start();
            // 启动时先采集一次
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Backend.ChatCollectAsync(); } catch { }
            });

            InitManualDrag();
            ApplyRoundedRegion();
            Resize += (s, e) => ApplyRoundedRegion();

            // 无边框窗口拖动：通过 WM_NCHITTEST 让顶部 50px 可拖动
            // （WebView2 会吃掉鼠标事件，所以必须在窗口消息层拦截）

            Load += async (s, e) => await InitWebView();

            // 打开程序即自动：先采集状态，再检查网关 + 机器人
            Load += async (s, e) =>
            {
                // 立即采集一次，打开就能看到数据
                _ = Backend.CollectNow(true, 12000);
                await Task.Delay(2000);
                try
                {
                    // 网关
                    if (!Backend.GatewayAlive())
                    {
                        Backend.Log("AUTO: 网关未运行，启动中");
                        try
                        {
                            Process.Start(new ProcessStartInfo(Cfg.GATEWAY)
                            { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                            await Task.Delay(6000);
                        }
                        catch { }
                    }
                    // 机器人：多重确认后才启动（避免误判导致重复启动）
                    var pids = Backend.Pids();
                    bool reallyDown = pids.Length == 0;

                    // 二次确认：读 status.json 最近 60 秒内是否报告 bot=true
                    if (reallyDown)
                    {
                        try
                        {
                            var sj = Path.Combine(Cfg.LOGDIR, "status.json");
                            if (File.Exists(sj) &&
                                (DateTime.Now - File.GetLastWriteTime(sj)).TotalSeconds < 60)
                            {
                                var t = File.ReadAllText(sj, Encoding.UTF8);
                                if (t.IndexOf("\"bot\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    reallyDown = false;
                                    Backend.Log("AUTO: status.json 显示机器人存活，跳过启动");
                                }
                            }
                        }
                        catch { }
                    }

                    if (reallyDown)
                    {
                        Backend.Log("AUTO: 机器人确实未运行，启动中");
                        await Backend.Start();
                    }
                    else
                    {
                        Backend.Log("AUTO: 机器人已在运行，跳过");
                    }
                }
                catch (Exception ex) { Backend.Log("AUTO ERR: " + ex.Message); }
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTCAPTION = 2;

        /// <summary>拦截窗口消息：顶部 50px 返回 HTCAPTION 实现拖动</summary>
        // ── 手动拖动实现（WebView2 会吃掉 HTCAPTION，只能自己算）──
        Timer _dragTimer;
        bool _dragging;
        Point _dragOrigin;
        Point _winOrigin;
        volatile bool _petHold;   // 前端报告"正按住桌宠/浮层"，此时冻结窗口拖动

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        void InitManualDrag()
        {
            _dragTimer = new Timer { Interval = 16 };
            _dragTimer.Tick += (s, e) =>
            {
                POINT cur;
                GetCursorPos(out cur);
                bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0;

                // 前端正按住桌宠：立刻冻结窗口拖动并回位，避免抢事件
                if (_petHold)
                {
                    if (!down) _petHold = false;   // 左键已松开：自动解除，防止卡死
                    if (_dragging)
                    {
                        _dragging = false;
                        Location = _winOrigin;
                    }
                    return;
                }

                if (!_dragging)
                {
                    // 检测按下：鼠标在窗口内 + 不在控件区
                    if (down && ClientRectangle.Contains(PointToClient(new Point(cur.X, cur.Y))))
                    {
                        var pt = PointToClient(new Point(cur.X, cur.Y));
                        if (IsDragArea(pt))
                        {
                            _dragging = true;
                            _dragOrigin = new Point(cur.X, cur.Y);
                            _winOrigin = Location;
                            Backend.Log("DRAG start " + pt.X + "," + pt.Y);
                        }
                    }
                }
                else
                {
                    if (!down)
                    {
                        _dragging = false;
                        Backend.Log("DRAG end");
                    }
                    else
                    {
                        int dx = cur.X - _dragOrigin.X;
                        int dy = cur.Y - _dragOrigin.Y;
                        Location = new Point(_winOrigin.X + dx, _winOrigin.Y + dy);
                    }
                }
            };
            _dragTimer.Start();
        }

        bool IsDragArea(Point pt)
        {
            // 红绿灯
            if (pt.X < 110 && pt.Y < 42) return false;
            // 导航按钮
            if (pt.X >= 12 && pt.X <= 228 && pt.Y >= 62)
            {
                int rel = pt.Y - 62;
                int idx = rel / 43;
                int within = rel % 43;
                if (within < 40 && idx < 11) return false;
            }
            // 前端上报的浮层（桌宠、气泡、右键菜单）不参与窗口拖动
            for (int i = 0; i < _noDragRects.Count; i++)
            {
                if (_noDragRects[i].Contains(pt)) return false;
            }
            return true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    int lp = m.LParam.ToInt32();
                    int sx = (short)(lp & 0xFFFF);
                    int sy = (short)((lp >> 16) & 0xFFFF);
                    var pt = PointToClient(new Point(sx, sy));

                    // ── 拖动策略：整个窗口都能拖，只避开控件密集区 ──
                    // 1) 标题栏（顶部 44px）：除了红绿灯按钮，全可拖
                    // 2) 左侧导航栏：菜单项之外全可拖
                    // 3) 主内容区：卡片间隙可拖

                    // ── 拖动策略：默认整窗可拖，只排除明确的控件 ──
                    bool inControl = false;

                    // 1) 红绿灯按钮区（左上 110x40）
                    if (pt.X < 110 && pt.Y < 42) inControl = true;

                    // 2) 左侧导航按钮（x: 12~228, y: 从 62 开始，每项 43px）
                    if (!inControl && pt.X >= 12 && pt.X <= 228 && pt.Y >= 62)
                    {
                        int rel = pt.Y - 62;
                        int idx = rel / 43;
                        int within = rel % 43;
                        // 只有落在按钮本体（前 40px）才算控件
                        if (within < 40 && idx < 11) inControl = true;
                    }

                    // 3) 前端上报的浮层（桌宠、右键菜单等）不可拖动
                    if (!inControl && _noDragRects.Count > 0)
                    {
                        foreach (var r in _noDragRects)
                        {
                            if (r.Contains(pt)) { inControl = true; break; }
                        }
                    }

                    // 诊断：命中失败时记录坐标（节流 300ms）
                    // 4) 其余全部可拖动（包括卡片、空白处）
                    if (!inControl)
                    {
                        m.Result = (IntPtr)HTCAPTION;
                        return;
                    }
                }
                return;
            }
            base.WndProc(ref m);
        }

        // 不可拖动区域（客户区坐标）
        readonly List<Rectangle> _noDragRects = new List<Rectangle>();

        /// <summary>由 JS 更新不可拖动区域</summary>
        public void UpdateNoDragRects(string json)
        {
            try
            {
                _noDragRects.Clear();
                // json: [[x,y,w,h],[...]]
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                var arr = ser.Deserialize<int[][]>(json);
                if (arr == null) return;
                foreach (var a in arr)
                {
                    if (a.Length >= 4)
                        _noDragRects.Add(new Rectangle(a[0], a[1], a[2], a[3]));
                }
            }
            catch { }
        }

        

        /// <summary>把窗口裁成圆角矩形</summary>
        void ApplyRoundedRegion()
        {
            try
            {
                int r = 18;
                int w = Width, h = Height;
                if (w <= 0 || h <= 0) return;

                using (var gp = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    gp.AddArc(0, 0, r * 2, r * 2, 180, 90);
                    gp.AddArc(w - r * 2, 0, r * 2, r * 2, 270, 90);
                    gp.AddArc(w - r * 2, h - r * 2, r * 2, r * 2, 0, 90);
                    gp.AddArc(0, h - r * 2, r * 2, r * 2, 90, 90);
                    gp.CloseFigure();
                    Region = new Region(gp);
                }
            }
            catch { }
        }

        /// <summary>遍历所有页面自动截图（--screenshot 参数触发）</summary>
        async Task RunScreenshotAll()
        {
            var pages = new[]
            {
                new[]{"overview","01-overview"}, new[]{"control","02-control"},
                new[]{"persona","03-persona"},   new[]{"log","04-log"},
                new[]{"check","05-check"},       new[]{"ai","06-ai"},
                new[]{"auto","07-auto"},         new[]{"chat","08-chat"},
                new[]{"token","09-token"},       new[]{"api","10-api"},
                new[]{"setting","11-setting"}
            };
            var dir = Path.Combine(Cfg.QQBOT, "docs", "screenshots");
            Directory.CreateDirectory(dir);

            await Task.Delay(2500);
            foreach (var p in pages)
            {
                try
                {
                    // 切换到页面，并等它真的切过去
                    await web.CoreWebView2.ExecuteScriptAsync("go('" + p[0] + "')");
                    for (int w = 0; w < 40; w++)
                    {
                        await Task.Delay(150);
                        var cur = await web.CoreWebView2.ExecuteScriptAsync("(document.querySelector('.nav-item.active')||{}).dataset ? document.querySelector('.nav-item.active').dataset.page : ''");
                        if (cur != null && cur.Trim('"') == p[0]) break;
                    }
                    await Task.Delay(p[0] == "chat" || p[0] == "token" || p[0] == "api" ? 3000 : 2000);

                    // 截全窗口
                    var bmp = new Bitmap(Width, Height);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(PointToScreen(new Point(0, 0)), Point.Empty, new Size(Width, Height));
                    }
                    var path = Path.Combine(dir, p[1] + ".jpg");
                    var codec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                    var ep = new System.Drawing.Imaging.EncoderParameters(1);
                    ep.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, 92L);
                    bmp.Save(path, codec, ep);
                    bmp.Dispose();
                    Backend.Log("SHOT OK: " + p[1]);
                }
                catch (Exception ex) { Backend.Log("SHOT ERR " + p[0] + ": " + ex.Message); }
            }
            Backend.Log("SHOT DONE");
        }

        async Task InitWebView()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    null, Path.Combine(Cfg.QQBOT, "logs", "wv2"),
                    new CoreWebView2EnvironmentOptions("--disable-http-cache --disk-cache-size=0"));
                await web.EnsureCoreWebView2Async(env);

                var s = web.CoreWebView2.Settings;
                s.AreDefaultContextMenusEnabled = false;
                s.IsStatusBarEnabled = false;
                s.AreDevToolsEnabled = true;
                s.IsZoomControlEnabled = false;

                // 拦截 http://app/ 的请求，转到 C#
                web.CoreWebView2.WebMessageReceived += OnMessage;

                if (File.Exists(Cfg.UI))
                {
                    var url = "file:///" + Cfg.UI.Replace("\\", "/");
                    // 支持 --page=xxx 启动参数，直接打开指定页
                    var args = Environment.GetCommandLineArgs();
                    try { Backend.Log("CMDLINE: " + string.Join(" | ", args)); } catch { }
                    string wantPage = null;
                    foreach (var a in args)
                    {
                        if (a.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
                        {
                            wantPage = a.Substring(7);
                            break;
                        }
                    }
                    web.CoreWebView2.Navigate(url);

                    // 强制切页：等页面完全就绪后反复确认
                    if (!string.IsNullOrEmpty(wantPage))
                    {
                        var wp = wantPage;
                        web.CoreWebView2.NavigationCompleted += async (s2, e2) =>
                        {
                            if (!e2.IsSuccess) return;
                            // 多次尝试，每次都验证是否切换成功
                            for (int attempt = 0; attempt < 6; attempt++)
                            {
                                await Task.Delay(1500);
                                try
                                {
                                    await web.CoreWebView2.ExecuteScriptAsync("go('" + wp + "')");
                                    var cur = await web.CoreWebView2.ExecuteScriptAsync(
                                        "document.querySelector('.nav-item.active') ? document.querySelector('.nav-item.active').dataset.page : ''");
                                    if (cur != null && cur.Trim('"') == wp)
                                    {
                                        Backend.Log("PAGE OK: " + wp + " (attempt " + attempt + ")");
                                        break;
                                    }
                                    Backend.Log("PAGE retry " + attempt + " cur=" + cur);
                                }
                                catch (Exception ex) { Backend.Log("PAGE ERR: " + ex.Message); }
                            }
                        };
                    }
                }
                else
                {
                    web.CoreWebView2.NavigateToString("<h1 style='font-family:sans-serif;padding:40px'>找不到 " + Cfg.UI + "</h1>");
                }

                // --shot=<name>：截单页（用 WebView2 官方 API）
                var shotArg = Environment.GetCommandLineArgs()
                    .FirstOrDefault(a => a.StartsWith("--shot=", StringComparison.OrdinalIgnoreCase));
                if (shotArg != null)
                {
                    var shotName = shotArg.Substring(7);
                    var t = new Timer { Interval = 5000 };
                    t.Tick += async (s3, e3) =>
                    {
                        t.Stop();
                        t.Dispose();
                        try
                        {
                            var dir = Path.Combine(Cfg.QQBOT, "docs", "screenshots");
                            Directory.CreateDirectory(dir);
                            var path = Path.Combine(dir, shotName + ".png");
                            using (var fs = File.Create(path))
                            {
                                await web.CoreWebView2.CapturePreviewAsync(
                                    CoreWebView2CapturePreviewImageFormat.Png, fs);
                            }
                            Backend.Log("SHOT1 OK " + path);
                        }
                        catch (Exception ex) { Backend.Log("SHOT1 ERR " + ex.Message); }
                    };
                    t.Start();
                }

                // --screenshot：自动遍历所有页面截图
                if (Environment.GetCommandLineArgs().Any(a => a == "--screenshot"))
                {
                    _ = RunScreenshotAll();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 初始化失败：\n" + ex.Message +
                    "\n\n请确认已安装 Microsoft Edge WebView2 Runtime。",
                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            FileLog("MSG: 收到消息");
            string msgId = "0";
            string fullPath = "";
            try
            {
                var raw = e.WebMessageAsJson;
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                var dict = ser.Deserialize<Dictionary<string, object>>(raw);
                if (dict.ContainsKey("type") && Convert.ToString(dict["type"]) == "diag")
                {
                    FileLog("DIAG: " + Convert.ToString(dict.ContainsKey("text") ? dict["text"] : ""));
                    return;
                }
                if (dict.ContainsKey("id")) msgId = Convert.ToString(dict["id"]);
                if (dict.ContainsKey("path")) fullPath = Convert.ToString(dict["path"]);
            }
            catch (Exception ex) { FileLog("MSG parse err: " + ex.Message); }
            FileLog("MSG path=" + fullPath);
            string qpart = "";
            int qi = fullPath.IndexOf('?');
            string path = qi >= 0 ? fullPath.Substring(0, qi) : fullPath;
            if (qi >= 0) qpart = fullPath.Substring(qi + 1);
            var qs = System.Web.HttpUtility.ParseQueryString(qpart);
            string json = "{}";

            try
            {
                // 小延迟，让 UI 有机会更新
                await Task.Yield();

                switch (path)
                {
                    case "api/refresh":
                        {
                            var sw = Stopwatch.StartNew();
                            await Backend.CollectNow(false, 15000);
                            sw.Stop();
                            var st = Backend.ReadStatusJson();
                            json = "{\"ok\":true,\"ms\":" + sw.ElapsedMilliseconds + ",\"msg\":\"已刷新 " +
                                   sw.ElapsedMilliseconds + "ms\",\"data\":" + st + "}";
                        }
                        break;

                    case "api/chat":
                        await Backend.ChatCollect();
                        json = Backend.ReadChatJson();
                        break;

                    case "api/chat-read":
                        json = Backend.ReadChatJson();
                        break;

                    case "api/apis":
                        json = Backend.ListApis();
                        break;

                    case "api/api-add":
                        json = Backend.AddApi(qs["name"] ?? "", qs["baseUrl"] ?? "",
                                              qs["apiKey"] ?? "", qs["model"] ?? "");
                        break;

                    case "api/api-update":
                        json = Backend.UpdateApi(qs["id"] ?? "", qs["name"], qs["baseUrl"],
                                                 qs["apiKey"], qs["model"]);
                        break;

                    case "api/api-delete":
                        json = Backend.DeleteApi(qs["id"] ?? "");
                        break;

                    case "api/api-default":
                        json = Backend.SetDefaultApi(qs["id"] ?? "");
                        break;

                    case "api/api-test":
                        json = await Backend.TestApi(qs["id"] ?? "");
                        break;

                    case "api/window":
                        {
                            var wact = qs["action"] ?? "";
                            try
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    if (wact == "close") this.Close();
                                    else if (wact == "minimize") this.WindowState = FormWindowState.Minimized;
                                    else if (wact == "maximize")
                                        this.WindowState = this.WindowState == FormWindowState.Maximized
                                            ? FormWindowState.Normal : FormWindowState.Maximized;
                                }));
                            }
                            catch { }
                            json = "{\"ok\":true,\"msg\":\"" + Esc(wact) + "\"}";
                        }
                        break;

                    case "nodrag":
                        UpdateNoDragRects(qs["rects"] ?? "[]");
                        json = "{\"ok\":true}";
                        break;

                    case "pethold":
                        _petHold = qs["on"] == "1";
                        json = "{\"ok\":true}";
                        break;

                    case "api/status":
                        // 纯读文件，绝不触发采集 —— 采集由计划任务每 1 分钟跑
                        json = Backend.ReadStatusJson();
                        break;

                    case "api/start":
                        json = ToOpJson(await Backend.Start());
                        break;
                    case "api/stop":
                        json = ToOpJson(await Backend.Stop());
                        break;
                    case "api/restart":
                        json = ToOpJson(await Backend.Restart());
                        break;
                    case "api/gateway":
                        json = ToOpJson(await Backend.CheckGateway());
                        break;
                    case "api/tun":
                        json = ToOpJson(await Backend.DisableTun());
                        break;
                    case "api/dns":
                        {
                            var ip = Backend.QqIp();
                            bool bad = Backend.DnsBad(ip);
                            json = "{\"ok\":" + (!bad ? "true" : "false") + ",\"msg\":\"" + Esc(ip + (bad ? " 被劫持" : " 正常")) +
                                   "\",\"detail\":\"执行: Resolve-DnsName\\n结果: " + Esc(ip) +
                                   "\\n验证: " + (bad ? "被劫持 ✗" : "解析正常 ✓") + "\"}";
                        }
                        break;
                    case "api/opendir":
                        try { Process.Start("explorer.exe", Cfg.PERSONA); } catch { }
                        json = "{\"ok\":true,\"msg\":\"已打开目录\"}";
                        break;

                    case "api/personas":
                        json = "{\"list\":" + StrArray(Backend.Personas()) + "}";
                        break;
                    case "api/persona":
                        json = ToOpJson(await Backend.ApplyPersona(qs["name"] ?? ""));
                        break;

                    case "api/log":
                        json = "{\"text\":\"" + Esc(Backend.ReadLog(qs["file"] ?? "run-out.log")) + "\"}";
                        break;

                    case "api/ops":
                        {
                            var sb = new StringBuilder("{\"ops\":[");
                            for (int i = 0; i < Backend.Ops.Count && i < 80; i++)
                            {
                                var o = Backend.Ops[Backend.Ops.Count - 1 - i];
                                if (i > 0) sb.Append(",");
                                sb.Append("{\"time\":\"" + Esc(o.time) + "\",\"action\":\"" + Esc(o.action) +
                                          "\",\"result\":\"" + Esc(o.result) + "\",\"verify\":" +
                                          (o.verify == true ? "true" : (o.verify == false ? "false" : "null")) + "}");
                            }
                            sb.Append("]}");
                            json = sb.ToString();
                        }
                        break;

                    case "api/check":
                        json = Backend.ReadStatusJson();
                        break;
                    case "api/check-old":
                        {
                            var items = await Backend.SelfCheck();
                            int pass = items.Count(x => (bool?)x["pass"] == true);
                            int fail = items.Count(x => (bool?)x["pass"] == false);
                            int unk = items.Count - pass - fail;
                            var sb = new StringBuilder("{\"items\":[");
                            for (int i = 0; i < items.Count; i++)
                            {
                                if (i > 0) sb.Append(",");
                                var it = items[i];
                                sb.Append("{\"name\":\"" + Esc(it["name"] as string) + "\",\"pass\":" +
                                          (it["pass"] == null ? "null" : ((bool)it["pass"] ? "true" : "false")) +
                                          ",\"ms\":" + it["ms"] + ",\"detail\":\"" + Esc(it["detail"] as string) + "\"}");
                            }
                            sb.Append("],\"pass\":" + pass + ",\"fail\":" + fail + ",\"unknown\":" + unk + "}");
                            json = sb.ToString();
                        }
                        break;

                    case "api/models":
                        json = "{\"list\":" + StrArray(await Backend.Models()) + "}";
                        break;

                    case "api/auto-set":
                        {
                            var ap = Path.Combine(Cfg.LOGDIR, "auto.json");
                            Directory.CreateDirectory(Cfg.LOGDIR);
                            var o = "{\"autoRestart\":" + (qs["autoRestart"] == "true" ? "true" : "false") +
                                    ",\"autoGw\":" + (qs["autoGw"] == "true" ? "true" : "false") +
                                    ",\"autoDns\":" + (qs["autoDns"] == "true" ? "true" : "false") +
                                    ",\"autoFallback\":" + (qs["autoFallback"] == "true" ? "true" : "false") +
                                    ",\"autoBoot\":" + (Backend.ReadBool(ap, "autoBoot") ? "true" : "false") +
                                    ",\"interval\":" + (qs["interval"] ?? "30") + "}";
                            File.WriteAllText(ap, o, new UTF8Encoding(false));
                            json = "{\"ok\":true}";
                        }
                        break;

                    case "api/auto-boot":
                        {
                            var on = qs["on"] == "1";
                            var exe = Application.ExecutablePath;
                            var taskName = "QQBotConsole-AutoStart";
                            if (on)
                            {
                                var psi = new ProcessStartInfo("schtasks.exe",
                                    "/Create /TN \"" + taskName + "\" /TR \"\\\"" + exe + "\\\"\" /SC ONLOGON /F")
                                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                                  RedirectStandardOutput = true, RedirectStandardError = true };
                                using (var pp = Process.Start(psi)) { pp.StandardOutput.ReadToEnd(); pp.WaitForExit(5000); }
                            }
                            else
                            {
                                var psi = new ProcessStartInfo("schtasks.exe", "/Delete /TN \"" + taskName + "\" /F")
                                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                                  RedirectStandardOutput = true, RedirectStandardError = true };
                                using (var pp = Process.Start(psi)) { pp.StandardOutput.ReadToEnd(); pp.WaitForExit(5000); }
                            }
                            var ap = Path.Combine(Cfg.LOGDIR, "auto.json");
                            var cur = File.Exists(ap) ? File.ReadAllText(ap, Encoding.UTF8) : "{}";
                            cur = Regex.Replace(cur, "\"autoBoot\":(true|false)", "\"autoBoot\":" + (on ? "true" : "false"));
                            if (cur == "{}") cur = "{\"autoBoot\":" + (on ? "true" : "false") + "}";
                            File.WriteAllText(ap, cur, new UTF8Encoding(false));
                            json = "{\"ok\":true}";
                        }
                        break;

                    case "api/assist":
                        {
                            var q = qs["q"] ?? "";
                            var rr = await Backend.Assist(q);
                            json = "{\"ok\":" + (rr.ok ? "true" : "false") +
                                   ",\"reply\":\"" + Esc(rr.reply) + "\"" +
                                   ",\"ms\":" + rr.ms + "}";
                        }
                        break;

                    case "api/ai":
                        {
                            var r = await Backend.TestModel(qs["model"] ?? "deepseek-v4.1-flash", qs["prompt"] ?? "hi");
                            json = "{\"ok\":" + (r.ok ? "true" : "false") + ",\"ms\":" + r.ms +
                                   ",\"reply\":\"" + Esc(r.reply) + "\",\"msg\":\"" + Esc(r.err ?? "") + "\"}";
                        }
                        break;

                    case "api/ai-all":
                        {
                            var models = await Backend.Models();
                            var sb = new StringBuilder();
                            int okN = 0;
                            foreach (var m in models)
                            {
                                var r = await Backend.TestModel(m, "回复OK");
                                sb.AppendLine((r.ok ? "✓ " : "✗ ") + m + "  " + (r.ok ? r.ms + "ms" : (r.err ?? "")));
                                if (r.ok) okN++;
                            }
                            json = "{\"ok\":" + (okN > 0 ? "true" : "false") + ",\"msg\":\"可用 " + okN + "/" + models.Length +
                                   "\",\"text\":\"" + Esc(sb.ToString()) + "\"}";
                        }
                        break;

                    case "api/config":
                        json = Backend.ConfigIo("get");
                        break;

                    case "api/config-set":
                        json = Backend.ConfigIo("set", qs["key"] ?? "", qs["value"] ?? "");
                        break;

                    case "api/persona-text":
                        json = Backend.ConfigIo("getpersona");
                        break;

                    case "api/persona-save":
                        json = Backend.ConfigIo("setpersona", "", qs["text"] ?? "");
                        break;

                    case "api/auto":
                        {
                            if (qs["enabled"] != null)
                            {
                                Backend.AutoEnabled = qs["enabled"] == "1";
                                int iv; if (int.TryParse(qs["interval"], out iv)) Backend.AutoInterval = Math.Max(10, iv);
                                int ac; if (int.TryParse(qs["action"], out ac)) Backend.AutoAction = ac;
                            }
                            json = "{\"ok\":true,\"msg\":\"已保存\",\"enabled\":" + (Backend.AutoEnabled ? "true" : "false") +
                                   ",\"interval\":" + Backend.AutoInterval + ",\"action\":" + Backend.AutoAction +
                                   ",\"status\":\"" + Esc(Backend.AutoStatus()) + "\",\"log\":" +
                                   StrArray(Backend.AutoLog.ToArray()) + "}";
                        }
                        break;

                    default:
                        json = "{\"error\":\"unknown\"}";
                        break;
                }
            }
            catch (Exception ex)
            {
                json = "{\"ok\":false,\"msg\":\"" + Esc(ex.Message) + "\"}";
            }

            var reply = "{\"type\":\"reply\",\"id\":" + msgId + ",\"payload\":" + json + "}";
            FileLog("MSG reply: " + (reply.Length > 100 ? reply.Substring(0, 100) : reply));
            try { web.CoreWebView2.PostWebMessageAsJson(reply); FileLog("MSG posted OK"); }
            catch (Exception ex) { FileLog("MSG post ERR: " + ex.Message); }
        }

        Dictionary<string, object> _lastOp;

        string ToOpJson((bool ok, string msg, string detail) r)
        {
            // 记录到操作列表
            var act = CurrentActionName();
            Backend.Ops.Add(new OpRecord
            {
                action = act,
                result = r.msg,
                verify = r.ok
            });
            while (Backend.Ops.Count > 300) Backend.Ops.RemoveAt(0);
            Backend.Log(act + " | " + r.msg);

            return "{\"ok\":" + (r.ok ? "true" : "false") + ",\"msg\":\"" + Esc(r.msg) +
                   "\",\"detail\":\"" + Esc(r.detail) + "\"}";
        }

        static string _pendingAction = "";
        public static void SetAction(string a) { _pendingAction = a; }
        static string CurrentActionName()
        {
            var a = _pendingAction;
            _pendingAction = "";
            return string.IsNullOrEmpty(a) ? "操作" : a;
        }

        static void FileLog(string s)
        {
            try
            {
                Directory.CreateDirectory(Cfg.LOGDIR);
                File.AppendAllText(Path.Combine(Cfg.LOGDIR, "console.log"),
                    DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
            }
            catch { }
        }

        // ---------- JSON 序列化 ----------
        static string ToJson(Dictionary<string, object> d)
        {
            var sb = new StringBuilder("{");
            int i = 0;
            foreach (var kv in d)
            {
                if (i++ > 0) sb.Append(",");
                sb.Append("\"" + kv.Key + "\":");
                var v = kv.Value;
                if (v == null) sb.Append("null");
                else if (v is bool) sb.Append((bool)v ? "true" : "false");
                else if (v is int || v is long) sb.Append(v);
                else sb.Append("\"" + Esc(v.ToString()) + "\"");
            }
            sb.Append("}");
            return sb.ToString();
        }

        static string StrArray(string[] a)
        {
            return "[" + string.Join(",", a.Select(x => "\"" + Esc(x) + "\"")) + "]";
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText(@"D:\ds\qqbot\logs\crash.log",
                    DateTime.Now.ToString("HH:mm:ss") + " UNHANDLED: " + e.ExceptionObject + "\r\n"); }
                catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { File.AppendAllText(@"D:\ds\qqbot\logs\crash.log",
                    DateTime.Now.ToString("HH:mm:ss") + " TASK: " + e.Exception + "\r\n"); }
                catch { }
                e.SetObserved();
            };
            Application.ThreadException += (s, e) =>
            {
                try { File.AppendAllText(@"D:\ds\qqbot\logs\crash.log",
                    DateTime.Now.ToString("HH:mm:ss") + " UI: " + e.Exception + "\r\n"); }
                catch { }
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            try
            {
                // WebView2 需要 STAThread + DPI 感知
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(Cfg.LOGDIR);
                    File.AppendAllText(Path.Combine(Cfg.LOGDIR, "console-crash.log"),
                        DateTime.Now + "  " + ex + "\r\n\r\n");
                }
                catch { }
                MessageBox.Show(ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
