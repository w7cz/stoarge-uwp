using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
namespace XboxStorageAnalyzer
{
    /// Deep file query (OS-side traversal: no managed recursion, loop-safe, no indexer dependency),
    /// paged in batches; folder totals aggregated by walking each file's path upward.
    public static class ScanEngine
    {
        static readonly ConcurrentDictionary<string, ScanResult> Cache = new ConcurrentDictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);

        public static bool TryFolderSize(string path, out long size)
        {
            foreach (var r in Cache.Values)
                if (path.StartsWith(r.Root, StringComparison.OrdinalIgnoreCase) && r.FolderSizes.TryGetValue(path, out size)) return true;
            size = 0; return false;
        }

        public static async Task<ScanResult> ScanAsync(StorageFolder root, IProgress<ScanProgress> progress, CancellationToken ct, bool force = false)
        {
            string rp = root.Path.TrimEnd('\\');
            if (!force && Cache.TryGetValue(rp, out var cached)) return cached;
            var res = new ScanResult { Root = rp };
            var sw = Stopwatch.StartNew(); long lastReport = 0, minTop = 0;
            try
            {
                var qo = new QueryOptions(CommonFileQuery.DefaultQuery, null) { FolderDepth = FolderDepth.Deep, IndexerOption = IndexerOption.DoNotUseIndexer };
                qo.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[0]);
                var query = root.CreateFileQueryWithOptions(qo);
                uint idx = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    System.Collections.Generic.IReadOnlyList<StorageFile> files;
                    try { files = await query.GetFilesAsync(idx, 500).AsTask(ct); }
                    catch (UnauthorizedAccessException) { res.Errors.Add("Access Denied while enumerating at item " + idx); break; }
                    catch (Exception ex) when (!(ex is OperationCanceledException)) { res.Errors.Add("Enumeration failed: " + ex.Message); break; }
                    if (files.Count == 0) break;
                    idx += (uint)files.Count;
                    foreach (var f in files)
                    {
                        long size;
                        try { size = (long)(await f.GetBasicPropertiesAsync().AsTask(ct)).Size; }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception) { res.Errors.Add("Unreadable: " + f.Name); continue; }
                        string p = f.Path;
                        if (string.IsNullOrEmpty(p) || p.Length < rp.Length) { res.Errors.Add("No path: " + f.Name); continue; }
                        res.FileCount++; res.TotalBytes += size;
                        int i = p.LastIndexOf('\\'); string dir = i > 0 ? p.Substring(0, i) : rp;
                        while (dir.Length >= rp.Length)
                        {
                            res.FolderSizes.TryGetValue(dir, out long cur); res.FolderSizes[dir] = cur + size;
                            if (dir.Length == rp.Length) break;
                            int j = dir.LastIndexOf('\\'); if (j < rp.Length) break; dir = dir.Substring(0, j);
                        }
                        if (size > minTop || res.TopFiles.Count < 200)
                        {
                            res.TopFiles.Add(new FileEntry { Path = p, Size = size });
                            if (res.TopFiles.Count >= 400) { res.TopFiles = res.TopFiles.OrderByDescending(x => x.Size).Take(100).ToList(); minTop = res.TopFiles[99].Size; }
                        }
                    }
                    if (sw.ElapsedMilliseconds - lastReport > 250) { lastReport = sw.ElapsedMilliseconds; progress?.Report(new ScanProgress { Files = res.FileCount, Bytes = res.TotalBytes }); }
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) { res.Cancelled = true; }
            res.TopFiles = res.TopFiles.OrderByDescending(x => x.Size).Take(100).ToList();
            res.Elapsed = sw.Elapsed;
            progress?.Report(new ScanProgress { Files = res.FileCount, Bytes = res.TotalBytes });
            if (!res.Cancelled) Cache[rp] = res;
            return res;
        }
    }
}
