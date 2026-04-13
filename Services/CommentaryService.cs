using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExhibitionClient.Services
{
    /// <summary>
    /// 讲解词服务 - 加载和朗读讲解词
    /// 注意：在 .NET Core/.NET 8 上，语音合成使用 System.Speech 或 Microsoft Speech SDK
    /// </summary>
    public class CommentaryService : IDisposable
    {
        private readonly string _fileServerUrl;
        private readonly HttpClient _http;
        private readonly Dictionary<string, string> _commentaryMap = new();
        private bool _isSpeaking;
        private bool _isMuted;
        
        // 语音合成使用系统内置的 Process + PowerShell 或 Windows 语音 API
        private System.Diagnostics.Process _speechProcess;

        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;

        public bool IsSpeaking => _isSpeaking;
        public bool IsMuted
        {
            get => _isMuted;
            set => _isMuted = value;
        }

        public CommentaryService(string fileServerUrl)
        {
            _fileServerUrl = fileServerUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// 加载讲解词文件
        /// </summary>
        public async Task LoadCommentaryAsync()
        {
            try
            {
                var json = await _http.GetStringAsync($"{_fileServerUrl}/commentary.json");
                var commentaries = JsonSerializer.Deserialize<List<CommentaryItem>>(json);
                
                _commentaryMap.Clear();
                if (commentaries != null)
                {
                    foreach (var item in commentaries)
                    {
                        if (!string.IsNullOrEmpty(item.File) && 
                            !string.IsNullOrEmpty(item.Commentary) && 
                            item.Status == "ok")
                        {
                            _commentaryMap[item.File] = item.Commentary;
                        }
                    }
                }
                
                Logger.Log($"[Commentary] 已加载 {_commentaryMap.Count} 条讲解词");
            }
            catch (Exception ex)
            {
                Logger.Log($"[Commentary] 加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 朗读指定文件的讲解词
        /// </summary>
        public void SpeakCommentary(string fileName)
        {
            // 清理文件名（去掉空格、路径等）
            var key = CleanFileName(fileName);
            
            if (!_commentaryMap.TryGetValue(key, out var raw))
            {
                Logger.Log($"[Commentary] 无讲解词: {fileName}");
                return;
            }

            var text = ParseCommentary(raw);
            if (string.IsNullOrEmpty(text))
                return;

            Speak(text);
        }

        /// <summary>
        /// 朗读文本 - 使用 Windows 语音合成 (SAPI)
        /// </summary>
        public void Speak(string text)
        {
            if (_isMuted)
            {
                Logger.Log("[Commentary] 已静音，跳过朗读");
                return;
            }

            Stop();

            _isSpeaking = true;
            OnSpeechStarted?.Invoke();

            Logger.Log($"[Commentary] 朗读 ({text.Length}字): {text.Substring(0, Math.Min(50, text.Length))}...");

            // 全部放到后台线程，避免 Process.Start 阻塞 UI
            Task.Run(() =>
            {
                try
                {
                    var psScript = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.SelectVoiceByHints('Female', 'Adult')
$synth.Rate = 0
$synth.Speak('{text.Replace("'", "''")}')
";
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };

                    var proc = System.Diagnostics.Process.Start(psi);
                    _speechProcess = proc;

                    proc?.WaitForExit();
                    _isSpeaking = false;
                    OnSpeechFinished?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Commentary] 语音合成失败: {ex.Message}");
                    _isSpeaking = false;
                    OnSpeechFinished?.Invoke();
                }
            });
        }

        /// <summary>
        /// 停止朗读
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_speechProcess != null && !_speechProcess.HasExited)
                {
                    _speechProcess.Kill();
                    _speechProcess.Dispose();
                    _speechProcess = null;
                }
            }
            catch { }
            
            _isSpeaking = false;
        }

        /// <summary>
        /// 解析讲解词（清理 HTML、替换分段标记）
        /// </summary>
        private string ParseCommentary(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // 去掉 HTML 标签
            var text = Regex.Replace(raw, "<[^>]+>", string.Empty);
            text = text.Replace("&nbsp;", " ");
            
            // 去掉 "高亮标记 X" 标记
            text = Regex.Replace(text, @"高亮标记\s*\d*\s*", string.Empty);
            
            // && 表示分段，用句号连接
            text = Regex.Replace(text, @"\s*&&\s*", "。");
            
            return text.Trim();
        }

        /// <summary>
        /// 清理文件名（统一格式）
        /// </summary>
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            
            // 去掉路径
            var name = Path.GetFileName(fileName);
            
            // 去掉空格
            name = name.Replace(" ", "").Replace("　", "");
            
            return name;
        }

        public void Dispose()
        {
            Stop();
            _http?.Dispose();
        }
    }

    internal class CommentaryItem
    {
        public string File { get; set; }
        public string Commentary { get; set; }
        public string Status { get; set; }
    }
}
