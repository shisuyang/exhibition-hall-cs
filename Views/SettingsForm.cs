using System;
using System.Drawing;
using System.Windows.Forms;
using ExhibitionClient.Services;

namespace ExhibitionClient.Views
{
    public class SettingsForm : Form
    {
        private readonly RuntimeSettings _settings;
        private TextBox _wsHost = null!;
        private NumericUpDown _wsPort = null!;
        private TextBox _fileHost = null!;
        private NumericUpDown _filePort = null!;
        private TextBox _mediaPath = null!;
        private NumericUpDown _screenNumber = null!;

        public SettingsForm(RuntimeSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            Text = "客户端配置";
            Size = new Size(520, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(28, 28, 32);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(20),
                BackColor = BackColor
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(panel);

            AddRow(panel, 0, "WebSocket IP", out _wsHost);
            AddPortRow(panel, 1, "WebSocket 端口", out _wsPort);
            AddRow(panel, 2, "文件服务 IP", out _fileHost);
            AddPortRow(panel, 3, "文件服务端口", out _filePort);
            AddRow(panel, 4, "媒体目录", out _mediaPath);
            AddPortRow(panel, 5, "屏幕号", out _screenNumber);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                BackColor = BackColor
            };

            var btnSave = MakeButton("保存", (_, __) => SaveAndClose());
            var btnCancel = MakeButton("取消", (_, __) => DialogResult = DialogResult.Cancel);
            var btnDefault = MakeButton("恢复默认", (_, __) => ResetDefault());
            var btnExit = MakeButton("退出程序", (_, __) => { DialogResult = DialogResult.Abort; Close(); });

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnDefault);
            btnPanel.Controls.Add(btnExit);
            panel.Controls.Add(btnPanel, 0, 6);
            panel.SetColumnSpan(btnPanel, 2);
        }

        private void AddRow(TableLayoutPanel panel, int row, string label, out TextBox textBox)
        {
            var lbl = MakeLabel(label);
            textBox = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(textBox, 1, row);
        }

        private void AddPortRow(TableLayoutPanel panel, int row, string label, out NumericUpDown input)
        {
            var lbl = MakeLabel(label);
            input = new NumericUpDown { Dock = DockStyle.Left, Width = 120, Minimum = 0, Maximum = 65535 };
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(input, 1, row);
        }

        private Label MakeLabel(string text) => new Label
        {
            Text = text,
            ForeColor = Color.White,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 0)
        };

        private Button MakeButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(8),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 52),
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 90);
            btn.Click += onClick;
            return btn;
        }

        private void LoadValues()
        {
            var ws = new Uri(_settings.WebSocketUrl);
            var fs = new Uri(_settings.FileServerUrl);
            _wsHost.Text = ws.Host;
            _wsPort.Value = ws.Port;
            _fileHost.Text = fs.Host;
            _filePort.Value = fs.Port;
            _mediaPath.Text = _settings.MediaPath;
            _screenNumber.Value = _settings.ScreenNumber ?? 0;
        }

        private void ResetDefault()
        {
            _wsHost.Text = "192.168.23.83";
            _wsPort.Value = 3000;
            _fileHost.Text = "192.168.23.83";
            _filePort.Value = 3001;
            _mediaPath.Text = @"C:\media";
            _screenNumber.Value = 0;
        }

        private void SaveAndClose()
        {
            _settings.WebSocketUrl = $"ws://{_wsHost.Text}:{_wsPort.Value}";
            _settings.FileServerUrl = $"http://{_fileHost.Text}:{_filePort.Value}";
            _settings.MediaPath = _mediaPath.Text;
            _settings.ScreenNumber = _screenNumber.Value > 0 ? (int)_screenNumber.Value : null;
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
