using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prism.Commands;

namespace VideoManager.ViewModels
{
    public class ManagerViewModel : BindableBase
    {
        private CancellationTokenSource? _thumbnailCts;
        private int _loadVersion;

        private string _selectedFolderPath = "未选择文件夹";

        public string SelectedFolderPath
        {
            get => _selectedFolderPath;
            set => SetProperty(ref _selectedFolderPath, value);
        }

        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();

        public DelegateCommand SelectFolderCommand { get; }

        private VideoItem? _selectedVideo;

        public VideoItem? SelectedVideo
        {
            get => _selectedVideo;
            set
            {
                if (SetProperty(ref _selectedVideo, value))
                {
                    // 仅用于高亮选择，不触发播放
                }
            }
        }

        private bool _isPlaying;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    RaisePropertyChanged(nameof(ShowVideoList));
                    RaisePropertyChanged(nameof(ShowControls));
                    TogglePauseCommand?.RaiseCanExecuteChanged();
                    ExitPlaybackCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool ShowVideoList => !IsPlaying;
        public bool ShowControls => IsPlaying;

        private Uri? _playbackUri;

        public Uri? PlaybackUri
        {
            get => _playbackUri;
            set
            {
                if (SetProperty(ref _playbackUri, value))
                {
                    // PlaybackUri 变化时，播放状态一般需要同步刷新
                    if (value == null)
                    {
                        IsPlaying = false;
                        IsPaused = false;
                    }
                }
            }
        }

