using System;
using System.Threading.Tasks;
using Windows.Storage;
namespace XboxStorageAnalyzer
{
    public static class VolumeService
    {
        /// Values come only from the shell properties System.Capacity / System.FreeSpace; null = the API did not expose it.
        public static async Task<(ulong? Capacity, ulong? Free)> GetAsync(StorageFolder f)
        {
            try
            {
                var d = await f.Properties.RetrievePropertiesAsync(new[] { "System.Capacity", "System.FreeSpace" });
                object co, fo;
                d.TryGetValue("System.Capacity", out co);
                d.TryGetValue("System.FreeSpace", out fo);
                ulong? c = co is ulong ? (ulong)co : (ulong?)null;
                ulong? fr = fo is ulong ? (ulong)fo : (ulong?)null;
                return (c, fr);
            }
            catch (Exception) { return (null, null); }
        }
    }
}
