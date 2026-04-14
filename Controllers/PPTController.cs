using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ExhibitionClient.Services;

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

                // 直接调用 PowerPoint 打开，避免系统默认程序/受保护视图影响 F5
                var psi = new ProcessStartInfo
                {
                    FileName = "powerpnt.exe",
                    Arguments = $"/S \"{localPath}\"",
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

                Logger.Info($"[PPT] 启动 PowerPoint: {fileName}");

                // 等待 PPT 窗口出现，反复尝试进入放映并置顶
                Task.Run(async () =>
                {
                    await EnsureSlideShowAndTopMostAsync();
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

        private async Task EnsureSlideShowAndTopMostAsync()
        {
            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(500);
                var hwnd = FindPowerPointWindow();
                Logger.Info($"[PPT] 尝试检测窗口 #{i + 1}, hwnd={hwnd}");
                if (hwnd == IntPtr.Zero) continue;

                ShowWindow(hwnd, 3);
                SetForegroundWindow(hwnd);
                Logger.Info("[PPT] 已最大化并前置，尝试进入放映模式");

                SendKeyToWindow(hwnd, 0x0D); // Enter，确保窗口激活
                await Task.Delay(150);
                SendKeyToWindow(hwnd, 0xF5);
                await Task.Delay(1200);

                var slideHwnd = FindSlideShowWindow();
                Logger.Info($"[PPT] 放映窗口检测 slideHwnd={slideHwnd}");
                if (slideHwnd != IntPtr.Zero)
                {
                    ShowWindow(slideHwnd, 3);
                    SetForegroundWindow(slideHwnd);
                    Logger.Info("[PPT] 放映窗口已前置（未置顶）");
                    return;
                }
            }

            Logger.Warn("[PPT] 未检测到放映窗口，尝试发送 Shift+F5");
            var hwnd2 = FindPowerPointWindow();
            if (hwnd2 != IntPtr.Zero)
            {
                SendShiftFunctionKey(hwnd2, 0x74); // F5
            }
        }

        /// <summary>
        /// 进入幻灯片放映模式
        /// </summary>
        public void EnterSlideShow()
        {
            var hwnd = FindPowerPointWindow();
            if (hwnd != IntPtr.Zero)
            {
                SendKeyToWindow(hwnd, 0xF5);
                Logger.Info("[PPT] 进入放映模式");
            }
            else
            {
                Logger.Warn("[PPT] 未找到 PowerPoint 窗口，无法进入放映模式");
            }
        }

        /// <summary>
        /// 下一页
        /// </summary>
        public void Next()
        {
            var hwnd = FindSlideShowWindow();
            if (hwnd == IntPtr.Zero) hwnd = FindPowerPointWindow();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Warn("[PPT] 下一页失败：未找到放映/编辑窗口");
                return;
            }

            FocusWindowForCommand(hwnd);
            SendKeyToWindow(hwnd, 0x27); // Right
            Thread.Sleep(80);
            SendKeyToWindow(hwnd, 0x22); // PageDown
            Logger.Info($"[PPT] 下一页 hwnd={hwnd}");
        }

        /// <summary>
        /// 上一页
        /// </summary>
        public void Prev()
        {
            var hwnd = FindSlideShowWindow();
            if (hwnd == IntPtr.Zero) hwnd = FindPowerPointWindow();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Warn("[PPT] 上一页失败：未找到放映/编辑窗口");
                return;
            }

            FocusWindowForCommand(hwnd);
            SendKeyToWindow(hwnd, 0x25); // Left
            Thread.Sleep(80);
            SendKeyToWindow(hwnd, 0x21); // PageUp
            Logger.Info($"[PPT] 上一页 hwnd={hwnd}");
        }

        /// <summary>
        /// 跳转到指定页
        /// </summary>
        public void Goto(int slideNumber)
        {
            var hwnd = FindSlideShowWindow();
            if (hwnd == IntPtr.Zero) hwnd = FindPowerPointWindow();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Warn($"[PPT] 跳转失败：未找到窗口，slide={slideNumber}");
                return;
            }

            FocusWindowForCommand(hwnd);
            SendKeys.SendWait(slideNumber.ToString());
            Thread.Sleep(100);
            SendKeyToWindow(hwnd, 0x0D);
            Logger.Info($"[PPT] 跳转到第 {slideNumber} 页 hwnd={hwnd}");
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
            SendKeyToWindow(hwnd, vkCode);
        }

        private void SendKeyToWindow(IntPtr hwnd, byte vkCode)
        {
            keybd_event(vkCode, 0, 0, 0);
            Thread.Sleep(50);
            keybd_event(vkCode, 0, 2, 0); // KEYEVENTF_KEYUP
            Thread.Sleep(50);
        }

        private void FocusWindowForCommand(IntPtr hwnd)
        {
            ShowWindow(hwnd, 3);
            SetForegroundWindow(hwnd);
            Logger.Info($"[PPT] 命令前临时拉前台 hwnd={hwnd}");
            Thread.Sleep(120);
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

        private void SendShiftFunctionKey(IntPtr hwnd, byte vkCode)
        {
            SendMessage(hwnd, 0x0100, (IntPtr)0x10, IntPtr.Zero);
            Thread.Sleep(50);
            SendMessage(hwnd, 0x0100, (IntPtr)vkCode, IntPtr.Zero);
            Thread.Sleep(50);
            SendMessage(hwnd, 0x0101, (IntPtr)vkCode, IntPtr.Zero);
            Thread.Sleep(50);
            SendMessage(hwnd, 0x0101, (IntPtr)0x10, IntPtr.Zero);
            Thread.Sleep(50);
            Logger.Info($"[PPT] 已发送 Shift+{vkCode}");
        }

        private IntPtr FindPowerPointWindow()
        {
            var hwnd = FindWindow(PPT7_CLASS_NAME, null);
            if (hwnd == IntPtr.Zero)
                hwnd = FindWindow(PPT_CLASS_NAME, null);
            return hwnd;
        }

        private IntPtr FindSlideShowWindow()
        {
            return FindWindow("screenClass", null);
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
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        public void Dispose()
        {
            Close();
            _watchdogCts?.Dispose();
        }
    }
}
