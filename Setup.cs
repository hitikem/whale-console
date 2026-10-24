using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleConsoleSetup
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupWizard());
        }
    }

    class SetupWizard : Form
    {
        const string APP = "Whale Console";
        const string VER = "1.0.0";
        const string PUB = "hitikem";

        static readonly Color C_BG = Color.White;
        static readonly Color C_SIDE = Color.FromArgb(248, 248, 250);
        static readonly Color C_TEXT = Color.FromArgb(29, 29, 31);
        static readonly Color C_MUTED = Color.FromArgb(110, 110, 115);
        static readonly Color C_LINE = Color.FromArgb(224, 224, 228);
        static readonly Color C_BLUE = Color.FromArgb(0, 113, 227);
        static readonly Color C_GREEN = Color.FromArgb(52, 199, 89);

        int _step = 0;
        Panel _content, _side;
        Button _btnNext, _btnBack, _btnCancel, _browseBtn;
        string _installPath;

        Label _lblStepTitle, _lblStepSub, _lblStatus, _lblWelcomeDesc, _pathLabel, _pathInfo, _finishLbl, _finishTip;
        TextBox _txtPath;
        ProgressBar _progress;
        RichTextBox _txtLog, _txtLicense;
        CheckBox _chkRun, _chkDesktop, _chkAgree;

        public SetupWizard()
        {
            Text = APP + " " + VER + " 安装程序";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = C_BG;
            Font = new Font("Microsoft YaHei UI", 9F);
            _installPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WhaleConsole");

            BuildUI();
            ShowStep(0);
        }

        void BuildUI()
        {
            _side = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = C_SIDE };
            _side.Paint += SidePaint;
            Controls.Add(_side);

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Color.FromArgb(250, 250, 252)
            };
            bottom.Paint += (s, e) =>
            {
                using (var p = new Pen(C_LINE)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
            };
            Controls.Add(bottom);

            _btnCancel = MakeBtn("取消", 96);
            _btnCancel.Click += (s, e) =>
            {
                if (_step == 3) return;
                if (MessageBox.Show("确定要退出安装吗？", APP,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Close();
            };
            _btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCancel.Location = new Point(bottom.Width - 110, 17);
            bottom.Controls.Add(_btnCancel);

            _btnNext = MakeBtn("下一步 >", 108);
            _btnNext.BackColor = C_BLUE;
            _btnNext.ForeColor = Color.White;
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            _btnNext.Click += BtnNext_Click;
            _btnNext.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnNext.Location = new Point(bottom.Width - 228, 17);
            bottom.Controls.Add(_btnNext);

            _btnBack = MakeBtn("< 上一步", 100);
            _btnBack.Click += (s, e) => ShowStep(_step - 1);
            _btnBack.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnBack.Location = new Point(bottom.Width - 334, 17);
            bottom.Controls.Add(_btnBack);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(32, 26, 32, 10)
            };
            Controls.Add(_content);
            _content.BringToFront();

            _lblStepTitle = new Label
            {
                Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
                ForeColor = C_TEXT,
                Location = new Point(32, 26),
                AutoSize = true
            };
            _content.Controls.Add(_lblStepTitle);

            _lblStepSub = new Label
            {
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = C_MUTED,
                Location = new Point(34, 62),
                AutoSize = true
            };
            _content.Controls.Add(_lblStepSub);

            _lblWelcomeDesc = new Label
            {
                Location = new Point(36, 108),
                Size = new Size(430, 270),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = Color.FromArgb(66, 66, 69)
            };
            _content.Controls.Add(_lblWelcomeDesc);

            _txtLicense = new RichTextBox
            {
                Location = new Point(36, 100),
                Size = new Size(440, 235),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 250, 252),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            _content.Controls.Add(_txtLicense);

            _chkAgree = new CheckBox
            {
                Text = "我已阅读并同意上述条款",
                Location = new Point(36, 348),
                AutoSize = true
            };
            _chkAgree.CheckedChanged += (s, e) => _btnNext.Enabled = _chkAgree.Checked;
            _content.Controls.Add(_chkAgree);

            _pathLabel = new Label
            {
                Text = "将 Whale Console 安装到：",
                Location = new Point(36, 156),
                AutoSize = true,
                ForeColor = Color.FromArgb(66, 66, 69)
            };
            _content.Controls.Add(_pathLabel);

            _txtPath = new TextBox
            {
                Location = new Point(36, 190),
                Width = 380
            };
            _content.Controls.Add(_txtPath);

            _browseBtn = MakeBtn("浏览...", 78);
            _browseBtn.Location = new Point(424, 188);
            _browseBtn.Height = 26;
            _browseBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "选择安装位置" })
                    if (dlg.ShowDialog() == DialogResult.OK) _txtPath.Text = dlg.SelectedPath;
            };
            _content.Controls.Add(_browseBtn);

            _pathInfo = new Label
            {
                Text = "所需空间：约 3 MB\r\n卸载：运行安装目录下的 uninstall.ps1",
                Location = new Point(36, 240),
                Size = new Size(440, 60),
                ForeColor = C_MUTED,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _content.Controls.Add(_pathInfo);

            _chkDesktop = new CheckBox
            {
                Text = "创建桌面快捷方式",
                Location = new Point(36, 310),
                AutoSize = true,
                Checked = true
            };
            _content.Controls.Add(_chkDesktop);

            _txtLog = new RichTextBox
            {
                Location = new Point(36, 120),
                Size = new Size(440, 185),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(250, 250, 252),
                Font = new Font("Consolas", 8.5F),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            _content.Controls.Add(_txtLog);

            _progress = new ProgressBar
            {
                Location = new Point(36, 320),
                Width = 440,
                Height = 8,
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };
            _content.Controls.Add(_progress);

            _lblStatus = new Label
            {
                Location = new Point(36, 336),
                AutoSize = true,
                ForeColor = C_MUTED
            };
            _content.Controls.Add(_lblStatus);

            _finishLbl = new Label
            {
                Location = new Point(36, 108),
                Size = new Size(440, 60),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = Color.FromArgb(66, 66, 69)
            };
            _content.Controls.Add(_finishLbl);

            _chkRun = new CheckBox
            {
                Text = "立即启动 Whale Console",
                Location = new Point(36, 190),
                AutoSize = true,
                Checked = true
            };
            _content.Controls.Add(_chkRun);

            _finishTip = new Label
            {
                Location = new Point(36, 240),
                Size = new Size(440, 120),
                ForeColor = C_MUTED,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _content.Controls.Add(_finishTip);
        }

        Button MakeBtn(string text, int w)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 243),
                ForeColor = C_TEXT
            };
            b.FlatAppearance.BorderColor = C_LINE;
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        void SidePaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var st = asm.GetManifestResourceStream("app.ico");
                if (st != null)
                {
                    using (var ico = new Icon(st, 56, 56))
                        g.DrawIcon(ico, new Rectangle(24, 40, 56, 56));
                }
            }
            catch { }

            using (var f = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold))
                g.DrawString(APP, f, new SolidBrush(C_TEXT), 24, 108);

            using (var f = new Font("Microsoft YaHei UI", 8.5F))
                g.DrawString("版本 " + VER, f, new SolidBrush(C_MUTED), 26, 134);

            string[] steps = { "欢迎", "许可协议", "选择位置", "正在安装", "完成" };
            int y = 190;
            for (int i = 0; i < steps.Length; i++)
            {
                bool active = i == _step;
                bool done = i < _step;

                var dotColor = done ? C_GREEN : (active ? C_BLUE : Color.FromArgb(200, 200, 205));
                using (var br = new SolidBrush(dotColor))
                    g.FillEllipse(br, 26, y + 4, 9, 9);

                if (done)
                {
                    using (var p = new Pen(Color.White, 1.6f))
                    {
                        g.DrawLine(p, 28.5f, y + 8.5f, 30f, y + 10.5f);
                        g.DrawLine(p, 30f, y + 10.5f, 33f, y + 6.5f);
                    }
                }

                using (var f = new Font("Microsoft YaHei UI", 9F, active ? FontStyle.Bold : FontStyle.Regular))
                using (var br = new SolidBrush(active ? C_TEXT : C_MUTED))
                    g.DrawString(steps[i], f, br, 44, y);

                y += 32;
            }

            using (var f = new Font("Microsoft YaHei UI", 7.5F))
                g.DrawString("(c) 2026 " + PUB + "\r\nMIT License", f,
                    new SolidBrush(Color.FromArgb(160, 160, 165)), 24, Height - 70);
        }

        void ShowStep(int step)
        {
            if (step < 0 || step > 4) return;
            _step = step;

            _lblWelcomeDesc.Visible = false;
            _txtLicense.Visible = false;
            _chkAgree.Visible = false;
            _pathLabel.Visible = false;
            _txtPath.Visible = false;
            _browseBtn.Visible = false;
            _pathInfo.Visible = false;
            _chkDesktop.Visible = false;
            _txtLog.Visible = false;
            _progress.Visible = false;
            _lblStatus.Visible = false;
            _finishLbl.Visible = false;
            _chkRun.Visible = false;
            _finishTip.Visible = false;

            switch (step)
            {
                case 0:
                    _lblWelcomeDesc.Text =
                        "Whale Console 是一个开源的 QQ 机器人桌面控制台。\r\n\r\n" +
                        "它可以帮助你：\r\n" +
                        "   -  一键启动 / 停止机器人\r\n" +
                        "   -  实时监控进程状态\r\n" +
                        "   -  自动检测网关和 DNS\r\n" +
                        "   -  管理多个 AI 厂商\r\n" +
                        "   -  与鲸鱼娘助手对话\r\n\r\n" +
                        "本程序需要 .NET Framework 4.8 和 WebView2 Runtime。\r\n" +
                        "安装程序会自动检查这些依赖。";
                    _lblWelcomeDesc.Visible = true;
                    break;

                case 1:
                    if (_txtLicense.Text.Length == 0) _txtLicense.Text = LICENSE_TEXT;
                    _txtLicense.Visible = true;
                    _chkAgree.Visible = true;
                    _chkAgree.Checked = false;
                    break;

                case 2:
                    _txtPath.Text = _installPath;
                    _pathLabel.Visible = true;
                    _txtPath.Visible = true;
                    _browseBtn.Visible = true;
                    _pathInfo.Visible = true;
                    _chkDesktop.Visible = true;
                    break;

                case 3:
                    _progress.Value = 0;
                    _lblStatus.Text = "准备安装...";
                    _txtLog.Clear();
                    _progress.Visible = true;
                    _lblStatus.Visible = true;
                    _txtLog.Visible = true;
                    _installPath = _txtPath.Text.Trim();
                    _ = DoInstall();
                    break;

                case 4:
                    _finishLbl.Text = "Whale Console 已安装到：\r\n" + _installPath;
                    _finishLbl.Visible = true;
                    _chkRun.Visible = true;
                    _finishTip.Text = "下一步：\r\n" +
                                      "   1. 打开控制台\r\n" +
                                      "   2. 到「API 管理」填写你的 API Key\r\n" +
                                      "   3. 到「设置」调整参数\r\n" +
                                      "   4. 点「启动」";
                    _finishTip.Visible = true;
                    break;
            }

            _btnBack.Visible = step >= 1 && step != 3 && step != 4;
            _btnNext.Visible = step != 3;
            _btnCancel.Visible = step != 4;
            _btnNext.Text = step == 0 ? "下一步 >" : (step == 1 ? "我同意 >" : (step == 2 ? "安装" : "完成"));
            _btnNext.Enabled = step != 1 || _chkAgree.Checked;

            _lblStepTitle.Text = new[] { "欢迎使用 " + APP, "许可协议", "选择安装位置", "正在安装", "安装完成" }[step];
            _lblStepSub.Text = new[]
            {
                "安装程序将引导你完成设置",
                "请阅读以下条款",
                "选择程序要安装到的文件夹",
                "请稍候",
                "感谢使用"
            }[step];

            _side.Invalidate();
        }

        void BtnNext_Click(object sender, EventArgs e)
        {
            switch (_step)
            {
                case 0:
                case 1:
                case 2:
                    ShowStep(_step + 1);
                    break;
                case 4:
                    if (_chkRun.Checked)
                    {
                        try { Process.Start(Path.Combine(_installPath, "QQBotConsole.exe")); }
                        catch { }
                    }
                    Close();
                    break;
            }
        }

        async Task DoInstall()
        {
            try
            {
                UpdateProg(5, "检查运行环境...");
                Log("[1/4] 检查运行环境");
                await Task.Delay(250);

                bool netOk = false;
                try
                {
                    var rel = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
                        "Release", null);
                    netOk = rel != null && Convert.ToInt32(rel) >= 528040;
                }
                catch { }
                Log(netOk ? "      .NET Framework 4.8         OK" : "      .NET Framework 4.8         缺失");

                try
                {
                    var pv = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                        "pv", null);
                    Log(pv != null ? "      WebView2 Runtime            OK" : "      WebView2 Runtime            缺失");
                }
                catch { Log("      WebView2 Runtime            检查失败"); }

                await Task.Delay(350);

                UpdateProg(20, "创建目录...");
                Log("");
                Log("[2/4] 创建目录");
                Directory.CreateDirectory(_installPath);
                Directory.CreateDirectory(Path.Combine(_installPath, "logs"));
                Directory.CreateDirectory(Path.Combine(_installPath, "logs", "wv2"));
                Log("      " + _installPath);
                await Task.Delay(250);

                UpdateProg(35, "解压文件...");
                Log("");
                Log("[3/4] 解压文件");

                var asm = Assembly.GetExecutingAssembly();
                var zipName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("payload.zip"));
                if (zipName == null) throw new Exception("安装包损坏（找不到 payload）");

                using (var stream = asm.GetManifestResourceStream(zipName))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    int total = zip.Entries.Count, done = 0;
                    foreach (var entry in zip.Entries)
                    {
                        var dest = Path.Combine(_installPath, entry.FullName.Replace('/', '\\'));
                        if (string.IsNullOrEmpty(entry.Name))
                            Directory.CreateDirectory(dest);
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));
                            entry.ExtractToFile(dest, true);
                        }
                        done++;
                        UpdateProg(35 + (int)(done * 45.0 / total), "解压 " + done + "/" + total);
                        if (done % 5 == 0) Application.DoEvents();
                    }
                    Log("      共 " + total + " 个文件");
                }
                await Task.Delay(350);

                UpdateProg(85, "创建快捷方式...");
                Log("");
                Log("[4/4] 创建快捷方式");

                var exe = Path.Combine(_installPath, "QQBotConsole.exe");

                if (_chkDesktop.Checked)
                {
                    try
                    {
                        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        CreateShortcut(Path.Combine(desktop, APP + ".lnk"), exe, _installPath);
                        Log("      桌面快捷方式     OK");
                    }
                    catch (Exception ex) { Log("      桌面快捷方式     失败：" + ex.Message); }
                }

                try
                {
                    var sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), APP);
                    Directory.CreateDirectory(sm);
                    CreateShortcut(Path.Combine(sm, APP + ".lnk"), exe, _installPath);
                    Log("      开始菜单         OK");
                }
                catch (Exception ex) { Log("      开始菜单         失败：" + ex.Message); }

                try
                {
                    var un = Path.Combine(_installPath, "uninstall.ps1");
                    File.WriteAllText(un, UNINSTALL_SCRIPT.Replace("__PATH__", _installPath));
                    Log("      卸载程序         OK");
                }
                catch { }

                await Task.Delay(350);
                UpdateProg(100, "安装完成");
                Log("");
                Log("安装成功！");
                await Task.Delay(600);
                ShowStep(4);
            }
            catch (Exception ex)
            {
                Log("");
                Log("!! 安装失败：" + ex.Message);
                UpdateProg(0, "安装失败");
                MessageBox.Show("安装失败：\n\n" + ex.Message, APP,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnNext.Enabled = false;
            }
        }

        void CreateShortcut(string lnk, string target, string workDir)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic sh = Activator.CreateInstance(t);
            dynamic sc = sh.CreateShortcut(lnk);
            sc.TargetPath = target;
            sc.IconLocation = target + ",0";
            sc.WorkingDirectory = workDir;
            sc.Description = APP;
            sc.Save();
        }

        void UpdateProg(int val, string status)
        {
            _progress.Value = Math.Min(100, Math.Max(0, val));
            _lblStatus.Text = status;
        }

        void Log(string s)
        {
            _txtLog.AppendText(s + "\n");
            _txtLog.ScrollToCaret();
        }

        const string LICENSE_TEXT =
@"MIT License

Copyright (c) 2026 hitikem

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

--------------------------------------------

使用须知：

1. 本软件按现状提供，不提供任何担保。
2. 使用本软件的风险由使用者自行承担。
3. 请遵守腾讯 QQ 服务条款及相关法律法规。
4. 禁止用于发送垃圾信息、传播违法内容等行为。
5. 机器人的人格和回复内容由使用者自行配置并负责。

完整免责声明请见 README.md。";

        const string UNINSTALL_SCRIPT = @"
$root = '__PATH__'
Write-Host '正在卸载 Whale Console...'
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Whale Console.lnk') -Force -EA SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Whale Console') -Recurse -Force -EA SilentlyContinue
schtasks /Delete /TN 'WhaleConsole-AutoStart' /F 2>&1 | Out-Null
Get-Process QQBotConsole -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Start-Sleep -Seconds 2
Set-Location $env:TEMP
Remove-Item $root -Recurse -Force -EA SilentlyContinue
Write-Host '卸载完成'
Read-Host '按回车退出'
";
    }
}
