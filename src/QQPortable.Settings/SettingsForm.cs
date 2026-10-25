using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QQPortable.Shared;

namespace QQPortable.Settings
{
    internal sealed class SettingsForm : Form
    {
        private readonly string root;
        private readonly CheckBox autoStartBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly Label pathLabel = new Label();
        private readonly Label descriptionLabel = new Label();
        private PortableConfig config;
        private bool loading;
        private float appliedDpi = 96F;

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        public SettingsForm(string root)
        {
            this.root = root;
            AutoScaleMode = AutoScaleMode.None;
            Text = "QQ Portable 设置";
            Width = 620;
            Height = 380;
            MinimumSize = new Size(540, 320);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = LoadIcon();

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(20),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = "QQ Portable",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10),
            }, 0, 0);

            descriptionLabel.Text = "控制登录 Windows 后是否自动启动当前便携目录的 QQ。启动器、设置程序和 QQ 插件共用同一份配置。";
            descriptionLabel.AutoSize = true;
            descriptionLabel.ForeColor = Color.DimGray;
            descriptionLabel.Margin = new Padding(0, 0, 0, 18);
            layout.Controls.Add(descriptionLabel, 0, 1);

            autoStartBox.Text = "登录 Windows 后自动启动 QQ";
            autoStartBox.AutoSize = true;
            autoStartBox.Margin = new Padding(0, 0, 0, 10);
            autoStartBox.CheckedChanged += AutoStartChanged;
            layout.Controls.Add(autoStartBox, 0, 2);

            statusLabel.AutoSize = true;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0, 0, 0, 14);
            layout.Controls.Add(statusLabel, 0, 3);

            pathLabel.AutoSize = false;
            pathLabel.Dock = DockStyle.Fill;
            pathLabel.ForeColor = Color.DimGray;
            pathLabel.TextAlign = ContentAlignment.TopLeft;
            pathLabel.Margin = new Padding(0, 0, 0, 14);
            layout.Controls.Add(pathLabel, 0, 4);

            Button closeButton = new Button
            {
                Text = "关闭",
                Width = 96,
                Height = 32,
                Anchor = AnchorStyles.Right,
            };
            closeButton.Click += (sender, args) => Close();
            layout.Controls.Add(closeButton, 0, 5);

            Controls.Add(layout);
            Load += (sender, args) => LoadConfig();
            Resize += (sender, args) => UpdateWrapping();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float windowDpi = GetDpiForWindow(Handle);
            if (windowDpi < 96F) windowDpi = 96F;
            if (windowDpi == appliedDpi)
            {
                UpdateWrapping();
                return;
            }

            float scale = windowDpi / appliedDpi;
            appliedDpi = windowDpi;
            SuspendLayout();
            Scale(new SizeF(scale, scale));
            MinimumSize = new Size(Scaled(540), Scaled(320));
            if (Width < Scaled(620)) Width = Scaled(620);
            if (Height < Scaled(380)) Height = Scaled(380);
            // 字体按点数由系统按 DPI 渲染，只缩放控件尺寸，不放大字体。
            Font = new Font("Microsoft YaHei UI", 9F);
            ResumeLayout(true);
            UpdateWrapping();
        }

        private void LoadConfig()
        {
            loading = true;
            try
            {
                config = PortableConfig.LoadOrCreate(root);
                autoStartBox.Checked = config.AutoStart;
                pathLabel.Text = "配置文件：" + config.FilePath;
                SetStatus(config.AutoStart ? "开机自启已开启。" : "开机自启已关闭。", false);
            }
            catch (Exception error)
            {
                autoStartBox.Enabled = false;
                pathLabel.Text = "配置文件：" + Path.Combine(root, PortableConfig.FileName);
                SetStatus(error.Message, true);
            }
            finally
            {
                loading = false;
            }
        }

        private void AutoStartChanged(object sender, EventArgs e)
        {
            if (loading || config == null)
            {
                return;
            }

            bool previous = config.AutoStart;
            bool enabled = autoStartBox.Checked;
            autoStartBox.Enabled = false;
            try
            {
                config.AutoStart = enabled;
                config.Save();
                AutoStartRegistration.Sync(root, enabled);
                SetStatus(enabled ? "已开启开机自启。" : "已关闭开机自启。", false);
            }
            catch (Exception error)
            {
                config.AutoStart = previous;
                try
                {
                    config.Save();
                }
                catch
                {
                    // 保留原始错误，避免回滚失败掩盖真正原因。
                }
                loading = true;
                autoStartBox.Checked = previous;
                loading = false;
                SetStatus(error.Message, true);
                MessageBox.Show(this, error.Message, "QQ Portable 设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                autoStartBox.Enabled = true;
            }
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        private void UpdateWrapping()
        {
            int width = ClientSize.Width - Scaled(56);
            if (width > Scaled(200))
            {
                descriptionLabel.MaximumSize = new Size(width, 0);
            }
        }

        private int Scaled(int value)
        {
            return (int)Math.Round(value * appliedDpi / 96.0);
        }

        private static Icon LoadIcon()
        {
            using (Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("launcher.ico"))
            {
                return stream == null ? null : new Icon(stream);
            }
        }
    }
}
