using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VideoManager.Views.SubPages
{
    public partial class SnapshotPreviewWindow : Window
    {
        private const double ZoomStep = 1.2;
        private const double MinZoom = 0.2;
        private const double MaxZoom = 8.0;

        private readonly List<SnapshotGalleryItem> _items;
        private int _currentIndex;

        private double _zoomScale = 1.0;
        private Vector _panOffset;
        private bool _isDragging;
        private Point _dragStartPoint;
        private Vector _dragStartOffset;
        private bool _hasValidImage;

        public SnapshotPreviewWindow(IEnumerable<SnapshotGalleryItem> items, int startIndex)
        {
            InitializeComponent();

            _items = items?.ToList() ?? new List<SnapshotGalleryItem>();
            if (_items.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            _currentIndex = NormalizeIndex(startIndex);
            UpdateCurrentItem();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                ShowPrevious();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                ShowNext();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }

            base.OnPreviewKeyDown(e);
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPrevious();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNext();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PreviewViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_hasValidImage)
                return;

            var zoomFactor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
            var oldZoom = _zoomScale;
            var newZoom = Math.Clamp(oldZoom * zoomFactor, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.0001)
                return;

            var position = e.GetPosition(PreviewHost);
            _panOffset = new Vector(
                position.X - ((position.X - _panOffset.X) / oldZoom) * newZoom,
                position.Y - ((position.Y - _panOffset.Y) / oldZoom) * newZoom);

            _zoomScale = newZoom;
            if (Math.Abs(_zoomScale - 1.0) < 0.0001)
            {
                _zoomScale = 1.0;
                _panOffset = default;
            }

            ApplyTransform();
            e.Handled = true;
        }

        private void PreviewViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_hasValidImage || _zoomScale <= 1.0)
                return;

            _isDragging = true;
            _dragStartPoint = e.GetPosition(PreviewHost);
            _dragStartOffset = _panOffset;
            PreviewViewport.CaptureMouse();
            PreviewViewport.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void PreviewViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            var current = e.GetPosition(PreviewHost);
            var delta = current - _dragStartPoint;
            _panOffset = _dragStartOffset + (Vector)delta;
            ApplyTransform();
            e.Handled = true;
        }

        private void PreviewViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopDragging();
        }

        private void PreviewViewport_MouseLeave(object sender, MouseEventArgs e)
        {
            StopDragging();
        }

        private void ShowPrevious()
        {
            if (_items.Count == 0)
                return;

            _currentIndex = (_currentIndex - 1 + _items.Count) % _items.Count;
            UpdateCurrentItem();
        }

        private void ShowNext()
        {
            if (_items.Count == 0)
                return;

            _currentIndex = (_currentIndex + 1) % _items.Count;
            UpdateCurrentItem();
        }

        private void UpdateCurrentItem()
        {
            if (_items.Count == 0)
                return;

            ResetViewTransform();

            var item = _items[_currentIndex];
            Title = $"截图预览 ({_currentIndex + 1}/{_items.Count})";
            CaptureTimeTextBlock.Text = string.IsNullOrWhiteSpace(item.CaptureTimeText)
                ? string.Empty
                : $"时间: {item.CaptureTimeText}";
            FileNameTextBlock.Text = item.FileName ?? string.Empty;

            if (TryCreateBitmap(item.ImagePath, out var bitmap))
            {
                _hasValidImage = true;
                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                PreviewFallbackText.Visibility = Visibility.Collapsed;
                return;
            }

            _hasValidImage = false;
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewFallbackText.Text = BuildFallbackText(item.ImagePath);
            PreviewFallbackText.Visibility = Visibility.Visible;
        }

        private void ShowEmptyState()
        {
            _hasValidImage = false;
            Title = "截图预览";
            CaptureTimeTextBlock.Text = string.Empty;
            FileNameTextBlock.Text = string.Empty;
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewFallbackText.Text = "没有可预览的截图";
            PreviewFallbackText.Visibility = Visibility.Visible;
            ResetViewTransform();
        }

        private void ResetViewTransform()
        {
            _zoomScale = 1.0;
            _panOffset = default;
            ApplyTransform();
            StopDragging();
        }

        private void ApplyTransform()
        {
            ZoomScaleTransform.ScaleX = _zoomScale;
            ZoomScaleTransform.ScaleY = _zoomScale;
            PanTranslateTransform.X = _panOffset.X;
            PanTranslateTransform.Y = _panOffset.Y;
        }

        private void StopDragging()
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            if (PreviewViewport.IsMouseCaptured)
                PreviewViewport.ReleaseMouseCapture();
            PreviewViewport.Cursor = Cursors.Arrow;
        }

        private static bool TryCreateBitmap(string? imagePath, out BitmapImage? bitmap)
        {
            bitmap = null;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return false;

            try
            {
                var uri = new Uri(imagePath, UriKind.Absolute);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = uri;
                image.EndInit();
                image.Freeze();
                bitmap = image;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildFallbackText(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return "图片路径无效";
            if (!File.Exists(imagePath))
                return "图片不存在或已被删除";
            return "图片加载失败";
        }

        private int NormalizeIndex(int index)
        {
            if (_items.Count == 0)
                return 0;

            var normalized = index % _items.Count;
            if (normalized < 0)
                normalized += _items.Count;

            return normalized;
        }
    }
}
