using System;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using ExhibitionClient.Services;

namespace ExhibitionClient.Controllers
{
    /// <summary>
    /// 视频播放控制器 - 使用 LibVLC
    /// </summary>
    public class VideoController : IDisposable
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private VideoView? _videoView;
        private Panel? _container;
        private readonly string _mediaPath;
        private bool _isPlaying;
        private bool _isMuted;
        private Media? _currentMedia;

        public event Action? OnEnded;
        public event Action? OnError;
        public event Action? OnPlaying;

        public bool IsPlaying => _isPlaying;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_mediaPlayer != null)
                    _mediaPlayer.Mute = value;
            }
        }

        public VideoView? VideoView => _videoView;
        public Panel? Container => _container;

        public VideoController(string mediaPath = @"C:\media")
        {
            _mediaPath = mediaPath;
            InitializeVLC();
        }

        private void InitializeVLC()
        {
            try
            {
                Core.Initialize();
                
                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--no-osd",
                    "--mouse-hide-timeout=0"
                );

                _container = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = System.Drawing.Color.Black,
                    Visible = false
                };

                _videoView = new VideoView
                {
                    Dock = DockStyle.Fill,
                    MediaPlayer = null
                };

                _container.Controls.Add(_videoView);

                _mediaPlayer = new MediaPlayer(_libVLC);
                _videoView.MediaPlayer = _mediaPlayer;

                _mediaPlayer.Playing += (s, e) =>
                {
                    _isPlaying = true;
                    OnPlaying?.Invoke();
                    Logger.Info("[Video] 开始播放");
                };

                _mediaPlayer.EndReached += (s, e) =>
                {
                    _isPlaying = false;
                    OnEnded?.Invoke();
                    Logger.Info("[Video] 播放结束");
                };

                Logger.Info("[Video] LibVLC 初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Video] LibVLC 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放视频文件（支持URL或本地路径）
        /// </summary>
        public void Play(string fileName)
        {
            try
            {
                var localPath = GetLocalPath(fileName);
                
                if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                {
                    Logger.Error($"[Video] 文件不存在: {fileName}");
                    OnError?.Invoke();
                    return;
                }

                Logger.Info($"[Video] 播放: {localPath}");

                _container!.Visible = true;
                _videoView!.Visible = true;

                Stop();

                using var media = new Media(_libVLC!, localPath, FromType.FromPath);
                _currentMedia = media;

                _mediaPlayer!.Media = media;
                _mediaPlayer.Volume = _isMuted ? 0 : 100;
                _mediaPlayer.Fullscreen = true;
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Video] 播放失败: {ex.Message}");
                OnError?.Invoke();
            }
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            if (_mediaPlayer?.CanPause == true)
            {
                _mediaPlayer.Pause();
                Logger.Info("[Video] 已暂停");
            }
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        public void Resume()
        {
            if (_mediaPlayer?.State == VLCState.Paused)
            {
                _mediaPlayer.Play();
                Logger.Info("[Video] 继续播放");
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Fullscreen = false;
                }
                _currentMedia?.Dispose();
                _currentMedia = null;
                _isPlaying = false;
                Logger.Info("[Video] 已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Video] 停止失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 跳转到指定位置（秒）
        /// </summary>
        public void Seek(long timeMs)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Time = timeMs;
            }
        }

        /// <summary>
        /// 隐藏播放器
        /// </summary>
        public void Hide()
        {
            Stop();
            _container!.Visible = false;
            _videoView!.Visible = false;
        }

        /// <summary>
        /// 显示播放器
        /// </summary>
        public void Show()
        {
            _container!.Visible = true;
            _videoView!.Visible = true;
        }

        private string? GetLocalPath(string fileName)
        {
            // 支持URL: 从URL取文件名并解码
            if (fileName.StartsWith("http://") || fileName.StartsWith("https://"))
            {
                var name = Uri.UnescapeDataString(fileName.Split('/').Last().Split('?')[0]);
                var path = Path.Combine(_mediaPath, name);
                return File.Exists(path) ? path : null;
            }
            if (File.Exists(fileName))
                return fileName;
            var p = Path.Combine(_mediaPath, Path.GetFileName(fileName));
            return File.Exists(p) ? p : null;
        }

        public void Dispose()
        {
            Stop();
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;
            _libVLC?.Dispose();
            _videoView?.Dispose();
            _container?.Dispose();
        }
    }
}
