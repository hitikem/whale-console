using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace WhaleConsoleSetup
{
    /// <summary>
    /// Whale Console 安装程序
    /// 单文件 EXE，内嵌所有文件，自解压安装
    /// </summary>
    static class Program
    {
        const string APP_NAME = "Whale Console";
        const string VERSION = "1.0.0";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    class SetupForm : Form
    {
        const string APP_NAME = "Whale Console";
        const string VERSION = "1.0.0";

        TextBox txtPath;
        Button btnBrowse, btnInstall, btnCancel;
        ProgressBar progress;
        Label lblStatus, lblTitle, lblSub;
        RichTextBox txtLog;

        public SetupForm()
        {
            Text = APP_NAME + " 安装程序";
            Size = new Size(620, 480);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(245, 245, 247);
            Font = new Font("Microsoft YaHei UI", 9F);

            // 标题
            lblTitle = new Label
            {
                Text = APP_NAME,
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 29, 31),
                Location = new Point(32, 28),
                AutoSize = true
            };
            Controls.Add(lblTitle);

            lblSub = new Label
            {
                Text = "QQ 机器人控制台  ·  v" + VERSION,
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.FromArgb(110, 110, 115),
                Location = new Point(35, 68),
                AutoSize = true
            };
            Controls.Add(lblSub);

            // 安装路径
            var lblPath = new Label
            {
                Text = "安装到：",
                Location = new Point(35, 115),
                AutoSize = true,
                ForeColor = Color.FromArgb(66, 66, 69)
            };
            Controls.Add(lblPath);

            txtPath = new TextBox
            {
                Location = new Point(110, 112),
                Width = 370,
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WhaleConsole")
            };
            Controls.Add(txtPath);

            btnBrowse = new Button
            {
                Text = "浏览…",
                Location = new Point(490, 110),
                Width = 80,
                Height = 26
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择安装目录";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtPath.Text = dlg.SelectedPath;
                }
            };
            Controls.Add(btnBrowse);

            // 日志区
            txtLog = new RichTextBox
            {
                Location = new Point(35, 155),
                Size = new Size(535, 190),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(txtLog);

            // 进度条
            progress = new ProgressBar
            {
                Location = new Point(35, 358),
                Size = new Size(535, 8),
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };
            Controls.Add(progress);

            // 状态
            lblStatus = new Label
            {
                Text = "准备安装",
                Location = new Point(35, 375),
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 110, 115)
            };
            Controls.Add(lblStatus);

            // 按钮
            btnInstall = new Button
            {
                Text = "开始安装",
                Location = new Point(370, 400),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(0, 113, 227),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;
            Controls.Add(btnInstall);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(480, 400),
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => Close();
            Controls.Add(btnCancel);

            Log("欢迎使用 " + APP_NAME + " 安装程序");
            Log("");
            Log("点击「开始安装」继续");
        }

        void Log(string msg)
        {
            txtLog.AppendText(msg + "\n");
            txtLog.ScrollToCaret();
        }

        async void BtnInstall_Click(object sender, EventArgs e)
        {
            var target = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("请选择安装目录", APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            txtPath.Enabled = false;

            try
            {
                // 1. 环境检查
                lblStatus.Text = "检查环境…";
                progress.Value = 10;
                Log("");
                Log("[1/5] 检查运行环境");

                bool ok = true;
                try
                {
                    var rel = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
                        "Release", null);
                    if (rel != null && Convert.ToInt32(rel) >= 528040)
                        Log("      .NET Framework 4.8          OK");
                    else { Log("      .NET Framework 4.8          缺失"); ok = false; }
                }
                catch { Log("      .NET Framework 4.8          检查失败"); ok = false; }

                try
                {
                    var pv = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                        "pv", null);
                    if (pv != null) Log("      WebView2 Runtime           " + pv);
                    else { Log("      WebView2 Runtime           缺失"); ok = false; }
                }
                catch { Log("      WebView2 Runtime           检查失败"); }

                if (!ok)
                {
                    var r = MessageBox.Show(
                        "环境不完整，缺少必要组件。\n\n仍然继续安装？（可能无法运行）",
                        APP_NAME, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) { ResetUI(); return; }
                }

                await System.Threading.Tasks.Task.Delay(300);

                // 2. 创建目录
                lblStatus.Text = "创建目录…";
                progress.Value = 25;
                Log("");
                Log("[2/5] 创建目录");
                Directory.CreateDirectory(target);
                Directory.CreateDirectory(Path.Combine(target, "logs"));
                Directory.CreateDirectory(Path.Combine(target, "logs", "wv2"));
                Log("      " + target);

                // 3. 解压文件
                lblStatus.Text = "解压文件…";
                progress.Value = 45;
                Log("");
                Log("[3/5] 解压文件");

                var asm = Assembly.GetExecutingAssembly();
                var zipName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("payload.zip"));
                if (zipName == null)
                {
                    Log("      !! 安装包损坏");
                    MessageBox.Show("安装包损坏", APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ResetUI(); return;
                }

                using (var stream = asm.GetManifestResourceStream(zipName))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    int total = zip.Entries.Count;
                    int done = 0;
                    foreach (var entry in zip.Entries)
                    {
                        var dest = Path.Combine(target, entry.FullName.Replace('/', '\\'));
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(dest);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest));
                            entry.ExtractToFile(dest, true);
                        }
                        done++;
                        progress.Value = 45 + (int)(done * 30.0 / total);
                        Application.DoEvents();
                    }
                    Log("      共 " + total + " 个文件");
                }

                // 4. 桌面快捷方式
                lblStatus.Text = "创建快捷方式…";
                progress.Value = 80;
                Log("");
                Log("[4/5] 创建桌面快捷方式");

                try
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var lnk = Path.Combine(desktop, APP_NAME + ".lnk");
                    var exe = Path.Combine(target, "QQBotConsole.exe");

                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic sc = shell.CreateShortcut(lnk);
                    sc.TargetPath = exe;
                    sc.IconLocation = exe + ",0";
                    sc.WorkingDirectory = target;
                    sc.Description = APP_NAME;
                    sc.Save();
                    Log("      " + lnk);
                }
                catch (Exception ex) { Log("      快捷键创建失败：" + ex.Message); }

                // 5. 完成
                lblStatus.Text = "安装完成";
                progress.Value = 100;
                Log("");
                Log("[5/5] 安装完成");

                await System.Threading.Tasks.Task.Delay(300);

                var launch = MessageBox.Show(
                    "安装成功！\n\n是否立即启动 " + APP_NAME + "？",
                    APP_NAME, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (launch == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(Path.Combine(target, "QQBotConsole.exe"));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("启动失败：" + ex.Message, APP_NAME,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                Close();
            }
            catch (Exception ex)
            {
                Log("");
                Log("!! 安装失败：" + ex.Message);
                MessageBox.Show("安装失败：\n\n" + ex.Message, APP_NAME,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetUI();
            }
        }

        void ResetUI()
        {
            btnInstall.Enabled = true;
            btnBrowse.Enabled = true;
            txtPath.Enabled = true;
            progress.Value = 0;
            lblStatus.Text = "准备安装";
        }
    }
}
