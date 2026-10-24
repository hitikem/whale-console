using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQBotConsole
{
    /// <summary>
    /// 纯 C# 状态采集器 —— 不启动任何子进程，不受沙箱限制
    /// </summary>
    public static class NativeCollector
    {
        public class Status
        {
            public bool Bot;
            public uint Pid;
            public string Uptime = "-";
            public double Cpu;
            public long MemMB;
            public long MemTotalMB;
            public double MemPct;
            public int Threads;
            public bool Gateway;
            public string Persona = "-";
            public string Dns = "-";
            public bool DnsHijacked;
            public int Connections;
            public string Time = "";
        }

        /// <summary>查找 QQ Bot 的 node 进程（不依赖 WMI）</summary>
        public static Process FindBot()
        {
            foreach (var p in Process.GetProcessesByName("node"))
            {
                try
                {
                    var sn = p.ProcessName;
                    if (sn != "node") continue;
                    // 用 MainModule 判断 + 启动时间
                    var mt = p.StartTime;
                    // 检查进程是否存活且运行超过 1 秒
                    if ((DateTime.Now - mt).TotalSeconds < 1) continue;
                    return p;
                }
                catch { }
            }
            return null;
        }

        /// <summary>采集完整状态</summary>
        public static Status Collect(string gatewayBase)
        {
            var s = new Status { Time = DateTime.Now.ToString("HH:mm:ss") };

            // ── 机器人进程 ──
            try
            {
                uint botPid = 0;

                // 方案A：从 autostart.log 读最后一次启动的 PID 并验证存活
                try
                {
                    var logPath = @"D:\ds\qqbot\autostart.log";
                    if (File.Exists(logPath))
                    {
                        var lines = File.ReadAllLines(logPath, Encoding.UTF8);
                        for (int i = lines.Length - 1; i >= 0; i--)
                        {
                            var m = Regex.Match(lines[i], @"qqbot launched, pid (\d+)");
                            if (!m.Success) continue;
                            uint tryPid = uint.Parse(m.Groups[1].Value);
                            try
                            {
                                var tp = Process.GetProcessById((int)tryPid);
                                if (!tp.HasExited) { botPid = tryPid; }
                            }
                            catch { }
                            break;
                        }
                    }
                }
                catch (Exception ex) { }

                // 方案B：WMI（沙箱内可能返回空集）
                if (botPid == 0)
                {
                    try
                    {
                        using (var searcher = new System.Management.ManagementObjectSearcher(
                            "SELECT ProcessId, CommandLine FROM Win32_Process"))
                        {
                            foreach (System.Management.ManagementObject mo in searcher.Get())
                            {
                                var cl = Convert.ToString(mo["CommandLine"]) ?? "";
                                if (cl.IndexOf("qqbot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cl.IndexOf("patch.yml", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    botPid = Convert.ToUInt32(mo["ProcessId"]);
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { }
                }

                // 方案C：进程特征（内存 > 80MB 且线程 >= 8）
                if (botPid == 0)
                {
                    try
                    {
                        foreach (var pp in Process.GetProcessesByName("node"))
                        {
                            try
                            {
                                if (pp.WorkingSet64 > 80L * 1024 * 1024 && pp.Threads.Count >= 8)
                                {
                                    botPid = (uint)pp.Id;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (botPid > 0)
                {
                    var p = Process.GetProcessById((int)botPid);
                    s.Pid = botPid;
                    s.Bot = true;
                    s.Threads = p.Threads.Count;
                    s.MemMB = p.WorkingSet64 / 1024 / 1024;

                    var span = DateTime.Now - p.StartTime;
                    s.Uptime = span.TotalHours >= 1
                        ? string.Format("{0:F0}h {1:D2}m", span.TotalHours, span.Minutes)
                        : string.Format("{0:F0}m {1:D2}s", span.TotalMinutes, span.Seconds);

                    try
                    {
                        p.Refresh();
                        var cpuMs = p.TotalProcessorTime.TotalMilliseconds;
                        var wallMs = span.TotalMilliseconds;
                        if (wallMs > 0)
                            s.Cpu = Math.Round(cpuMs / wallMs * 100.0 / Environment.ProcessorCount, 1);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { }

            // ── 系统内存 ──
            try
            {
                using (var mc = new System.Management.ManagementClass("Win32_ComputerSystem"))
                using (var moc = mc.GetInstances())
                {
                    foreach (System.Management.ManagementObject mo in moc)
                    {
                        s.MemTotalMB = Convert.ToInt64(mo["TotalPhysicalMemory"]) / 1024 / 1024;
                        break;
                    }
                }
            }
            catch
            {
                s.MemTotalMB = 0;
            }
            if (s.MemTotalMB > 0)
                s.MemPct = Math.Round((double)s.MemMB / s.MemTotalMB * 100.0, 1);

            // ── 网关 ──
            try
            {
                using (var c = new System.Net.Sockets.TcpClient())
                {
                    var t = c.ConnectAsync("127.0.0.1", 3065);
                    s.Gateway = t.Wait(1200) && c.Connected;
                }
            }
            catch { s.Gateway = false; }

            // ── 出站连接 ──
            try
            {
                s.Connections = System.Net.NetworkInformation.IPGlobalProperties
                    .GetIPGlobalProperties().GetActiveTcpConnections().Length;
            }
            catch { }

            // ── DNS ──
            try
            {
                var ips = System.Net.Dns.GetHostAddresses("api.sgroup.qq.com");
                if (ips.Length > 0)
                {
                    s.Dns = ips[0].ToString();
                    s.DnsHijacked = s.Dns.StartsWith("198.18.") || s.Dns.StartsWith("198.19.");
                }
            }
            catch { }

            return s;
        }

        /// <summary>转成前端用的 JSON</summary>
        public static string ToJson(Status s, string persona)
        {
            var sb = new StringBuilder();
            sb.Append("{\"ok\":true");
            sb.Append(",\"time\":\"" + s.Time + "\"");
            sb.Append(",\"bot\":" + (s.Bot ? "true" : "false"));
            sb.Append(",\"pid\":\"" + (s.Bot ? s.Pid.ToString() : "-") + "\"");
            sb.Append(",\"uptime\":\"" + s.Uptime + "\"");
            sb.Append(",\"cpu\":" + s.Cpu.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"mem\":" + s.MemMB.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"memTotal\":" + s.MemTotalMB);
            sb.Append(",\"memPct\":" + s.MemPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"threads\":" + s.Threads);
            sb.Append(",\"msgCount\":0");
            sb.Append(",\"gateway\":\"" + (s.Gateway ? "正常" : "离线") + "\"");
            sb.Append(",\"persona\":\"" + Esc(persona) + "\"");
            sb.Append(",\"personaFull\":\"" + Esc(persona) + "\"");
            sb.Append(",\"dns\":\"" + Esc(s.Dns) + "\"");
            sb.Append(",\"dnsHijacked\":" + (s.DnsHijacked ? "true" : "false"));
            sb.Append(",\"conn\":" + s.Connections);
            sb.Append(",\"tun\":0");
            sb.Append(",\"checks\":[]}");
            return sb.ToString();
        }

        
        static string Esc(string v)
        {
            if (v == null) return "";
            return v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
