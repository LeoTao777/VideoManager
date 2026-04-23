# CLAUDE.md

## 1. 项目概览
- 项目名：`VideoManager`
- 类型：Windows 桌面应用（WPF，`.NET 10`）
- 目标：浏览本地视频文件，展示缩略图，支持播放/暂停/进度拖动，并可对当前帧截图保存与回看。

## 2. 技术栈与依赖
- 运行时：`.NET 10`（`net10.0-windows`）
- UI：`WPF`（XAML + code-behind）
- MVVM：`Prism.Core`（`BindableBase`、`DelegateCommand`）
- 其他包：`CommunityToolkit.Mvvm`（已引用，当前主流程中使用较少）
- 外部工具依赖：`ffmpeg.exe`（截图功能依赖）

`VideoManager.csproj` 关键配置：
- `UseWPF=true`
- 输出目录重定向到 `.artifact/bin/...`
- 资源包含字体与图标（`Assets/Fonts`、`Assets/Ico`）

## 3. 目录结构（核心）
- `App.xaml` / `App.xaml.cs`：应用入口，默认启动 `Views/MainView.xaml`
- `Views/MainView.xaml`：主窗体（自定义标题栏 + 底部导航 + 内容区域）
- `ViewModels/MainViewViewModel.cs`：页面切换命令 `NavCommand`
- `Views/SubPages/ManagerView.xaml(.cs)`：视频管理页（主功能页）
- `ViewModels/ManagerViewModel.cs`：目录扫描、播放状态、缩略图加载、进度状态
- `Services/SnapshotIndexService.cs`：截图索引持久化（JSON）
- `Views/SubPages/SnapshotGalleryWindow.xaml(.cs)`：截图列表弹窗
- `Views/SubPages/HomePageView/SettingsView/AboutView`：占位页，逻辑较少

## 4. 主要功能流程
1. 用户点击“选择文件夹”
2. 通过 Win32 文件夹选择对话框选择目录
3. 递归扫描视频文件（支持 `.mp4/.mkv/.avi/.mov/.flv/.wmv/.m4v/.ts`）
4. 列表先展示占位缩略图，再并发异步替换真实缩略图
5. 双击视频开始播放，支持：
   - 暂停/继续
   - 退出播放
   - 进度条拖动跳转
6. 点击“保存截图”后：
   - 暂停播放
   - 调用 `ffmpeg` 按当前时间截帧
   - 保存 PNG 到截图目录
   - 记录索引到 `snapshot-index.json`
7. 右键视频项“查看截图”打开截图画廊窗口

## 5. 数据与存储
- 已选目录持久化：
  - 路径：`<AppContext.BaseDirectory>/setting/selected-folder.json`
- 截图文件与索引：
  - 根目录：`F:\testImage`（硬编码）
  - 索引文件：`F:\testImage\snapshot-index.json`

截图索引结构（`SnapshotRecord`）：
- `VideoPath`
- `ImagePath`
- `CaptureTimeMs`
- `CreatedAtUtc`

## 6. 运行与调试
- IDE：Visual Studio（WPF 项目）
- 直接运行 `VideoManager.csproj`
- 若截图失败，优先检查：
  - 系统中是否存在 `ffmpeg.exe`
  - `FFMPEG_PATH` 环境变量是否正确
  - 程序目录下 `ffmpeg.exe` / `tools/ffmpeg.exe` / `ffmpeg/bin/ffmpeg.exe`

## 7. 当前代码特征与注意点
- 采用 MVVM + code-behind 混合模式：
  - 状态管理在 `ViewModel`
  - 媒体控件事件、进度条交互、截图触发在 `ManagerView.xaml.cs`
- 缩略图加载包含取消与版本号保护，避免目录频繁切换导致旧任务覆盖新结果
- `SnapshotIndexService` 通过 `lock` 保证索引读写线程安全
- 项目中存在少量注释/字符串编码异常（疑似历史编码问题），不影响主要逻辑但建议统一清理

## 8. 已知风险/改进建议（后续 Claude 可优先处理）
- `ResolveFfmpegExecutable()` 中 PATH 检查使用了 `ffmpegPath`（而非常见 `PATH`），可能导致无法从系统 PATH 正常发现 ffmpeg
- `MainView` 中 `DragArea_MouseLeftButtonDown` 已定义但 XAML 未绑定
- Home/Settings/About 当前为占位页面
- 缺少自动化测试（特别是：目录扫描、截图索引、ffmpeg 解析逻辑）

## 9. 给 Claude 的协作建议
- 先从 `ManagerViewModel` + `ManagerView.xaml.cs` 入手理解核心行为
- 涉及截图功能的改动时，优先验证 `ffmpeg` 寻址与异常路径
- 优先把“路径配置化 + ffmpeg 检测修复 + 编码清理”作为第一批稳定性改造
