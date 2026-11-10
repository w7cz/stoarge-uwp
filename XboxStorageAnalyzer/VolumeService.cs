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
                ulong? c = d.TryGetValue("System.Capacity", out var co) && co is ulong cu ? cu : (ulong?)null;
                ulong? fr = d.TryGetValue("System.FreeSpace", out var fo) && fo is ulong fu ? fu : (ulong?)null;
                return (c, fr);
            }
            catch { return (null, null); }
        }
    }
}