        private bool _isPaused;

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    RaisePropertyChanged(nameof(PauseButtonText));
                }
            }
        }

        public string PauseButtonText => IsPaused ? "继续" : "暂停";

        private TimeSpan _videoPosition;

        public TimeSpan VideoPosition
        {
            get => _videoPosition;
            set
            {
                if (SetProperty(ref _videoPosition, value))
                {
                    RaisePropertyChanged(nameof(VideoProgress));
                }
            }
        }

        private TimeSpan _videoDuration;

        public TimeSpan VideoDuration
        {
            get => _videoDuration;
            set
            {
                if (SetProperty(ref _videoDuration, value))
                {
                    RaisePropertyChanged(nameof(VideoProgress));
                }
            }
        }

        public double VideoProgress
        {
            get
            {
                var durationSeconds = VideoDuration.TotalSeconds;
                if (durationSeconds <= 0)
                    return 0;
                return Math.Max(0, Math.Min(1, VideoPosition.TotalSeconds / durationSeconds));
            }
        }

        public DelegateCommand TogglePauseCommand { get; }
        public DelegateCommand ExitPlaybackCommand { get; }

        public ManagerViewModel()
        {
            SelectFolderCommand = new DelegateCommand(ExecuteSelectFolder);
            TogglePauseCommand = new DelegateCommand(TogglePause, CanTogglePause);
            ExitPlaybackCommand = new DelegateCommand(ExitPlayback, () => IsPlaying);
        }

        public void StartPlayback(VideoItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FullName))
                return;

            try
            {
                PlaybackUri = new Uri(item.FullName);
                IsPaused = false;
                IsPlaying = true;
                VideoPosition = TimeSpan.Zero;
                VideoDuration = TimeSpan.Zero;
            }
            catch
            {
                // ignore invalid uri
            }
        }

        private void TogglePause()
        {
            if (!IsPlaying)
                return;
            IsPaused = !IsPaused;
        }

        private bool CanTogglePause()
        {
            return IsPlaying;
        }

        private void ExitPlayback()
        {
            PlaybackUri = null;
            IsPlaying = false;
            IsPaused = false;
            VideoPosition = TimeSpan.Zero;
            VideoDuration = TimeSpan.Zero;
        }

        private void ExecuteSelectFolder()
        {
            // 使用 Win32 原生对话框选择文件夹，避免依赖 System.Windows.Forms
            var selectedPath = BrowseForFolder("选择包含视频文件的文件夹");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (!Directory.Exists(selectedPath))
                return;

            SelectedFolderPath = selectedPath;
            LoadVideosFromFolder(selectedPath);
        }

        private static string? BrowseForFolder(string description)
        {
            // 允许用户选择“目录”，并尽量使用新的样式
            const uint BIF_RETURNONLYFSDIRS = 0x0001;
            const uint BIF_NEWDIALOGSTYLE = 0x0040;

            var ownerWindow = Application.Current?.MainWindow;
            var ownerHandle = ownerWindow != null ? new WindowInteropHelper(ownerWindow).Handle : IntPtr.Zero;

            var bi = new BROWSEINFO
            {
                hwndOwner = ownerHandle,
                pidlRoot = IntPtr.Zero,
                pszDisplayName = IntPtr.Zero,
                lpszTitle = description,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
                lpfn = IntPtr.Zero,
                lParam = IntPtr.Zero,
                iImage = 0
            };

            var pidl = SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero)
                return null; // 用户取消

            try
            {
                var path = new StringBuilder(260);
                var ok = SHGetPathFromIDList(pidl, path);
                return ok ? path.ToString() : null;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        private void LoadVideosFromFolder(string folderPath)
        {
            Videos.Clear();

            var supportedExtensions = new[]
            {
                ".mp4", ".mkv", ".avi", ".mov",
                ".flv", ".wmv", ".m4v", ".ts"
            };

            try
            {
                var filePaths = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList();

                // 为避免用户快速切换目录时“旧任务覆盖新任务”，用版本号做保护
                var myVersion = Interlocked.Increment(ref _loadVersion);
                _thumbnailCts?.Cancel();
                _thumbnailCts?.Dispose();
                _thumbnailCts = new CancellationTokenSource();
                var token = _thumbnailCts.Token;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                // 先给占位图：取第一个文件的 ICONONLY，保证列表不会是空白
                ImageSource? placeholder = null;
                if (filePaths.Count > 0)
                {
                    placeholder = ExtractShellImage(filePaths[0], ThumbnailSize, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK);
                }

                // 先把条目放到集合里（UI 立刻可见），随后后台逐个替换 Thumbnail
                var workItems = new System.Collections.Generic.List<(string path, VideoItem item)>(filePaths.Count);
                foreach (var file in filePaths)
                {
                    var info = new FileInfo(file);
                    var item = new VideoItem
                    {
                        Name = info.Name,
                        FullName = info.FullName,
                        Size = FormatSize(info.Length),
                        LastWriteTime = info.LastWriteTime,
                        Thumbnail = placeholder
                    };

                    Videos.Add(item);
                    workItems.Add((info.FullName, item));
                }

                // 并发限制：一次最多 4 个缩略图提取任务
                var semaphore = new SemaphoreSlim(4);
                foreach (var (path, item) in workItems)
                {
                    var localPath = path;
                    var localItem = item;

                    _ = Task.Run(async () =>
                    {
                        var acquired = false;
                        try
                        {
                            await semaphore.WaitAsync(token);
                            acquired = true;
                            if (token.IsCancellationRequested || myVersion != _loadVersion)
                                return;

                            var thumb = ExtractShellThumbnail(localPath);
                            if (token.IsCancellationRequested || myVersion != _loadVersion)
                                return;

                            await dispatcher.InvokeAsync(() =>
                            {
                                localItem.Thumbnail = thumb;
                            }, DispatcherPriority.Background);
                        }
                        catch (OperationCanceledException)
                        {
                            // 忽略取消
                        }
                        catch
                        {
                            // 忽略单个文件的失败，避免任务整体中断
                        }
                        finally
                        {
                            if (acquired)
                                semaphore.Release();
                        }
                    }, token);
                }
            }
            catch (Exception)
            {
                // 可根据需要添加日志或错误提示
            }
        }

        private const int ThumbnailSize = 96;

        private static ImageSource? ExtractShellThumbnail(string filePath)
        {
            // 先尝试拿缩略图；如果该文件没有缩略图，再回退到图标（Explorer 行为类似）。
            var thumb = ExtractShellImage(filePath, ThumbnailSize, SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK);
            if (thumb != null)
                return thumb;

            return ExtractShellImage(filePath, ThumbnailSize, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK);
        }

        private static ImageSource? ExtractShellImage(string filePath, int size, uint flags)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                var iid = IID_IShellItemImageFactory;
                var hrFactory = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iid, out var factory);
                if (hrFactory != 0 || factory == null)
                    return null;

                var sz = new SIZE { cx = size, cy = size };
                var hr = factory.GetImage(sz, flags, out hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero)
                    return null;

                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
            }
        }

        private static string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
            return $"{bytes} B";
        }

        public class VideoItem : BindableBase
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Size { get; set; }
            public DateTime LastWriteTime { get; set; }
            private ImageSource? _thumbnail;

            public ImageSource? Thumbnail
            {
                get => _thumbnail;
                set => SetProperty(ref _thumbnail, value);
            }
        }

        private static readonly Guid IID_IShellItemImageFactory =
            new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        private const uint SIIGBF_BIGGERSIZEOK = 0x00000001;
        private const uint SIIGBF_ICONONLY = 0x00000004;
        private const uint SIIGBF_THUMBNAILONLY = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, uint flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
