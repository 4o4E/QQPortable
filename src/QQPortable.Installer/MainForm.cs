using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly Label statusLabel = new Label();
        private TableLayoutPanel rootLayout;

        public MainForm()
        {
            Text = "QQ Portable Installer";
            Width = 780;
            Height = 560;
            MinimumSize = new Size(680, 480);
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
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label { Text = "目标目录", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            targetBox.Dock = DockStyle.Fill;
            targetBox.Text = "D:\\QQPortable";
            root.Controls.Add(targetBox, 1, 0);
            browseButton.Text = "浏览";
            browseButton.Dock = DockStyle.Fill;
            browseButton.Click += (sender, args) => BrowseTarget();
            root.Controls.Add(browseButton, 2, 0);

            root.Controls.Add(new Label { Text = "QQ 版本", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            qqBox.Dock = DockStyle.Fill;
            qqBox.DropDownStyle = ComboBoxStyle.DropDownList;
            root.Controls.Add(qqBox, 1, 1);
            root.SetColumnSpan(qqBox, 2);

            root.Controls.Add(new Label { Text = "LiteLoader", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            liteLoaderBox.Dock = DockStyle.Fill;
            liteLoaderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            root.Controls.Add(liteLoaderBox, 1, 2);
            root.SetColumnSpan(liteLoaderBox, 2);

            statusLabel.Text = "正在读取 GitHub 版本列表…";
            statusLabel.AutoSize = true;
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
            installButton.Height = 32;
            installButton.Dock = DockStyle.Fill;
            installButton.Enabled = false;
            installButton.Click += async (sender, args) => await InstallAsync();
            root.Controls.Add(installButton, 1, 5);

            Label hint = new Label
            {
                Text = "只安装 QQ、LiteLoader 和启动器；插件请安装完成后自行放入 LiteLoader\\plugins。",
                AutoSize = true,
                ForeColor = Color.DimGray,
            };
            root.Controls.Add(hint, 0, 6);
            root.SetColumnSpan(hint, 3);

            Controls.Add(root);
            Load += async (sender, args) => await LoadCatalogsAsync();
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
                statusLabel.Text = "版本列表已加载。QQ 来源：" + "a1515333102/QQ-Releases";
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
                Padding = new Padding(28),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = new Label
            {
                Text = "安装完成",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
            };
            layout.Controls.Add(title, 0, 0);

            Label summary = new Label
            {
                Text = result.Summary + "\r\n\r\n插件请自行放入：\r\n" + Path.Combine(target, "LiteLoader", "plugins"),
                AutoSize = true,
                MaximumSize = new Size(680, 0),
            };
            layout.Controls.Add(summary, 0, 1);

            CheckBox openQq = new CheckBox
            {
                Text = "立即打开 QQ",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 12, 0, 12),
            };
            layout.Controls.Add(openQq, 0, 2);

            Button finish = new Button
            {
                Text = "完成",
                Width = 100,
                Height = 32,
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
