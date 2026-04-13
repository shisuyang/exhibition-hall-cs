const fs = require('fs');
const f = 'C:/Users/User/.openclaw/workspace/exhibition-hall-cs/Controllers/VideoController.cs';
let c = fs.readFileSync(f, 'utf8');

// Logger替换Console
c = c.replace(
  "using ExhibitionClient.Services;",
  ""
);
c = c.replace(
  'Console.WriteLine($"[Video] 文件不存在: {fileName}");',
  'Logger.Error($"[Video] 文件不存在: {fileName}");'
);
c = c.replace(
  'Console.WriteLine($"[Video] 播放: {fileName}");',
  'Logger.Info($"[Video] 播放: {fileName}");'
);
c = c.replace(
  'Console.WriteLine($"[Video] 播放失败: {ex.Message}");',
  'Logger.Error($"[Video] 播放失败: {ex.Message}");'
);
c = c.replace(
  'Console.WriteLine($"[Video] 暂停: {fileName}");',
  'Logger.Info($"[Video] 暂停: {fileName}");'
);
c = c.replace(
  'Console.WriteLine($"[Video] 继续播放: {fileName}");',
  'Logger.Info($"[Video] 继续播放: {fileName}");'
);
c = c.replace(
  'Console.WriteLine($"[Video] 停止: {fileName}");',
  'Logger.Info($"[Video] 停止: {fileName}");'
);

// GetLocalPath支持URL
c = c.replace(
  'private string GetLocalPath(string fileName)',
  'private string ResolveLocalPath(string fileName)'
);
c = c.replace(
  'var path = Path.Combine(_mediaPath, Path.GetFileName(fileName));',
  `// 支持URL: 从URL取文件名并解码
            if (fileName.StartsWith("http://") || fileName.StartsWith("https://"))
            {
                var name = Uri.UnescapeDataString(fileName.Split('/').Last().Split('?')[0]);
                var path = Path.Combine(_mediaPath, name);
                return File.Exists(path) ? path : null;
            }
            var path = Path.Combine(_mediaPath, Path.GetFileName(fileName));`
);
c = c.replace(
  'GetLocalPath(fileName)',
  'ResolveLocalPath(fileName)'
);

// 去掉Container = null的崩溃
c = c.replace(
  '_container!.Visible = true;',
  '_container?.ResumeLayout(false); _container.Visible = true;'
);
c = c.replace(
  '_container.Visible = false;',
  '_container.Visible = false; _container?.SuspendLayout();'
);

fs.writeFileSync(f, c, 'utf8');
console.log('done');
