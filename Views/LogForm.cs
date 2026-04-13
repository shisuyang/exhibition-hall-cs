using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExhibitionClient.Views
{
    /// <summary>
    /// 日志悬浮窗 - F10 呼出/隐藏
    /// </summary>
    public class LogForm : Form
    {
        private RichTextBox _logBox = null!;
        private Button _btnClear = null!;
        private Button _btnClose = null!;
        private static LogForm? _instance;

        public static LogForm Instance => _instance ??= new LogForm();

        private LogForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "📋 运行日志";
            Size = new Size(900, 500);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(0, Screen.PrimaryScreen!.WorkingArea.Height - 500);
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.FromArgb(200, 200, 200);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            TopMost = true;
            ShowInTaskbar = false;

            // 关闭时只隐藏，不销毁
            FormClosing += (s, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(6, 4, 6, 4)
            };

            _btnClear = new Button
            {
                Text = "清空",
                Size = new Size(60, 26),
                Location = new Point(6, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9F),
                Cursor = Cursors.Hand
            };
            _btnClear.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnClear.Click += (s, e) => _logBox.Clear();
            toolbar.Controls.Add(_btnClear);

            _btnClose = new Button
            {
                Text = "隐藏 (F10)",
                Size = new Size(90, 26),
                Location = new Point(74, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9F),
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnClose.Click += (s, e) => Hide();
            toolbar.Controls.Add(_btnClose);

            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Consolas", 10F),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                WordWrap = false
            };

            Controls.Add(_logBox);
            Controls.Add(toolbar);
        }

        /// <summary>
        /// 追加一条日志（线程安全）
        /// </summary>
        public void AppendLog(string level, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendLog(level, message)));
                return;
            }

            var color = level switch
            {
                "ERROR" => Color.FromArgb(255, 100, 100),
                "WARN"  => Color.FromArgb(255, 200, 80),
                "INFO"  => Color.FromArgb(100, 200, 255),
                _       => Color.FromArgb(180, 180, 180),
            };

            var ts = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{ts}] [{level}] {message}\n";

            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = color;
            _logBox.AppendText(line);

            // 超过 2000 行自动清理头部
            if (_logBox.Lines.Length > 2000)
            {
                _logBox.Select(0, _logBox.GetFirstCharIndexFromLine(500));
                _logBox.SelectedText = "";
            }

            _logBox.ScrollToCaret();
        }

        public void ToggleVisible()
        {
            if (Visible) Hide();
            else Show();
        }
    }
}
