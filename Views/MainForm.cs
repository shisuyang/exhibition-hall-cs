using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ExhibitionClient.Services;
using ExhibitionClient.Controllers;
using LibVLCSharp.Shared;

namespace ExhibitionClient.Views
{
    /// <summary>
    /// 展厅主窗口 - 移植自 Electron 前端
    /// </summary>
    public class MainForm : Form
    {
        // ==================== 服务层 ====================
        private readonly WebSocketService _ws;
        private readonly FileSyncService _sync;
        private readonly CommentaryService _commentary;
        
        // ==================== 控制器 ====================
        private readonly VideoController _video;
        private readonly ImageController _image;
        private readonly PPTController _ppt;
        
        // ==================== 视图面板 ====================
        private Panel _idlePanel = null!;
        private Panel _speechPanel = null!;
        private Panel _qaPanel = null!;
        private Panel _topPanel = null!;
        
        // ==================== 待机画面控件 ====================
        private Label _idleLogo = null!;
        private Label _idleTitle = null!;
        private Label _idleSubtitle = null!;
        private Label _idleHint = null!;
        
        // ==================== 播报控件 ====================
        private Label _speechAvatar = null!;
        private Label _speechText = null!;
        
        // ==================== 问答控件 ====================
        private Label _qaQuestion = null!;
        private Label _qaAnswer = null!;
        
        // ==================== 状态栏 ====================
        private Panel _statusBar = null!;
        private Panel _statusBarFill = null!;
        
        // ==================== Toast ====================
        private Label _toast = null!;
        private System.Threading.Timer _toastTimer = null!;
        
        // ==================== 管理面板 ====================
        private Panel _adminPanel = null!;
        
        // ==================== 当前状态 ====================
        private string _currentView = "idle";
        private int? _screenNumber;
        private bool _isAdminVisible;
        private bool _isFullscreen;

        // ==================== 定时器 ====================
        private System.Threading.Timer _statusUpdateTimer = null!;

        public MainForm()
        {
            Core.Initialize();

            var wsUrl = System.Configuration.ConfigurationManager.AppSettings["WebSocketUrl"] ?? "ws://192.168.23.83:3000";
            var fileServerUrl = System.Configuration.ConfigurationManager.AppSettings["FileServerUrl"] ?? "http://192.168.23.83:3001";
            var mediaPath = System.Configuration.ConfigurationManager.AppSettings["MediaPath"] ?? @"C:\media";
            int? fixedScreen = null;
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScreenNumber"], out var sn) && sn > 0)
                fixedScreen = sn;

            _ws = new WebSocketService(wsUrl, fixedScreen);
            _ws.OnCommand += HandleCommand;
            _ws.OnRegistered += OnDeviceRegistered;
            _ws.OnConnected += OnWSConnected;
            _ws.OnDisconnected += OnWSDisconnected;

            _sync = new FileSyncService(fileServerUrl, mediaPath);
            _commentary = new CommentaryService(fileServerUrl);
            _commentary.OnSpeechFinished += OnSpeechFinished;

            _video = new VideoController(mediaPath);
            _video.OnEnded += OnVideoEnded;
            _video.OnError += OnVideoError;

            _image = new ImageController(mediaPath);

            _ppt = new PPTController(mediaPath);
            _ppt.OnClosed += OnPPTClosed;

            InitializeComponent();

            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            ConnectAndStart();
            _statusUpdateTimer = new System.Threading.Timer(UpdateStatus, null, 0, 1000);
        }

        private void InitializeComponent()
        {
            Text = "思德科技展厅";
            Size = new Size(1920, 1080);
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            
            _topPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            Controls.Add(_topPanel);

            _idlePanel = CreateIdlePanel();
            _topPanel.Controls.Add(_idlePanel);

            var videoContainer = _video.Container;
            videoContainer.Dock = DockStyle.Fill;
            videoContainer.Visible = false;
            _topPanel.Controls.Add(videoContainer);

            var imageContainer = _image.Container;
            imageContainer.Dock = DockStyle.Fill;
            imageContainer.Visible = false;
            _topPanel.Controls.Add(imageContainer);

            _speechPanel = CreateSpeechPanel();
            _speechPanel.Visible = false;
            _topPanel.Controls.Add(_speechPanel);

            _qaPanel = CreateQAPanel();
            _qaPanel.Visible = false;
            _topPanel.Controls.Add(_qaPanel);

            _statusBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = Color.FromArgb(34, 34, 34)
            };
            _statusBarFill = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                Height = 3,
                BackColor = Color.FromArgb(0, 212, 255)
            };
            _statusBar.Controls.Add(_statusBarFill);
            _topPanel.Controls.Add(_statusBar);

