using System;
using System.Collections.Generic;
namespace XboxStorageAnalyzer
{
    public sealed class ScanProgress { public long Files; public long Bytes; }
    public sealed class FileEntry { public string Path; public long Size; }
    public sealed class ScanResult
    {
        public string Root;
        public long TotalBytes, FileCount;
        public Dictionary<string, long> FolderSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase); // cumulative, folders that contain files
        public List<FileEntry> TopFiles = new List<FileEntry>();
        public List<string> Errors = new List<string>();
        public bool Cancelled; public TimeSpan Elapsed;
    }
    public sealed class Row
    {
        public string Name { get; set; } public string Kind { get; set; } public string SizeText { get; set; }
        public string Note { get; set; } public double Percent { get; set; } public long Size { get; set; } = -1;
        public DateTimeOffset Modified { get; set; } public object Item { get; set; }
    }
    public static class Fmt
    {
        public static string Bytes(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" }; double v = b; int i = 0;
            while (v >= 1024 && i < 4) { v /= 1024; i++; }
            return i == 0 ? b + " B" : v.ToString("0.##") + " " + u[i];
        }
    }
}
