using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExhibitionClient.Models;

namespace ExhibitionClient.Services
{
    /// <summary>
    /// WebSocket 通信服务
    /// </summary>
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket _ws;
        private readonly string _url;
        private readonly int? _fixedScreenNumber;
        private readonly string _deviceId;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        
        public event Action<Command> OnCommand;
        public event Action<DeviceInfo> OnRegistered;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public bool IsConnected => _isConnected;
        public DeviceInfo Device { get; private set; }

        public WebSocketService(string url, int? fixedScreenNumber = null)
        {
            _url = url;
            _fixedScreenNumber = fixedScreenNumber;
            _deviceId = LoadOrCreateDeviceId();
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 从本地文件读取持久化的 deviceId，不存在则生成并保存
        /// </summary>
        private static string LoadOrCreateDeviceId()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "device-id.txt");

            if (System.IO.File.Exists(path))
            {
                var id = System.IO.File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    Logger.Log($"[WS] 加载本地 deviceId: {id}");
                    return id;
                }
            }

            var newId = Guid.NewGuid().ToString();
            System.IO.File.WriteAllText(path, newId);
            Logger.Log($"[WS] 生成新 deviceId: {newId}");
            return newId;
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                    return;

                _ws = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                
                await _ws.ConnectAsync(new Uri(_url), _cts.Token);
                _isConnected = true;
                OnConnected?.Invoke();

                // 注册设备（带持久化 deviceId + 固定屏幕号，服务端据此复用已有设备）
                await SendAsync(new
                {
                    type = "register",
                    deviceType = "client",
                    deviceId = _deviceId,
                    name = Environment.MachineName,
                    os = Environment.OSVersion.ToString(),
                    version = "2.0.0",
                    screenNumber = _fixedScreenNumber.HasValue && _fixedScreenNumber.Value > 0
                        ? (object)_fixedScreenNumber.Value
                        : null
                });

                // 启动接收循环
                _ = ReceiveLoop();
                
                // 启动心跳
                _ = HeartbeatLoop();
            }
            catch (Exception ex)
            {
                Logger.Log($"WebSocket 连接失败: {ex.Message}");
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            
            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(buffer, _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await CloseAsync();
                        break;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Logger.Log($"[WS] 收到: {json}");
                    
                    ProcessMessage(json);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log($"[WS] 接收异常: {ex.Message}");
                    break;
                }
            }

            _isConnected = false;
            OnDisconnected?.Invoke();
            
            // 断线重连
            await Task.Delay(3000);
            await ConnectAsync();
        }

        private void ProcessMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "registered":
                        if (root.TryGetProperty("device", out var deviceProp))
                        {
                            Device = new DeviceInfo
                            {
                                Id = deviceProp.GetProperty("id").GetString(),
                                ScreenNumber = deviceProp.TryGetProperty("screenNumber", out var sn) ? sn.GetInt32() : null,
                                Name = deviceProp.TryGetProperty("name", out var name) ? name.GetString() : null
                            };
                            OnRegistered?.Invoke(Device);
                            Logger.Log($"[WS] 已注册: {Device.Id}, 屏幕编号: {Device.ScreenNumber}");
                        }
                        break;

                    case "command":
                        var cmd = new Command
                        {
                            Type = type,
                            Action = root.TryGetProperty("action", out var action) ? action.GetString() : null,
                            ReplyText = root.TryGetProperty("replyText", out var rt) ? rt.GetString() : null,
                            File = root.TryGetProperty("params", out var p1) && p1.TryGetProperty("file", out var f) ? f.GetString() : null,
                            Text = root.TryGetProperty("params", out var p2) && p2.TryGetProperty("text", out var t) ? t.GetString() : null,
                            Question = root.TryGetProperty("params", out var p3) && p3.TryGetProperty("question", out var q) ? q.GetString() : null,
                            Answer = root.TryGetProperty("params", out var p4) && p4.TryGetProperty("answer", out var a) ? a.GetString() : null,
                            Slide = root.TryGetProperty("params", out var p5) && p5.TryGetProperty("slide", out var s) ? s.GetInt32() : null,
                            Mute = root.TryGetProperty("params", out var p6) && p6.TryGetProperty("mute", out var m) ? m.GetBoolean() : null
                        };
                        OnCommand?.Invoke(cmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[WS] 解析消息失败: {ex.Message}");
            }
        }

        private async Task HeartbeatLoop()
        {
            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(15000, _cts.Token);
                if (_ws.State == WebSocketState.Open)
                {
                    await SendAsync(new { type = "heartbeat" });
                }
            }
        }

        public async Task SendAsync(object data)
        {
            if (_ws.State != WebSocketState.Open)
                return;

            try
            {
                var json = JsonSerializer.Serialize(data);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log($"[WS] 发送失败: {ex.Message}");
            }
        }

        public async Task CloseAsync()
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    _cts.Cancel();
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch { }
            }
            _isConnected = false;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _ws?.Dispose();
        }
    }

    /// <summary>
    /// 日志服务
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = @"C:\media\logs\client.log";
        
        static Logger()
        {
            var dir = System.IO.Path.GetDirectoryName(LogPath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
        }

        public static void Log(string msg)
        {
            try
            {
                var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}";
                System.IO.File.AppendAllText(LogPath, log + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine(log);
            }
            catch { }
        }
    }
}
