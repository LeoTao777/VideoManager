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
using System.Windows.Shapes;
using System.ComponentModel;
using System.Windows.Threading;
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

        public ManagerView()
        {
            InitializeComponent();
            DataContext = new ManagerViewModel();
            _vm = DataContext as ManagerViewModel;

            if (_vm != null)
            {
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
                _progressTimer?.Stop();
                VideoPlayer.Stop();
                VideoPlayer.Source = null;
                return;
            }

            _isMediaOpened = false;
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
