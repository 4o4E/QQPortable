using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQPortable.Installer
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox targetBox = new TextBox();
        private readonly ComboBox qqBox = new ComboBox();
        private readonly ComboBox liteLoaderBox = new ComboBox();
        private readonly Button browseButton = new Button();
        private readonly Button installButton = new Button();
        private readonly TextBox logBox = new TextBox();
        private readonly TextBox statusLabel = new TextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            TabStop = false,
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 36,
        };
        private TableLayoutPanel rootLayout;
        private float appliedDpi = 96F;

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "QQ Portable Installer";
            using (Stream iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("launcher.ico"))
            {
                if (iconStream != null)
                {
                    Icon = new Icon(iconStream);
                }
            }
            Width = 1040;
            Height = 760;
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(14),
            };
            TableLayoutPanel root = rootLayout;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(Caption("目标目录"), 0, 0);
            targetBox.Dock = DockStyle.Fill;
            targetBox.Text = "D:\\QQPortable";
            root.Controls.Add(targetBox, 1, 0);
            browseButton.Text = "浏览";
            browseButton.Dock = DockStyle.Fill;
            browseButton.Click += (sender, args) => BrowseTarget();
            root.Controls.Add(browseButton, 2, 0);

            root.Controls.Add(Caption("QQ 版本"), 0, 1);
            qqBox.Dock = DockStyle.Fill;
            qqBox.DropDownStyle = ComboBoxStyle.DropDownList;
            root.Controls.Add(qqBox, 1, 1);
            root.SetColumnSpan(qqBox, 2);

            root.Controls.Add(Caption("LiteLoader"), 0, 2);
            liteLoaderBox.Dock = DockStyle.Fill;
            liteLoaderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            root.Controls.Add(liteLoaderBox, 1, 2);
            root.SetColumnSpan(liteLoaderBox, 2);

            statusLabel.Text = "正在读取 GitHub 版本列表…";
            statusLabel.ForeColor = Color.DimGray;
            root.Controls.Add(statusLabel, 0, 3);
            root.SetColumnSpan(statusLabel, 3);

            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Color.White;
            root.Controls.Add(logBox, 0, 4);
            root.SetColumnSpan(logBox, 3);

            installButton.Text = "开始安装";
            installButton.Height = 44;
            installButton.AutoSize = false;
            installButton.Dock = DockStyle.Fill;
            installButton.Enabled = false;
            installButton.Click += async (sender, args) => await InstallAsync();
            root.Controls.Add(installButton, 0, 5);
            root.SetColumnSpan(installButton, 3);

            TextBox hint = new TextBox
            {
                Text = "只安装 QQ、LiteLoader 和启动器；插件请安装完成后自行放入 LiteLoader\\plugins。",
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                TabStop = false,
                Multiline = true,
                ForeColor = Color.DimGray,
                Height = 42,
                Dock = DockStyle.Fill,
            };
            root.Controls.Add(hint, 0, 6);
            root.SetColumnSpan(hint, 3);

            Controls.Add(root);
            Load += async (sender, args) => await LoadCatalogsAsync();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float windowDpi = GetDpiForWindow(Handle);
            if (windowDpi < 96F) windowDpi = 96F;
            if (windowDpi == appliedDpi)
            {
                return;
            }

            float scale = windowDpi / appliedDpi;
            appliedDpi = windowDpi;
            SuspendLayout();
            Scale(new SizeF(scale, scale));
            MinimumSize = new Size((int)(900 * scale), (int)(640 * scale));
            if (Width < (int)(1040 * scale)) Width = (int)(1040 * scale);
            if (Height < (int)(760 * scale)) Height = (int)(760 * scale);
            installButton.MinimumSize = new Size(0, Scaled(44));
            // 字体按点数由系统按 DPI 渲染，只缩放控件尺寸，不放大字体。
            Font = new Font("Microsoft YaHei UI", 9F);
            ResumeLayout(true);
        }

        private int Scaled(int value)
        {
            return (int)Math.Round(value * appliedDpi / 96.0);
        }

        private static TextBox Caption(string text)
        {
            return new TextBox
            {
                Text = text,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                TabStop = false,
                Dock = DockStyle.Fill,
            };
        }

        private void BrowseTarget()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择便携 QQ 的安装目录";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    targetBox.Text = dialog.SelectedPath;
                }
            }
        }

        private async Task LoadCatalogsAsync()
        {
            try
            {
                IReadOnlyList<QqAsset> qqAssets = await GitHubCatalog.GetQqAssetsAsync();
                IReadOnlyList<LiteLoaderRelease> releases = await GitHubCatalog.GetLiteLoaderReleasesAsync();
                qqBox.Items.Clear();
                foreach (QqAsset asset in qqAssets)
                {
                    qqBox.Items.Add(asset);
                }
                liteLoaderBox.Items.Clear();
                foreach (LiteLoaderRelease release in releases)
                {
                    liteLoaderBox.Items.Add(release);
                }
                if (qqBox.Items.Count > 0) qqBox.SelectedIndex = 0;
                if (liteLoaderBox.Items.Count > 0) liteLoaderBox.SelectedIndex = 0;
                installButton.Enabled = qqBox.Items.Count > 0 && liteLoaderBox.Items.Count > 0;
                statusLabel.Text = "版本列表已加载。QQ 来源：a1515333102/QQ-Releases + Rodert/qq-versions";
                AppendLog("QQ 版本：" + qqBox.Items.Count + " 个；LiteLoader 版本：" + liteLoaderBox.Items.Count + " 个");
            }
            catch (Exception error)
            {
                statusLabel.Text = "读取版本列表失败";
                AppendLog(error.ToString());
            }
        }

        private async Task InstallAsync()
        {
            if (qqBox.SelectedItem is not QqAsset qqAsset || liteLoaderBox.SelectedItem is not LiteLoaderRelease release)
            {
                return;
            }

            string target = targetBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "请选择目标目录。", "QQ Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            {
                MessageBox.Show(this, "目标目录不是空的，请换一个空目录。", "QQ Portable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            installButton.Enabled = false;
            browseButton.Enabled = false;
            qqBox.Enabled = false;
            liteLoaderBox.Enabled = false;
            AppendLog("开始安装 QQ " + qqAsset.Version + " / LiteLoader " + release.Tag);
            bool completed = false;
            try
            {
                InstallResult result = await Task.Run(() => InstallerCore.Install(new InstallOptions
                {
                    TargetDirectory = target,
                    QQVersion = qqAsset.Version,
                    LiteLoaderVersion = release.Tag,
                }, AppendLog));
                ShowCompletion(result, target);
                completed = true;
            }
            catch (Exception error)
            {
                AppendLog(error.ToString());
                statusLabel.Text = "安装失败，详情见日志";
            }
            finally
            {
                if (!completed)
                {
                    installButton.Enabled = true;
                    browseButton.Enabled = true;
                    qqBox.Enabled = true;
                    liteLoaderBox.Enabled = true;
                }
            }
        }

        private void ShowCompletion(InstallResult result, string target)
        {
            Controls.Clear();

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(Scaled(28)),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TextBox title = new TextBox
            {
                Text = "安装完成",
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                TabStop = false,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, Scaled(12)),
                Dock = DockStyle.Fill,
            };
            layout.Controls.Add(title, 0, 0);

            TextBox summary = new TextBox
            {
                Text = result.Summary + "\r\n\r\n插件请自行放入：\r\n" + Path.Combine(target, "LiteLoader", "plugins"),
                ReadOnly = true,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
            };
            layout.Controls.Add(summary, 0, 1);

            CheckBox openQq = new CheckBox
            {
                Text = "立即打开 QQ",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, Scaled(12), 0, Scaled(12)),
            };
            layout.Controls.Add(openQq, 0, 2);

            Button finish = new Button
            {
                Text = "完成",
                Width = Scaled(100),
                Height = Scaled(32),
                Anchor = AnchorStyles.Right,
            };
            finish.Click += (sender, args) =>
            {
                if (openQq.Checked)
                {
                    string launcher = Path.Combine(target, "QQPortable.exe");
                    try
                    {
                        Process.Start(new ProcessStartInfo(launcher)
                        {
                            WorkingDirectory = target,
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(this, error.Message, "无法打开 QQ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                Close();
            };
            layout.Controls.Add(finish, 0, 3);
            Controls.Add(layout);
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
    }
}
