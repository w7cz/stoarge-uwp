using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
namespace XboxStorageAnalyzer
{
    /// Tries every drive letter root and "<drive>:\DevelopmentFiles". Nothing is assumed: each hit is a real StorageFolder,
    /// each Access Denied is reported as such, and non-existent paths are skipped.
    public static class DevFilesService
    {
        public static async Task<List<Row>> ProbeAsync()
        {
            var rows = new List<Row>();
            for (char c = 'A'; c <= 'Z'; c++)
            {
                string root = c + ":\\";
                foreach (var path in new[] { root, root + "DevelopmentFiles" })
                {
                    try { var f = await StorageFolder.GetFolderFromPathAsync(path); rows.Add(new Row { Name = path, Kind = "Accessible", Note = "Opened via StorageFolder.GetFolderFromPathAsync", Item = f }); }
                    catch (UnauthorizedAccessException) { rows.Add(new Row { Name = path, Kind = "Denied", Note = "Access Denied / Permission Required (path exists, sandbox blocks it)" }); }
                    catch (Exception) { /* path does not exist or is not mapped */ }
                }
            }
            return rows;
        }
    }
}
