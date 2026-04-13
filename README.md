# 展厅展示端 - C# WinForms 版本

基于 Electron 前端重写的 C# WinForms 版本，使用原生 Windows 控件。

## ✅ 编译完成

发布目录：`exhibition-hall-cs/publish/`

**直接运行 `ExhibitionClient.exe` 即可！**

## 功能对比

| 功能 | Electron 版本 | C# WinForms 版本 |
|------|--------------|------------------|
| 视频播放 | HTML5 `<video>` | LibVLC |
| PPT 控制 | HTTP 调外部服务 | **PowerPoint 进程 + 键盘命令** |
| 图片展示 | Canvas 绘制 | PictureBox |
| TTS 播报 | speechSynthesis | Windows 语音合成 (SAPI) |
| WebSocket | 浏览器原生 | ClientWebSocket |
| 文件同步 | fetch | HttpClient |

## 项目结构

```
exhibition-hall-cs/
├── Program.cs                    # 入口
├── App.config                    # 配置文件
├── ExhibitionClient.csproj      # 项目文件
├── Models/
│   └── Command.cs               # 命令模型
├── Services/
│   ├── WebSocketService.cs      # WebSocket 通信
│   ├── FileSyncService.cs        # 文件同步
│   └── CommentaryService.cs      # 讲解词/TTS
├── Controllers/
│   ├── PPTController.cs         # PPT 控制
│   ├── VideoController.cs        # 视频播放 (LibVLC)
│   └── ImageController.cs        # 图片展示
├── Views/
│   └── MainForm.cs              # 主窗口
├── Properties/
│   └── Settings.Designer.cs      # 设置
└── publish/                      # 发布输出目录
    └── ExhibitionClient.exe     # ✅ 可直接运行
```

## 依赖

- **.NET 8 运行时** - 已打包在 publish 目录中
- **Microsoft Office PowerPoint** - 用于打开 PPT 文件
- **VLC 运行时** - 已包含在 publish 目录

## 配置

编辑 `App.config` 修改服务器地址：

```xml
<appSettings>
    <add key="WebSocketUrl" value="ws://192.168.23.83:3000" />
    <add key="FileServerUrl" value="http://192.168.23.83:3001" />
    <add key="MediaPath" value="C:\media" />
</appSettings>
```

## 操作说明

| 操作 | 方式 |
|------|------|
| 全屏 | F11 或 Ctrl+Enter |
| 调出管理面板 | F12 |
| 关闭管理面板 | Escape |
| 暂停播报 | Space |

## 命令列表

通过 WebSocket 接收的命令：

| 命令 | 参数 | 说明 |
|------|------|------|
| play_video | file | 播放视频 |
| play_ppt | file | 打开 PPT |
| show_doc | file | 显示图片/文档 |
| next_slide | - | PPT 下一页 |
| prev_slide | - | PPT 上一页 |
| goto_slide | slide | 跳转到指定页 |
| close_ppt | - | 关闭 PPT |
| pause | - | 暂停播放 |
| resume | - | 继续播放 |
| mute | mute=true/false | 静音/取消静音 |
| speak | text | 文字播报 |
| show_qa | question, answer | 显示问答 |
| home | - | 返回待机画面 |

## 日志

运行日志保存在：`C:\media\logs\client.log`

## 从源码重新编译

```bash
cd exhibition-hall-cs
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

## 迁移说明

### 前端代码迁移

| Electron JS | C# WinForms |
|-------------|-------------|
| `new WebSocket()` | `ClientWebSocket` |
| `fetch()` | `HttpClient` |
| `speechSynthesis.speak()` | PowerShell 调用 SAPI |
| `document.getElementById()` | WinForms 控件 |
| `classList.add/remove` | `Visible` 属性 |
| `setTimeout` | `System.Threading.Timer` |
| `localStorage` | `Properties.Settings.Default` |

### 主要区别

1. **UI 布局**：HTML/CSS → WinForms Panel + GDI+
2. **事件处理**：JS 事件 → C# 事件委托
3. **异步操作**：JS Promise → C# async/await
4. **PPT 控制**：HTTP 调用 → PowerPoint 进程控制

## TODO

- [ ] 添加开机自启功能
- [ ] 添加多屏幕支持
- [ ] 添加远程截图功能
- [ ] 添加日志查看界面
- [ ] 添加配置界面
