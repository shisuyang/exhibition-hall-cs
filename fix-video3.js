const fs = require('fs');
const f = 'C:/Users/User/.openclaw/workspace/exhibition-hall-cs/Controllers/VideoController.cs';
let c = fs.readFileSync(f, 'utf8');

// 添加 using
c = c.replace(
  'using System;',
  'using System;\nusing ExhibitionClient.Services;'
);

// 重写 GetLocalPath，合并URL支持
c = c.replace(
  /private string\? GetLocalPath\(string fileName\)\s*\{[\s\S]*?return File\.Exists\(path\) \? path : null;\s*\}/,
  `private string? GetLocalPath(string fileName)
        {
            // 支持URL: 从URL取文件名并解码
            if (fileName.StartsWith("http://") || fileName.StartsWith("https://"))
            {
                var name = Uri.UnescapeDataString(fileName.Split('/').Last().Split('?')[0]);
                var p = Path.Combine(_mediaPath, name);
                Logger.Info($"[Video] URL->本地: {fileName} -> {p}");
                return File.Exists(p) ? p : null;
            }
            if (File.Exists(fileName))
                return fileName;
            var path = Path.Combine(_mediaPath, Path.GetFileName(fileName));
            return File.Exists(path) ? path : null;
        }`
);

// Logger替换Console
c = c.replace(
  'Console.WriteLine("[Video] 文件不存在', 'Logger.Error("[Video] 文件不存在'
);
c = c.replace(
  'Console.WriteLine("[Video] 播放: {fileName}");', 'Logger.Info($"[Video] 播放: {fileName}");'
);
c = c.replace(
  'Console.WriteLine("[Video] 播放失败: {ex.Message}");', 'Logger.Error($"[Video] 播放失败: {ex.Message}");'
);
c = c.replace(
  'Console.WriteLine("[Video] 暂停: {fileName}");', 'Logger.Info($"[Video] 暂停: {fileName}");'
);
c = c.replace(
  'Console.WriteLine("[Video] 继续播放: {fileName}");', 'Logger.Info($"[Video] 继续播放: {fileName}");'
);
c = c.replace(
  'Console.WriteLine("[Video] 停止: {fileName}");', 'Logger.Info($"[Video] 停止: {fileName}");'
);

fs.writeFileSync(f, c, 'utf8');
console.log('done');
