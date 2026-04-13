using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExhibitionClient.Controllers
{
    /// <summary>
    /// PPT 控制服务 - 通过进程和键盘命令控制 PowerPoint
    /// </summary>
    public class PPTController : IDisposable
    {
        private Process? _pptProcess;
        private string? _currentFile;
        private readonly string _mediaPath;
        private bool _isOpened;
        private CancellationTokenSource? _watchdogCts;

        public event Action? OnOpened;
        public event Action? OnClosed;

        public bool IsOpened => _isOpened;
        public string? CurrentFile => _currentFile;

        // PowerPoint 窗口类名
        private const string PPT_CLASS_NAME = "PPTFrameClass";
        private const string PPT7_CLASS_NAME = "OfficePowerPointFrame";

        public PPTController(string mediaPath = @"C:\media")
        {
            _mediaPath = mediaPath;
        }

        /// <summary>
        /// 打开 PPT 文件
        /// </summary>
        public void Open(string filePath)
        {
            try
            {
                // 确保是本地路径
                var fileName = Path.GetFileName(filePath);
                var localPath = Path.Combine(_mediaPath, fileName);

                if (!File.Exists(localPath))
                {
                    Console.WriteLine($"[PPT] 文件不存在: {localPath}");
                    throw new FileNotFoundException($"PPT 文件不存在: {fileName}");
                }

                // 关闭之前的
                Close();

                // 杀掉可能残留的 PowerPoint 进程
                KillAllPowerPoint();

                // 启动 PowerPoint
                var psi = new ProcessStartInfo
                {
                    FileName = localPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Maximized
                };

                _pptProcess = Process.Start(psi);
                if (_pptProcess == null)
                {
                    throw new Exception("无法启动 PowerPoint");
                }

                _currentFile = fileName;
                _isOpened = true;

                Console.WriteLine($"[PPT] 启动 PowerPoint: {fileName}");

                // 等待 PPT 窗口出现，然后全屏
                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    EnterSlideShow();
                    await Task.Delay(500);
                    MaximizeWindow();
                });

                OnOpened?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 打开失败: {ex.Message}");
                _isOpened = false;
                throw;
            }
        }

        /// <summary>
        /// 进入幻灯片放映模式
        /// </summary>
        public void EnterSlideShow()
        {
            SendKey(0xF5);
            Console.WriteLine("[PPT] 进入放映模式");
        }

        /// <summary>
        /// 下一页
        /// </summary>
        public void Next()
        {
            SendKey(0x22);
            SendKey(0x27);
            Console.WriteLine("[PPT] 下一页");
        }

        /// <summary>
        /// 上一页
        /// </summary>
        public void Prev()
        {
            SendKey(0x21);
            SendKey(0x25);
            Console.WriteLine("[PPT] 上一页");
        }

        /// <summary>
        /// 跳转到指定页
        /// </summary>
        public void Goto(int slideNumber)
        {
            SendKey(0x1B);
            Thread.Sleep(300);

            SendKeys.SendWait(slideNumber.ToString());
            Thread.Sleep(100);
            SendKey(0x0D);
            Console.WriteLine($"[PPT] 跳转到第 {slideNumber} 页");
        }

        /// <summary>
        /// 退出放映
        /// </summary>
        public void ExitSlideShow()
        {
            SendKey(0x1B);
            Console.WriteLine("[PPT] 退出放映");
        }

        /// <summary>
        /// 全屏/退出全屏
        /// </summary>
        public void ToggleFullscreen()
        {
            SendKey(0xF5);
            Console.WriteLine("[PPT] 切换全屏");
        }

        /// <summary>
        /// 最大化窗口
        /// </summary>
        public void MaximizeWindow()
        {
            var hwnd = FindPowerPointWindow();
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 3);
            }
        }

        /// <summary>
        /// 关闭 PPT
        /// </summary>
        public void Close()
        {
            try
            {
                _watchdogCts?.Cancel();
                
                if (_pptProcess != null && !_pptProcess.HasExited)
                {
                    SendKey(0x1B);
                    Thread.Sleep(500);
                    
                    SendKey(0x11, 0x57);
                    Thread.Sleep(500);

                    if (!_pptProcess.HasExited)
                    {
                        _pptProcess.Kill();
                    }
                }

                KillAllPowerPoint();
                
                _pptProcess?.Dispose();
                _pptProcess = null;
                _currentFile = null;
                _isOpened = false;
                
                Console.WriteLine("[PPT] 已关闭");
                OnClosed?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 关闭异常: {ex.Message}");
            }
        }

        private void SendKey(byte vkCode)
        {
            var hwnd = FindPowerPointWindow();
            if (hwnd == IntPtr.Zero) return;

            SendMessage(hwnd, 0x0100, (IntPtr)vkCode, IntPtr.Zero);
            Thread.Sleep(50);
            SendMessage(hwnd, 0x0101, (IntPtr)vkCode, IntPtr.Zero);
            Thread.Sleep(50);
        }

        private void SendKey(byte vkCode1, byte vkCode2)
        {
            var hwnd = FindPowerPointWindow();
            if (hwnd == IntPtr.Zero) return;

            SendMessage(hwnd, 0x0100, (IntPtr)0x11, IntPtr.Zero);
            Thread.Sleep(50);
            
            SendMessage(hwnd, 0x0100, (IntPtr)vkCode2, IntPtr.Zero);
            Thread.Sleep(50);
            
            SendMessage(hwnd, 0x0101, (IntPtr)vkCode2, IntPtr.Zero);
            Thread.Sleep(50);
            
            SendMessage(hwnd, 0x0101, (IntPtr)0x11, IntPtr.Zero);
            Thread.Sleep(50);
        }

        private IntPtr FindPowerPointWindow()
        {
            var hwnd = FindWindow(PPT7_CLASS_NAME, null);
            if (hwnd == IntPtr.Zero)
                hwnd = FindWindow(PPT_CLASS_NAME, null);
            
            return hwnd;
        }

        private void KillAllPowerPoint()
        {
            foreach (var p in Process.GetProcessesByName("POWERPNT"))
            {
                try
                {
                    p.Kill();
                    p.Dispose();
                }
                catch { }
            }
        }

        // Windows API
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public void Dispose()
        {
            Close();
            _watchdogCts?.Dispose();
        }
    }
}
