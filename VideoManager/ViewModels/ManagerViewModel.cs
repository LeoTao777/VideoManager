using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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

        private string _selectedFolderPath = string.Empty;

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
                    // 浠呯敤浜庨珮浜€夋嫨锛屼笉瑙﹀彂鎾斁
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
                    // PlaybackUri 鍙樺寲鏃讹紝鎾斁鐘舵€佷竴鑸渶瑕佸悓姝ュ埛鏂?
                    if (value == null)
                    {
                        IsPlaying = false;
                        IsPaused = false;
                    }
                }
            }
        }

        private string? _currentVideoPath;

        public string? CurrentVideoPath
        {
            get => _currentVideoPath;
            set => SetProperty(ref _currentVideoPath, value);
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
            TryRestoreSavedFolder();
        }

        public void StartPlayback(VideoItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FullName))
                return;

            try
            {
                PlaybackUri = new Uri(item.FullName);
                CurrentVideoPath = item.FullName;
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
            CurrentVideoPath = null;
            IsPlaying = false;
            IsPaused = false;
            VideoPosition = TimeSpan.Zero;
            VideoDuration = TimeSpan.Zero;
        }

        private void ExecuteSelectFolder()
        {
            // 浣跨敤 Win32 鍘熺敓瀵硅瘽妗嗛€夋嫨鏂囦欢澶癸紝閬垮厤渚濊禆 System.Windows.Forms
            var selectedPath = BrowseForFolder("閫夋嫨鍖呭惈瑙嗛鏂囦欢鐨勬枃浠跺す");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (!Directory.Exists(selectedPath))
                return;

            SelectedFolderPath = selectedPath;
            SaveSelectedFolder(selectedPath);
            LoadVideosFromFolder(selectedPath);
        }
        private void TryRestoreSavedFolder()
        {
            var savedFolder = TryLoadSavedFolder();
            if (string.IsNullOrWhiteSpace(savedFolder) || !Directory.Exists(savedFolder))
            {
                SelectedFolderPath = string.Empty;
                Videos.Clear();
                return;
            }

            SelectedFolderPath = savedFolder;
            LoadVideosFromFolder(savedFolder);
        }

        private static string? TryLoadSavedFolder()
        {
            try
            {
                var settingsFilePath = GetSettingsFilePath();
                if (!File.Exists(settingsFilePath))
                    return null;

                var json = File.ReadAllText(settingsFilePath);
                var settings = JsonSerializer.Deserialize<FolderSettings>(json);
                return settings?.SelectedFolderPath;
            }
            catch
            {
                return null;
            }
        }

        private static void SaveSelectedFolder(string folderPath)
        {
            try
            {
                var settingsFilePath = GetSettingsFilePath();
                var settingsDir = Path.GetDirectoryName(settingsFilePath);
                if (!string.IsNullOrWhiteSpace(settingsDir))
                    Directory.CreateDirectory(settingsDir);

                var settings = new FolderSettings
                {
                    SelectedFolderPath = folderPath
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(settingsFilePath, json);
            }
            catch
            {
                // ignore save errors
            }
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "setting", "selected-folder.json");
        }

        private class FolderSettings
        {
            public string? SelectedFolderPath { get; set; }
        }
        private static string? BrowseForFolder(string description)
        {
            // 鍏佽鐢ㄦ埛閫夋嫨鈥滅洰褰曗€濓紝骞跺敖閲忎娇鐢ㄦ柊鐨勬牱寮?
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
                return null; // 鐢ㄦ埛鍙栨秷

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

                // 涓洪伩鍏嶇敤鎴峰揩閫熷垏鎹㈢洰褰曟椂鈥滄棫浠诲姟瑕嗙洊鏂颁换鍔♀€濓紝鐢ㄧ増鏈彿鍋氫繚鎶?
                var myVersion = Interlocked.Increment(ref _loadVersion);
                _thumbnailCts?.Cancel();
                _thumbnailCts?.Dispose();
                _thumbnailCts = new CancellationTokenSource();
                var token = _thumbnailCts.Token;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                // 鍏堢粰鍗犱綅鍥撅細鍙栫涓€涓枃浠剁殑 ICONONLY锛屼繚璇佸垪琛ㄤ笉浼氭槸绌虹櫧
                ImageSource? placeholder = null;
                if (filePaths.Count > 0)
                {
                    placeholder = ExtractShellImage(filePaths[0], ThumbnailSize, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK);
                }

                // 鍏堟妸鏉＄洰鏀惧埌闆嗗悎閲岋紙UI 绔嬪埢鍙锛夛紝闅忓悗鍚庡彴閫愪釜鏇挎崲 Thumbnail
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

                // 骞跺彂闄愬埗锛氫竴娆℃渶澶?4 涓缉鐣ュ浘鎻愬彇浠诲姟
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
                            // 蹇界暐鍙栨秷
                        }
                        catch
                        {
                            // 蹇界暐鍗曚釜鏂囦欢鐨勫け璐ワ紝閬垮厤浠诲姟鏁翠綋涓柇
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
                // 鍙牴鎹渶瑕佹坊鍔犳棩蹇楁垨閿欒鎻愮ず
            }
        }

        private const int ThumbnailSize = 96;

        private static ImageSource? ExtractShellThumbnail(string filePath)
        {
            // 鍏堝皾璇曟嬁缂╃暐鍥撅紱濡傛灉璇ユ枃浠舵病鏈夌缉鐣ュ浘锛屽啀鍥為€€鍒板浘鏍囷紙Explorer 琛屼负绫讳技锛夈€?
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


