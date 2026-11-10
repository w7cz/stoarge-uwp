using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace XboxStorageAnalyzer
{
    public sealed partial class MainPage : Page
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        readonly Stack<StorageFolder> nav = new Stack<StorageFolder>();
        StorageFolder cur; List<Row> rows = new List<Row>();
        public MainPage() { InitializeComponent(); Loaded += async (s, e) => { await LoadRootsAsync(false); Ring.IsActive = false; await RefreshDashboard(); }; }

        async Task<ScanResult> Scan(StorageFolder f, bool force = false)
        {
            Ring.IsActive = true;
            var prog = new Progress<ScanProgress>(p => Status.Text = "Scanning " + f.Name + ": " + p.Files.ToString("N0") + " files, " + Fmt.Bytes(p.Bytes) + " (total unknown until finished)");
            try { return await ScanEngine.ScanAsync(f, prog, cts.Token, force); } finally { Ring.IsActive = false; }
        }
        void Cancel_Click(object s, RoutedEventArgs e) { cts.Cancel(); cts = new CancellationTokenSource(); Status.Text = "Cancelled."; }
        async void Refresh_Click(object s, RoutedEventArgs e) { await RefreshDashboard(true); }

        async Task RefreshDashboard(bool force = false)
        {
            var (cap, free) = await VolumeService.GetAsync(ApplicationData.Current.LocalFolder);
            if (cap.HasValue && free.HasValue && cap > 0)
            {
                ulong used = cap.Value - free.Value;
                VolText.Text = "Total " + Fmt.Bytes((long)cap.Value) + "   Used " + Fmt.Bytes((long)used) + "   Free " + Fmt.Bytes((long)free.Value) + "   (" + (100.0 * used / cap.Value).ToString("0.0") + "% used)";
                VolBar.Value = 100.0 * used / cap.Value;
                VolSrc.Text = "Source: system API (System.Capacity / System.FreeSpace) for the volume holding this app's LocalState.";
            }
            else { VolText.Text = "Device capacity: unavailable"; VolBar.Value = 0; VolSrc.Text = "The system API did not expose System.Capacity/System.FreeSpace on this device; no estimate is shown."; }
            var results = new List<ScanResult>();
            foreach (var f in new[] { ApplicationData.Current.LocalFolder, ApplicationData.Current.LocalCacheFolder, ApplicationData.Current.TemporaryFolder, ApplicationData.Current.RoamingFolder })
            { var r = await Scan(f, force); if (r.Cancelled) return; results.Add(r); }
            long max = results.SelectMany(r => r.FolderSizes.Values).DefaultIfEmpty(1).Max();
            TopDirs.ItemsSource = results.SelectMany(r => r.FolderSizes).OrderByDescending(k => k.Value).Take(15)
                .Select(k => new Row { Name = k.Key.Substring(k.Key.LastIndexOf('\\') + 1), Note = k.Key, Kind = "Folder", Size = k.Value, SizeText = Fmt.Bytes(k.Value), Percent = 100.0 * k.Value / max }).ToList();
            var tf = results.SelectMany(r => r.TopFiles).OrderByDescending(x => x.Size).Take(15).ToList();
            TopFiles.ItemsSource = tf.Select(x => new Row { Name = System.IO.Path.GetFileName(x.Path), Note = x.Path, Kind = "File", Size = x.Size, SizeText = Fmt.Bytes(x.Size), Percent = 100.0 * x.Size / Math.Max(1, tf[0].Size) }).ToList();
            Status.Text = "Measured " + results.Sum(r => r.FileCount).ToString("N0") + " files / " + Fmt.Bytes(results.Sum(r => r.TotalBytes)) + " in app-accessible folders; errors: " + results.Sum(r => r.Errors.Count);
        }

        // ---------- roots / USB ----------
        List<Row> probeRows = new List<Row>();
        async Task LoadRootsAsync(bool usb)
        {
            var list = new List<Row>();
            void Add(string n, string kind, StorageFolder f, string note = null) => list.Add(new Row { Name = n, Kind = kind, Item = f, Note = note ?? f.Path });
            var ad = ApplicationData.Current;
            Add("LocalState", "App", ad.LocalFolder); Add("LocalCache", "App", ad.LocalCacheFolder); Add("TempState", "App", ad.TemporaryFolder); Add("RoamingState", "App", ad.RoamingFolder);
            try { Add("Install dir (read-only)", "App", Windows.ApplicationModel.Package.Current.InstalledLocation); } catch { }
            foreach (var e in StorageApplicationPermissions.FutureAccessList.Entries)
                try { var f = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(e.Token); Add(f.DisplayName, "Granted", f); } catch { }
            if (usb)
                try
                {
                    var devs = await KnownFolders.RemovableDevices.GetFoldersAsync();
                    if (devs.Count == 0) Status.Text = "No removable storage visible to this app (needs removableStorage capability + declared file types, and a drive attached).";
                    foreach (var d in devs)
                    {
                        var (c, fr) = await VolumeService.GetAsync(d);
                        string cap = c.HasValue ? "Capacity " + Fmt.Bytes((long)c.Value) + (fr.HasValue ? ", free " + Fmt.Bytes((long)fr.Value) + ", used " + Fmt.Bytes((long)(c.Value - fr.Value)) : ", free unavailable") : "Capacity unavailable";
                        Add(d.DisplayName, "USB", d, cap);
                    }
                }
                catch (UnauthorizedAccessException) { Status.Text = "Permission Required: removableStorage capability not granted."; }
                catch (Exception ex) { Status.Text = "USB enumeration unsupported: " + ex.Message; }
            list.AddRange(probeRows); RootList.ItemsSource = list;
        }
        async void Probe_Click(object s, RoutedEventArgs e)
        {
            Ring.IsActive = true; probeRows = await DevFilesService.ProbeAsync(); Ring.IsActive = false;
            await LoadRootsAsync(false); Status.Text = "Probe done: " + probeRows.Count + " path(s) exist (accessible or denied). If none are accessible use 'Pick folder / drive'.";
        }
        async void Usb_Click(object s, RoutedEventArgs e) { await LoadRootsAsync(true); }
        async void AddFolder_Click(object s, RoutedEventArgs e)
        {
            var p = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder }; p.FileTypeFilter.Add("*");
            var f = await p.PickSingleFolderAsync();
            if (f != null) { StorageApplicationPermissions.FutureAccessList.AddOrReplace(f.Path.Length > 0 ? f.Path : f.DisplayName, f); await LoadRootsAsync(false); }
        }
        async void Root_Click(object s, ItemClickEventArgs e)
        {
            if (!(((Row)e.ClickedItem).Item is StorageFolder f)) { Status.Text = "Not accessible: " + ((Row)e.ClickedItem).Name; return; }
            nav.Clear(); Tabs.SelectedIndex = 1;
            await Open(f);
        }

        // ---------- explorer ----------
        async Task Open(StorageFolder f)
        {
            cur = f; PathText.Text = string.IsNullOrEmpty(f.Path) ? f.DisplayName : f.Path; var list = new List<Row>(); int dirs = 0, files = 0;
            try
            {
                uint i = 0; IReadOnlyList<IStorageItem> page;
                while ((page = await f.GetItemsAsync(i, 500)).Count > 0)
                {
                    i += (uint)page.Count;
                    foreach (var it in page)
                    {
                        string hid = await IsHiddenAsync(it) ? " [Hidden]" : "";
                        if (it is StorageFolder sf) { dirs++; bool k = ScanEngine.TryFolderSize(sf.Path, out long sz); list.Add(new Row { Name = sf.Name + hid, Kind = "Folder", Item = sf, Size = k ? sz : -1, SizeText = k ? Fmt.Bytes(sz) : "not scanned", Modified = sf.DateCreated }); }
                        else if (it is StorageFile fl) { files++; long sz = -1; DateTimeOffset m = fl.DateCreated; try { var bp = await fl.GetBasicPropertiesAsync(); sz = (long)bp.Size; m = bp.DateModified; } catch { }
                            list.Add(new Row { Name = fl.Name + hid, Kind = "File", Item = fl, Size = sz, SizeText = sz >= 0 ? Fmt.Bytes(sz) : "Unavailable", Modified = m }); }
                    }
                }
                Status.Text = dirs + " folders, " + files + " files (direct children)";
            }
            catch (UnauthorizedAccessException) { Status.Text = "Access Denied: " + f.Path; }
            catch (Exception ex) { Status.Text = "Unavailable: " + ex.Message; }
            rows = list; Resort();
        }
        // Windows.Storage.FileAttributes has no Hidden flag; read the Win32 attribute through the shell property instead.
        static async Task<bool> IsHiddenAsync(IStorageItem it)
        {
            try
            {
                var props = it as IStorageItemProperties;
                if (props == null) return false;
                var d = await props.Properties.RetrievePropertiesAsync(new[] { "System.FileAttributes" });
                object o;
                if (d.TryGetValue("System.FileAttributes", out o) && o is uint) return (((uint)o) & 0x2) != 0;
            }
            catch (Exception) { }
            return false;
        }
        void Resort()
        {
            IEnumerable<Row> r = rows;
            switch (SortBox.SelectedIndex) { case 1: r = r.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase); break; case 2: r = r.OrderByDescending(x => x.Modified); break; default: r = r.OrderByDescending(x => x.Size); break; }
            var l = r.ToList(); long max = Math.Max(1, l.Select(x => x.Size).DefaultIfEmpty(1).Max()); foreach (var x in l) x.Percent = x.Size > 0 ? 100.0 * x.Size / max : 0;
            Items.ItemsSource = l;
        }
        void Sort_Changed(object s, SelectionChangedEventArgs e) { if (Items != null) Resort(); }
        async void Items_Click(object s, ItemClickEventArgs e)
        {
            if (((Row)e.ClickedItem).Item is StorageFolder f) { if (cur != null) nav.Push(cur); await Open(f); }
        }
        async void Up_Click(object s, RoutedEventArgs e) { if (nav.Count > 0) await Open(nav.Pop()); }
        async void ScanHere_Click(object s, RoutedEventArgs e)
        {
            if (cur == null) return; var r = await Scan(cur, true); if (r.Cancelled) return;
            await Open(cur);
            Status.Text = "Scanned " + r.FileCount.ToString("N0") + " files, " + Fmt.Bytes(r.TotalBytes) + " in " + r.Elapsed.TotalSeconds.ToString("0.0") + "s; " + r.FolderSizes.Count + " folders contain files; " + r.Errors.Count + " unreadable items.";
        }

        // ---------- apps ----------
        async void Apps_Click(object s, RoutedEventArgs e)
        {
            var notes = new List<string>(); Ring.IsActive = true;
            var prog = new Progress<ScanProgress>(p => Status.Text = "Measuring… " + p.Files.ToString("N0") + " files");
            try
            {
                var rs = await AppService.DiscoverAsync(prog, cts.Token, notes); long max = Math.Max(1, rs.Max(r => r.Size));
                foreach (var r in rs) r.Percent = r.Size > 0 ? 100.0 * r.Size / max : 0;
                AppList.ItemsSource = rs; AppNote.Text = string.Join("\n", notes); Status.Text = rs.Count + " package(s) listed.";
            }
            catch (OperationCanceledException) { Status.Text = "Cancelled."; }
            finally { Ring.IsActive = false; }
        }
    }
}
