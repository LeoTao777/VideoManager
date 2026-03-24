using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using VideoManager.Services;
using VideoManager.ViewModels;

namespace VideoManager.Views.SubPages
{
    /// <summary>
    /// ManagerView.xaml 的交互逻辑
    /// </summary>
    public partial class ManagerView : UserControl
    {
        private ManagerViewModel? _vm;
        private DispatcherTimer? _progressTimer;
        private bool _isMediaOpened;
        private bool _isSeeking;

        public ManagerView()
        {
            InitializeComponent();
            DataContext = new ManagerViewModel();
            _vm = DataContext as ManagerViewModel;

            if (_vm != null)
            {
                SnapshotIndexService.EnsureStorage();
                _vm.PropertyChanged += ViewModelOnPropertyChanged;

                VideoPlayer.MediaOpened += VideoPlayerOnMediaOpened;
                VideoPlayer.MediaEnded += VideoPlayerOnMediaEnded;
                VideoPlayer.MediaFailed += VideoPlayerOnMediaFailed;

                _progressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, OnProgressTick, Dispatcher);
            }
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_vm == null)
                return;

            if (e.PropertyName == nameof(ManagerViewModel.PlaybackUri))
            {
                PlayFromPlaybackUri();
            }
            else if (e.PropertyName == nameof(ManagerViewModel.IsPaused))
            {
                ApplyPauseState();
            }
        }

        private void PlayFromPlaybackUri()
        {
            if (_vm == null)
                return;

            if (_vm.PlaybackUri == null)
            {
                _isMediaOpened = false;
                _isSeeking = false;
                SeekBar.Maximum = 1;
                SeekBar.Value = 0;
                _progressTimer?.Stop();
                VideoPlayer.Stop();
                VideoPlayer.Source = null;
                return;
            }

            _isMediaOpened = false;
            _isSeeking = false;
            SeekBar.Maximum = 1;
            SeekBar.Value = 0;
            _progressTimer?.Stop();
            VideoPlayer.Stop();
            VideoPlayer.Source = _vm.PlaybackUri;
            VideoPlayer.Play();
            // MediaOpened 后才设置 duration
            _progressTimer?.Start();
        }

        private void ApplyPauseState()
        {
            if (_vm == null)
                return;
            if (_vm.PlaybackUri == null)
                return;

            if (_vm.IsPaused)
                VideoPlayer.Pause();
            else
                VideoPlayer.Play();
        }

        private void VideoPlayerOnMediaOpened(object? sender, EventArgs e)
        {
            if (_vm == null)
                return;

            // NaturalDuration 可能在某些流上为 0，这里尽量保护
            _isMediaOpened = true;
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                _vm.VideoDuration = VideoPlayer.NaturalDuration.TimeSpan;
                SeekBar.Maximum = Math.Max(1, _vm.VideoDuration.TotalSeconds);
            }
        }

        private void VideoPlayerOnMediaEnded(object? sender, EventArgs e)
        {
            if (_vm == null)
                return;

            _vm.ExitPlaybackCommand.Execute();
        }

        private void VideoPlayerOnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            // 播放失败时也退出播放界面，避免卡在空白
            if (_vm == null)
                return;
            _vm.ExitPlaybackCommand.Execute();
        }

        private void OnProgressTick(object? sender, EventArgs e)
        {
            if (_vm == null)
                return;
            if (_vm.PlaybackUri == null)
                return;
            if (!_isMediaOpened && !VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                // duration 还没准备好时，至少推进 position
            }

            _vm.VideoPosition = VideoPlayer.Position;
            if (!_isSeeking)
            {
                SeekBar.Value = Math.Min(SeekBar.Maximum, Math.Max(0, VideoPlayer.Position.TotalSeconds));
            }
        }

        private void VideoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null)
                return;

            if (sender is ListView lv && lv.SelectedItem is ManagerViewModel.VideoItem item)
            {
                _vm.StartPlayback(item);
                return;
            }

            // 兜底：从点击位置取 DataContext
            var clicked = GetClickedItemDataContext(e.OriginalSource);
            if (clicked is ManagerViewModel.VideoItem clickedItem)
            {
                _vm.StartPlayback(clickedItem);
            }
        }

        private void SeekBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSeeking = true;
        }

        private void SeekBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null || _vm.PlaybackUri == null)
            {
                _isSeeking = false;
                return;
            }

            var target = TimeSpan.FromSeconds(Math.Max(0, Math.Min(SeekBar.Value, SeekBar.Maximum)));
            VideoPlayer.Position = target;
            _vm.VideoPosition = target;
            _isSeeking = false;
        }

        private async void SaveSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || string.IsNullOrWhiteSpace(_vm.CurrentVideoPath) || _vm.PlaybackUri == null)
                return;

            try
            {
                if (!_vm.IsPaused)
                {
                    _vm.IsPaused = true;
                }

                var videoPath = _vm.CurrentVideoPath;
                var capturePosition = VideoPlayer.Position;
                var outputPath = BuildOutputPath(videoPath, capturePosition);

                await Task.Run(() => CaptureFrameWithFfmpeg(videoPath, capturePosition, outputPath));

                SnapshotIndexService.AddRecord(new SnapshotRecord
                {
                    VideoPath = videoPath,
                    ImagePath = outputPath,
                    CaptureTimeMs = (long)Math.Max(0, capturePosition.TotalMilliseconds),
                    CreatedAtUtc = DateTime.UtcNow
                });

                MessageBox.Show($"截图已保存:\n{outputPath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"截图失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string BuildOutputPath(string videoPath, TimeSpan capturePosition)
        {
            SnapshotIndexService.EnsureStorage();
            var safeName = System.IO.Path.GetFileNameWithoutExtension(videoPath);
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }

            var pos = capturePosition;
            var fileName = $"{safeName}_{(int)pos.TotalHours:00}{pos.Minutes:00}{pos.Seconds:00}_{pos.Milliseconds:000}.png";
            return System.IO.Path.Combine(SnapshotIndexService.SnapshotRoot, fileName);
        }

        private static void CaptureFrameWithFfmpeg(string videoPath, TimeSpan capturePosition, string outputPath)
        {
            var timeArg = $"{(int)capturePosition.TotalHours:00}:{capturePosition.Minutes:00}:{capturePosition.Seconds:00}.{capturePosition.Milliseconds:000}";
            var args = $"-hide_banner -loglevel error -i \"{videoPath}\" -ss {timeArg} -frames:v 1 -vsync 0 \"{outputPath}\"";
            var ffmpegExe = ResolveFfmpegExecutable();

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 ffmpeg 进程。");
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "ffmpeg 执行失败。" : stderr.Trim());
            }
        }

        private static string ResolveFfmpegExecutable()
        {
            const string exeName = "ffmpeg.exe";

            // 1) 允许通过环境变量直接指定：FFMPEG_PATH=.../ffmpeg.exe 或 .../bin
            var env = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (!string.IsNullOrWhiteSpace(env))
            {
                var normalized = env.Trim().Trim('"');
                if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(normalized))
                {
                    return normalized;
                }

                var fromDir = Path.Combine(normalized, exeName);
                if (File.Exists(fromDir))
                {
                    return fromDir;
                }
            }

            // 2) 当前程序目录下（便于随程序打包）
            var baseDir = AppContext.BaseDirectory;
            var localCandidates = new[]
            {
                Path.Combine(baseDir, exeName),
                Path.Combine(baseDir, "tools", exeName),
                Path.Combine(baseDir, "ffmpeg", "bin", exeName)
            };
            foreach (var candidate in localCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 3) PATH 里查找
            var pathEnv = Environment.GetEnvironmentVariable("ffmpegPath");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                foreach (var dir in pathEnv.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(dir))
                        continue;
                    try
                    {
                        var candidate = Path.Combine(dir.Trim(), exeName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch
                    {
                        // ignore invalid PATH entries
                    }
                }
            }

            throw new FileNotFoundException(
                "未找到 ffmpeg.exe。请安装 FFmpeg 并将其加入 PATH，或设置环境变量 FFMPEG_PATH 指向 ffmpeg.exe/其所在目录。");
        }

        private void ViewSnapshotsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.DataContext as ManagerViewModel.VideoItem;
            if (item == null || string.IsNullOrWhiteSpace(item.FullName))
                return;

            var owner = Window.GetWindow(this);
            var gallery = new SnapshotGalleryWindow(item.FullName)
            {
                Owner = owner
            };
            gallery.ShowDialog();
        }

        private static object? GetClickedItemDataContext(object? originalSource)
        {
            var current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current is FrameworkElement fe)
                    return fe.DataContext;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
