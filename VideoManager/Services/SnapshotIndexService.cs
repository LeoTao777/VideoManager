using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VideoManager.Services
{
    public sealed class SnapshotRecord
    {
        public string VideoPath { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public long CaptureTimeMs { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public static class SnapshotIndexService
    {
        public const string SnapshotRoot = @"F:\testImage";
        private static readonly string IndexFilePath = Path.Combine(SnapshotRoot, "snapshot-index.json");
        private static readonly object SyncRoot = new object();

        public static void EnsureStorage()
        {
            Directory.CreateDirectory(SnapshotRoot);
            if (!File.Exists(IndexFilePath))
            {
                File.WriteAllText(IndexFilePath, "[]");
            }
        }

        public static void AddRecord(SnapshotRecord record)
        {
            lock (SyncRoot)
            {
                EnsureStorage();
                var all = LoadAllInternal();
                all.Add(record);
                SaveAllInternal(all);
            }
        }

        public static List<SnapshotRecord> GetRecordsByVideo(string videoPath)
        {
            lock (SyncRoot)
            {
                EnsureStorage();
                return LoadAllInternal()
                    .Where(x => string.Equals(x.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.CaptureTimeMs)
                    .ToList();
            }
        }

        private static List<SnapshotRecord> LoadAllInternal()
        {
            try
            {
                var json = File.ReadAllText(IndexFilePath);
                return JsonSerializer.Deserialize<List<SnapshotRecord>>(json) ?? new List<SnapshotRecord>();
            }
            catch
            {
                return new List<SnapshotRecord>();
            }
        }

        private static void SaveAllInternal(List<SnapshotRecord> records)
        {
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(IndexFilePath, json);
        }
    }
}
