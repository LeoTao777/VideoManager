using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using VideoManager.Services;

namespace VideoManager.Views.SubPages
{
    public partial class SnapshotGalleryWindow : Window
    {
        public string HeaderText { get; }
        public ObservableCollection<SnapshotGalleryItem> Records { get; } = new ObservableCollection<SnapshotGalleryItem>();

        public SnapshotGalleryWindow(string videoPath)
        {
            InitializeComponent();

            var records = SnapshotIndexService.GetRecordsByVideo(videoPath)
                .OrderBy(x => x.CaptureTimeMs)
                .ToList();

            var name = Path.GetFileName(videoPath);
            HeaderText = $"视频: {name}  (共 {records.Count} 张，按时间排序)";

            foreach (var record in records)
            {
                Records.Add(new SnapshotGalleryItem
                {
                    ImagePath = record.ImagePath,
                    CaptureTimeText = FormatTime(record.CaptureTimeMs),
                    FileName = Path.GetFileName(record.ImagePath)
                });
            }

            DataContext = this;
        }

        private static string FormatTime(long captureTimeMs)
        {
            var ts = TimeSpan.FromMilliseconds(captureTimeMs);
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss\.fff")
                : ts.ToString(@"mm\:ss\.fff");
        }

        private void SnapshotItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            var clickedItem = ResolveClickedItem(sender);
            if (clickedItem == null)
                return;

            OpenPreviewWindow(clickedItem);
        }

        private SnapshotGalleryItem? ResolveClickedItem(object sender)
        {
            if (sender is FrameworkElement element && element.DataContext is SnapshotGalleryItem item)
                return item;

            return null;
        }

        private void OpenPreviewWindow(SnapshotGalleryItem clickedItem)
        {
            if (Records.Count == 0)
                return;
            if (string.IsNullOrWhiteSpace(clickedItem.ImagePath))
                return;

            var startIndex = FindStartIndex(clickedItem);
            if (startIndex < 0)
                return;

            var items = Records.ToList();
            var preview = new SnapshotPreviewWindow(items, startIndex)
            {
                Owner = this
            };
            preview.ShowDialog();
        }

        private int FindStartIndex(SnapshotGalleryItem clickedItem)
        {
            for (var i = 0; i < Records.Count; i++)
            {
                if (ReferenceEquals(Records[i], clickedItem))
                    return i;
            }

            for (var i = 0; i < Records.Count; i++)
            {
                if (string.Equals(Records[i].ImagePath, clickedItem.ImagePath, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }

    public sealed class SnapshotGalleryItem
    {
        public string ImagePath { get; set; } = string.Empty;
        public string CaptureTimeText { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
