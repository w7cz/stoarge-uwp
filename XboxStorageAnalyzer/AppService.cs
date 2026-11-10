using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;
namespace XboxStorageAnalyzer
{
    public static class AppService
    {
        public static async Task<List<Row>> DiscoverAsync(IProgress<ScanProgress> prog, CancellationToken ct, List<string> notes)
        {
            var rows = new List<Row>();
            // 1) This app: the only package whose LocalState/LocalCache/TempState/RoamingState we can truly measure.
            var ad = ApplicationData.Current;
            var parts = new (string, StorageFolder)[] { ("LocalState", ad.LocalFolder), ("LocalCache", ad.LocalCacheFolder), ("TempState", ad.TemporaryFolder), ("RoamingState", ad.RoamingFolder) };
            long total = 0; var sb = new System.Text.StringBuilder();
            foreach (var (n, f) in parts)
            {
                var r = await ScanEngine.ScanAsync(f, prog, ct, true); total += r.TotalBytes;
                sb.Append(n + ": " + Fmt.Bytes(r.TotalBytes) + (r.Errors.Count > 0 ? " (" + r.Errors.Count + " unreadable)" : "") + "   ");
            }
            string inst = ""; try { inst = Package.Current.InstalledLocation.Path; } catch { inst = "unavailable"; }
            rows.Add(new Row { Name = Package.Current.DisplayName + "  (this app)", Kind = "Measured", Size = total, SizeText = Fmt.Bytes(total),
                Note = Package.Current.Id.FullName + "\nInstall: " + inst + "\n" + sb });

            // 2) Other packages: PackageManager is a restricted capability and usually unavailable to Xbox UWP apps.
            try
            {
                var pm = new PackageManager();
                foreach (var p in pm.FindPackagesForUser("").Where(x => x.Id.FullName != Package.Current.Id.FullName))
                {
                    ct.ThrowIfCancellationRequested();
                    string name; try { name = p.DisplayName; if (string.IsNullOrEmpty(name)) name = p.Id.Name; } catch { name = p.Id.Name; }
                    var row = new Row { Name = name, Kind = "Package", SizeText = "Unavailable" };
                    string path = null; try { path = p.InstalledLocation.Path; } catch { }
                    string note = p.Id.FullName + "\nInstall: " + (path ?? "Access Denied") + "\nLocalState/LocalCache/TempState/RoamingState: Access Denied (private to package)";
                    if (path != null)
                        try { var r = await ScanEngine.ScanAsync(await StorageFolder.GetFolderFromPathAsync(path), prog, ct); row.Size = r.TotalBytes; row.SizeText = Fmt.Bytes(r.TotalBytes) + " (install dir)"; }
                        catch (UnauthorizedAccessException) { note += "\nInstall folder size: Access Denied"; }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { note += "\nInstall folder size: Unavailable (" + ex.Message + ")"; }
                    row.Note = note; rows.Add(row);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { notes.Add("PackageManager unavailable (" + ex.GetType().Name + "): Permission Required (packageManagement is restricted on Xbox). Only this app can be measured."); }
            return rows.OrderByDescending(r => r.Size).ToList();
        }
    }
}
