using System;
using System.IO;
using System.Text.Json;
using VideoManager.Services;

namespace VideoManager.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private string _defaultFolder = "未配置";
        public string DefaultFolder
        {
            get => _defaultFolder;
            set => SetProperty(ref _defaultFolder, value);
        }

        private string _snapshotFolder = "未配置";
        public string SnapshotFolder
        {
            get => _snapshotFolder;
            set => SetProperty(ref _snapshotFolder, value);
        }

        private string _defaultFolderStatus = "尚未选择默认文件夹";
        public string DefaultFolderStatus
        {
            get => _defaultFolderStatus;
            set => SetProperty(ref _defaultFolderStatus, value);
        }

        private string _snapshotFolderStatus = string.Empty;
        public string SnapshotFolderStatus
        {
            get => _snapshotFolderStatus;
            set => SetProperty(ref _snapshotFolderStatus, value);
        }

        public string SettingsFilePath => GetSettingsFilePath();
        public string SnapshotIndexFilePath => Path.Combine(SnapshotIndexService.SnapshotRoot, "snapshot-index.json");

        public SettingsViewModel()
        {
            Reload();
        }

        public void Reload()
        {
            DefaultFolder = "未配置";
            DefaultFolderStatus = "尚未选择默认文件夹";
            SnapshotFolder = SnapshotIndexService.SnapshotRoot;

            var savedFolder = TryLoadSavedFolder();
            if (!string.IsNullOrWhiteSpace(savedFolder))
            {
                DefaultFolder = savedFolder;
                DefaultFolderStatus = Directory.Exists(savedFolder)
                    ? "目录可用"
                    : "目录不存在或不可访问";
            }

            SnapshotFolderStatus = Directory.Exists(SnapshotFolder)
                ? "目录可用"
                : "目录尚未创建（首次截图会自动创建）";
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

        private static string GetSettingsFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "setting", "selected-folder.json");
        }

        private sealed class FolderSettings
        {
            public string? SelectedFolderPath { get; set; }
        }
    }
}
