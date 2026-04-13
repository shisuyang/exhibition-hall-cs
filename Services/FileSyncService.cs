using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ExhibitionClient.Models;

namespace ExhibitionClient.Services
{
    /// <summary>
    /// 文件同步服务 - 从服务器下载文件到本地
    /// </summary>
    public class FileSyncService : IDisposable
    {
        private readonly string _fileServerUrl;
        private readonly string _localPath;
        private readonly HttpClient _http;
        private readonly HashSet<string> _syncedFiles = new();

        public FileSyncService(string fileServerUrl, string localPath)
        {
            _fileServerUrl = fileServerUrl.TrimEnd('/');
            _localPath = localPath;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            
            if (!Directory.Exists(_localPath))
                Directory.CreateDirectory(_localPath);
        }

        /// <summary>
        /// 获取文件列表并同步
        /// </summary>
        public async Task SyncAllAsync()
        {
            try
            {
                Logger.Info("[Sync] 开始同步文件...");
                
                var response = await _http.GetStringAsync($"{_fileServerUrl}/list");
                var data = JsonSerializer.Deserialize<FileListResponse>(response);
                
                if (data?.Files == null)
                {
                    Logger.Warn("[Sync] 文件列表为空");
                    return;
                }

                var tasks = data.Files.Select(f =>
                {
                    // name 字段在 Windows 上可能乱码，从 url 里解码文件名更可靠
                    string fileName = f.Name;
                    if (!string.IsNullOrEmpty(f.Url))
                    {
                        try
                        {
                            var decoded = Uri.UnescapeDataString(f.Url.Split('/').Last());
                            if (!string.IsNullOrEmpty(decoded)) fileName = decoded;
                        }
                        catch { }
                    }
                    return DownloadFileAsync(fileName, f.Url);
                });
                await Task.WhenAll(tasks);
                
                Logger.Info($"[Sync] 同步完成，共 {data.Files.Count} 个文件");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Sync] 同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下载单个文件
        /// </summary>
        public async Task<string?> DownloadFileAsync(string fileName, string? fileUrl)
        {
            var localPath = Path.Combine(_localPath, fileName);
            
            try
            {
                if (_syncedFiles.Contains(fileName) && File.Exists(localPath))
                    return localPath;

                Logger.Info($"[Sync] 下载: {fileName}");

                // 优先用服务端返回的 url（已正确编码），fallback 到拼接
                var url = fileUrl?.StartsWith("http") == true ? fileUrl : $"{_fileServerUrl}/{Uri.EscapeDataString(fileName)}";
                var response = await _http.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, bytes);
                    _syncedFiles.Add(fileName);
                    Logger.Info($"[Sync] 完成: {fileName}");
                }
                else
                {
                    Logger.Warn($"[Sync] 下载失败 [{response.StatusCode}]: {fileName}");
                }
                
                return localPath;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Sync] 下载异常: {fileName} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取本地文件路径
        /// </summary>
        public string? GetLocalPath(string fileName)
        {
            var localPath = Path.Combine(_localPath, fileName);
            return File.Exists(localPath) ? localPath : null;
        }

        /// <summary>
        /// 检查本地文件是否存在
        /// </summary>
        public bool FileExists(string fileName)
        {
            return File.Exists(Path.Combine(_localPath, fileName));
        }

        private class FileListResponse
        {
            public List<FileItem> Files { get; set; } = new();
        }

        private class FileItem
        {
            public string Name { get; set; } = string.Empty;
            public string? Url { get; set; }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
