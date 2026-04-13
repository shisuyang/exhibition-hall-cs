const fs = require('fs');
const f = 'C:/Users/User/.openclaw/workspace/exhibition-hall-cs/Views/MainForm.cs';
let c = fs.readFileSync(f, 'utf8');

// 1. 恢复 Core.Initialize()
c = c.replace(
  '// Core.Initialize(); // TODO: 调试禁用 LibVLC 初始化',
  'Core.Initialize();'
);

// 2. 恢复 VideoController 初始化
c = c.replace(
  /\/\/ _video = new VideoController\(mediaPath\);[\s\S]*?\/\/ _video\.OnError \+= OnVideoError;\s*\n\s*_video = null!;/,
  '_video = new VideoController(mediaPath);\n            _video.OnEnded += OnVideoEnded;\n            _video.OnError += OnVideoError;'
);

// 3. 恢复视频容器
c = c.replace(
  /\/\/ TODO: 调试禁用视频容器\n\s*\/\/ var videoContainer = _video\.Container;\n\s*\/\/ videoContainer\.Dock = DockStyle\.Fill;\n\s*\/\/ videoContainer\.Visible = false;\n\s*\/\/ _topPanel\.Controls\.Add\(videoContainer\);/,
  'var videoContainer = _video.Container;\n            videoContainer.Dock = DockStyle.Fill;\n            videoContainer.Visible = false;\n            _topPanel.Controls.Add(videoContainer);'
);

// 4. 恢复 HandleCommand 里的 play_video
c = c.replace(
  /_video\.IsMuted = _commentary\.IsMuted;/g,
  '_video.IsMuted = _commentary.IsMuted;'
);

// 5. 恢复 ShowView 视频
c = c.replace(
  /\/\/ TODO: 调试禁用视频容器\n\s*(\/\/ _video\.Container\.Visible = view == "video";)/,
  '_video.Container.Visible = view == "video";'
);

// 6. 恢复 ShowDoc 视频 Hide
c = c.replace(
  /\/\/ TODO: 调试禁用视频 Hide\n\s*(\/\/ _video\.Hide\(\);)/,
  '_video.Hide();'
);

// 7. 恢复 TestVideo
c = c.replace(
  /\/\/ TODO: 调试禁用视频\n\s*private void TestVideo\(\) \{ \}/,
  'private void TestVideo() => PlayVideo("test.mp4");'
);

// 8. 恢复 OnKeyDown video pause
c = c.replace(
  /if \(_currentView == "video"\)\s*\n\s*_video\.Pause\(\);/,
  'if (_currentView == "video")\n                        _video.Pause();'
);

// 9. 恢复 HandleCommand 里的 play_video case
c = c.replace(
  /case "pause":\s*\n\s*_video\.Pause\(\);/,
  'case "pause":\n                        _video.Pause();'
);

fs.writeFileSync(f, c, 'utf8');
console.log('done');
