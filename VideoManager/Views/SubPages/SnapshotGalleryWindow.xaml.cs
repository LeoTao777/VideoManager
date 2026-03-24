using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    }

    public sealed class SnapshotGalleryItem
    {
        public string ImagePath { get; set; } = string.Empty;
        public string CaptureTimeText { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
