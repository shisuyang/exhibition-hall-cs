using System;
using System.IO;
using System.Text.Json;

namespace ExhibitionClient.Services
{
    public class RuntimeSettings
    {
        public string WebSocketUrl { get; set; } = "ws://192.168.23.83:3000";
        public string FileServerUrl { get; set; } = "http://192.168.23.83:3001";
        public string MediaPath { get; set; } = @"C:\media";
        public int? ScreenNumber { get; set; }
            = null;

        public static string SettingsPath => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            "settings.json");

        public static RuntimeSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<RuntimeSettings>(json);
                    if (settings != null)
                    {
                        Logger.Info($"[Settings] 已加载 settings.json: {SettingsPath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Settings] 读取 settings.json 失败: {ex.Message}");
            }

            var fallback = new RuntimeSettings
            {
                WebSocketUrl = System.Configuration.ConfigurationManager.AppSettings["WebSocketUrl"] ?? "ws://192.168.23.83:3000",
                FileServerUrl = System.Configuration.ConfigurationManager.AppSettings["FileServerUrl"] ?? "http://192.168.23.83:3001",
                MediaPath = System.Configuration.ConfigurationManager.AppSettings["MediaPath"] ?? @"C:\media"
            };

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScreenNumber"], out var sn) && sn > 0)
                fallback.ScreenNumber = sn;

            Logger.Info("[Settings] 未找到 settings.json，使用 App.config");
            return fallback;
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            Logger.Info($"[Settings] 已保存 settings.json: {SettingsPath}");
        }
    }
}