            _toast = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 14F),
                BackColor = Color.FromArgb(200, 0, 0, 0),
                ForeColor = Color.White,
                Visible = false,
                Location = new Point((Width - 400) / 2, 50),
                Size = new Size(400, 50)
            };
            _topPanel.Controls.Add(_toast);

            _adminPanel = CreateAdminPanel();
            _adminPanel.Visible = false;
            Controls.Add(_adminPanel);
        }

        private Panel CreateIdlePanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };

            panel.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(
                    panel.ClientRectangle,
                    Color.FromArgb(26, 26, 46),
                    Color.FromArgb(22, 33, 62),
                    LinearGradientMode.ForwardDiagonal);
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            };

            _idleLogo = new Label
            {
                Text = "🏢",
                Font = new Font("", 80F),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            panel.Controls.Add(_idleLogo);

            _idleTitle = new Label
            {
                Text = "思德科技展厅",
                Font = new Font("Microsoft YaHei UI", 48F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            panel.Controls.Add(_idleTitle);

            _idleSubtitle = new Label
            {
                Text = "SMART EXHIBITION HALL",
                Font = new Font("Microsoft YaHei UI", 18F),
                ForeColor = Color.FromArgb(136, 136, 136),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            panel.Controls.Add(_idleSubtitle);

            _idleHint = new Label
            {
                Text = "🔊 请使用移动端语音控制",
                Font = new Font("Microsoft YaHei UI", 14F),
                ForeColor = Color.FromArgb(85, 85, 85),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            panel.Controls.Add(_idleHint);

            panel.Resize += (s, e) =>
            {
                int centerX = panel.Width / 2;
                int startY = panel.Height / 2 - 150;
                
                _idleLogo.Location = new Point(centerX - 50, startY);
                _idleTitle.Location = new Point(centerX - _idleTitle.Width / 2, startY + 140);
                _idleSubtitle.Location = new Point(centerX - _idleSubtitle.Width / 2, startY + 220);
                _idleHint.Location = new Point(centerX - _idleHint.Width / 2, startY + 350);
            };

            return panel;
        }

        private Panel CreateSpeechPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(26, 26, 46) };

            panel.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(
                    panel.ClientRectangle,
                    Color.FromArgb(26, 26, 46),
                    Color.FromArgb(22, 33, 62),
                    LinearGradientMode.ForwardDiagonal);
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            };

            _speechAvatar = new Label
            {
                Text = "🦞",
                Font = new Font("", 60F),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true
            };
            panel.Controls.Add(_speechAvatar);

            _speechText = new Label
            {
                Font = new Font("Microsoft YaHei UI", 28F),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Location = new Point(100, 320),
                Size = new Size(Width - 200, 300)
            };
            panel.Controls.Add(_speechText);

            panel.Resize += (s, e) =>
            {
                _speechAvatar.Location = new Point(panel.Width / 2 - 75, 150);
                _speechText.Location = new Point(100, panel.Height / 2);
                _speechText.Size = new Size(panel.Width - 200, 300);
            };

            return panel;
        }

        private Panel CreateQAPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 32, 39) };

            panel.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(
                    panel.ClientRectangle,
                    Color.FromArgb(15, 32, 39),
                    Color.FromArgb(44, 83, 100),
                    LinearGradientMode.ForwardDiagonal);
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            };

            _qaQuestion = new Label
            {
                Font = new Font("Microsoft YaHei UI", 22F),
                ForeColor = Color.FromArgb(0, 212, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Location = new Point(60, 60),
                Size = new Size(panel.Width - 120, 100),
                Padding = new Padding(20)
            };
            _qaQuestion.BackColor = Color.FromArgb(26, 0, 212, 255);
            _qaQuestion.BorderStyle = BorderStyle.FixedSingle;
            panel.Controls.Add(_qaQuestion);

            _qaAnswer = new Label
            {
                Font = new Font("Microsoft YaHei UI", 28F),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Location = new Point(60, 200),
                Size = new Size(panel.Width - 120, panel.Height - 300)
            };
            panel.Controls.Add(_qaAnswer);

            return panel;
        }

        private Panel CreateAdminPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BackColor = Color.FromArgb(230, 0, 0, 0),
                Padding = new Padding(20)
            };

            var title = new Label
            {
                Text = "🖥️ 客户端控制",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 40
            };
            panel.Controls.Add(title);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            panel.Controls.Add(btnPanel);

            AddAdminButton(btnPanel, "🏠 待机画面", (s, e) => ShowView("idle"));
            AddAdminButton(btnPanel, "⛶ 全屏/退出", (s, e) => ToggleFullscreen());
            AddAdminButton(btnPanel, "🎬 测试视频", (s, e) => TestVideo());
            AddAdminButton(btnPanel, "🔊 测试播报", (s, e) => TestSpeech());
            AddAdminButton(btnPanel, "❓ 测试问答", (s, e) => TestQA());
            AddAdminButton(btnPanel, "📄 测试PPT", (s, e) => TestPPT());
            AddAdminButton(btnPanel, "🔇 静音/取消", (s, e) => ToggleMute());

            var info = new Label
            {
                Text = "未连接",
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.FromArgb(85, 85, 85),
                Dock = DockStyle.Bottom,
                Height = 30
            };
            panel.Controls.Add(info);
            panel.Tag = info;

            return panel;
        }

        private void AddAdminButton(Panel container, string text, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 12F),
                Size = new Size(260, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 34, 34),
                ForeColor = Color.FromArgb(204, 204, 204),
                Margin = new Padding(0, 5, 0, 5),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(68, 68, 68);
            btn.Click += handler;
            btn.MouseEnter += (s, e) => { btn.BackColor = Color.FromArgb(51, 51, 51); };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.FromArgb(34, 34, 34); };
            container.Controls.Add(btn);
        }

        private async void ConnectAndStart()
        {
            ShowToast("正在连接服务器...");
            
            try
            {
                await _ws.ConnectAsync();
            }
            catch
            {
                ShowToast("连接失败，3秒后重试...");
                await System.Threading.Tasks.Task.Delay(3000);
                ConnectAndStart();
            }
        }

        private void HandleCommand(Models.Command cmd)
        {
            BeginInvoke(new Action(() =>
            {
                Console.WriteLine($"[CMD] {cmd.Action}");
                
                switch (cmd.Action)
                {
                    case "play_video":
                        PlayVideo(cmd.File);
                        break;
                    case "play_ppt":
                    case "show_doc":
                        var ext = System.IO.Path.GetExtension(cmd.File)?.ToLower();
                        if (ext == ".pptx" || ext == ".ppt")
                            OpenPPT(cmd.File);
                        else
                            ShowDoc(cmd.File);
                        break;
                    case "next_slide":
                        _ppt.Next();
                        break;
                    case "prev_slide":
                        _ppt.Prev();
                        break;
                    case "goto_slide":
                        if (cmd.Slide.HasValue)
                            _ppt.Goto(cmd.Slide.Value);
                        break;
                    case "close_ppt":
                        _ppt.Close();
                        ShowView("idle");
                        ShowToast("📄 PPT 已关闭");
                        break;
                    case "fullscreen_ppt":
                        _ppt.ToggleFullscreen();
                        break;
                    case "pause":
                        _video.Pause();
                        _commentary.Stop();
                        break;
                    case "resume":
                        _video.Resume();
                        break;
                    case "mute":
                        var mute = cmd.Mute ?? true;
                        _commentary.IsMuted = mute;
                        _video.IsMuted = mute;
                        ShowToast(mute ? "🔇 已静音" : "🔊 已取消静音");
                        break;
                    case "speak":
                        Speak(cmd.Text ?? cmd.ReplyText ?? "");
                        break;
                    case "show_qa":
                        ShowQA(cmd.Question ?? "", cmd.Answer ?? "");
                        break;
                    case "home":
                    case "idle":
                        ShowView("idle");
                        break;
                    case "fullscreen":
                        ToggleFullscreen();
                        break;
                    default:
                        ShowToast("未知指令: " + cmd.Action);
                        break;
                }

                if (!string.IsNullOrEmpty(cmd.ReplyText))
                    ShowToast(cmd.ReplyText);
            }));
        }

        private void OnDeviceRegistered(Models.DeviceInfo device)
        {
            Invoke(new Action(() =>
            {
                _screenNumber = device.ScreenNumber;
                UpdateIdleHint();
                ShowToast($"已连接: {_screenNumber}号屏");
                _ = _commentary.LoadCommentaryAsync();
                _ = _sync.SyncAllAsync();
            }));
        }

        private void OnWSConnected() => BeginInvoke(new Action(() => UpdateAdminInfo("已连接")));
        private void OnWSDisconnected() => BeginInvoke(new Action(() => UpdateAdminInfo("断开连接")));
        private void OnVideoEnded() => BeginInvoke(new Action(ShowIdle));
        private void OnVideoError() => BeginInvoke(new Action(() => { ShowToast("❌ 视频播放失败"); ShowView("idle"); }));
        private void OnPPTClosed() => BeginInvoke(new Action(ShowIdle));
        private void OnSpeechFinished() => BeginInvoke(new Action(() => { if (_currentView == "speech") ShowView("idle"); }));

        private void ShowView(string view)
        {
            _currentView = view;
            
            _idlePanel.Visible = view == "idle";
            _video.Container.Visible = view == "video";
            _image.Container.Visible = view == "doc";
            _speechPanel.Visible = view == "speech";
            _qaPanel.Visible = view == "qa";
            
            if (view != "speech")
                _commentary.Stop();
        }

        private void ShowIdle()
        {
            _ppt.Close();
            _video.Hide();
            _image.Hide();
            _commentary.Stop();
            ShowView("idle");
        }

        private void PlayVideo(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            _ppt.Close();
            _image.Hide();
            _commentary.Stop();
            ShowView("video");
            _video.Play(fileName);
        }

        private void OpenPPT(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            _video.Hide();
            _image.Hide();
            ShowView("idle");
            
            try
            {
                _ppt.Open(fileName);
                ShowToast("📄 正在打开: " + System.IO.Path.GetFileName(fileName));
                var cleanName = System.IO.Path.GetFileName(fileName).Replace(" ", "");
                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                    BeginInvoke(new Action(() => _commentary.SpeakCommentary(cleanName))));
            }
            catch (Exception ex)
            {
                ShowToast("❌ PPT打开失败: " + ex.Message);
            }
        }

        private void ShowDoc(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            _ppt.Close();
            _video.Hide();
            _commentary.Stop();
            ShowView("doc");
            _image.ShowImage(fileName);
            var cleanName = System.IO.Path.GetFileName(fileName).Replace(" ", "");
            _commentary.SpeakCommentary(cleanName);
        }

        private void Speak(string text)
        {
            _ppt.Close();
            _video.Hide();
            _image.Hide();
            ShowView("speech");
            _speechText.Text = text;
            _commentary.Speak(text);
        }

        private void ShowQA(string question, string answer)
        {
            _ppt.Close();
            _video.Hide();
            _image.Hide();
            _commentary.Stop();
            ShowView("qa");
            _qaQuestion.Text = "❓ " + question;
            _qaAnswer.Text = answer;
            _commentary.Speak(answer);
        }

        private void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = _isFullscreen ? FormWindowState.Maximized : FormWindowState.Normal;
            TopMost = _isFullscreen;
            ShowToast(_isFullscreen ? "⛶ 全屏" : "⛶ 退出全屏");
        }

        private void TestVideo() => PlayVideo("test.mp4");
        private void TestSpeech() => Speak("您好，欢迎来到思德科技展厅，这里展示了我们的核心产品与解决方案。");
        private void TestQA() => ShowQA("思德科技的主营业务是什么？", "思德科技专注于人工智能营销解决方案，为企业打造智能化展厅与营销系统。");
        private void TestPPT() => OpenPPT("test.pptx");

        private void ToggleMute()
        {
            _commentary.IsMuted = !_commentary.IsMuted;
            _video.IsMuted = _commentary.IsMuted;
            ShowToast(_commentary.IsMuted ? "🔇 已静音" : "🔊 已取消静音");
        }

        private void ShowToast(string message)
        {
            _toast.Text = message;
            _toast.Visible = true;
            _toast.Location = new Point((Width - 400) / 2, 50);
            
            _toastTimer?.Dispose();
            _toastTimer = new System.Threading.Timer(_ =>
            {
                BeginInvoke(new Action(() => _toast.Visible = false));
            }, null, 2500, System.Threading.Timeout.Infinite);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F12:
                    _isAdminVisible = !_isAdminVisible;
                    _adminPanel.Visible = _isAdminVisible;
                    break;
                case Keys.Escape:
                    _adminPanel.Visible = false;
                    _isAdminVisible = false;
                    break;
                case Keys.F11:
                case Keys.Enter when e.Control:
                    ToggleFullscreen();
                    break;
                case Keys.Space:
                    if (_currentView == "video")
                        _video.Pause();
                    else if (_currentView == "speech")
                        _commentary.Stop();
                    break;
            }
        }

        private void UpdateStatus(object? state)
        {
            if (!IsHandleCreated) return;
            
            BeginInvoke(new Action(() =>
            {
                if (_currentView == "speech" && _commentary.IsSpeaking)
                {
                    _statusBarFill.Width = (_statusBarFill.Width + 5) % _statusBar.Width;
                }
                else
                {
                    _statusBarFill.Width = _ws.IsConnected ? _statusBar.Width : 0;
                }
            }));
        }

        private void UpdateIdleHint()
        {
            var hint = _screenNumber.HasValue
                ? $"📺 {_screenNumber}号屏 | 🔊 请使用移动端语音控制"
                : "🔊 请使用移动端语音控制";
            _idleHint.Text = hint;
        }

        private void UpdateAdminInfo(string status)
        {
            if (_adminPanel.Tag is Label label)
            {
                label.Text = _screenNumber.HasValue ? $"{_screenNumber}号屏 | {status}" : status;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _ws.Dispose();
            _sync.Dispose();
            _commentary.Dispose();
            _video.Dispose();
            _image.Dispose();
            _ppt.Dispose();
            _statusUpdateTimer?.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_toast != null)
                _toast.Location = new Point((Width - 400) / 2, 50);
        }
    }
}
