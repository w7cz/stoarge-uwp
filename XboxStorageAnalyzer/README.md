# Xbox Storage Analyzer (UWP, Dev Mode) — setup
Not compiled by the author's environment; build in Visual Studio 2019/2022 on Windows.
1. New project: "Blank App (Universal Windows)" named XboxStorageAnalyzer, min/target Windows 10 1809+ (17763) or newer.
2. Delete the generated App.xaml(.cs)/MainPage.xaml(.cs); add these files.
3. Package.appxmanifest: add `<Capabilities>` and extension below; Target device family Windows.Universal (runs on Xbox).
4. Settings: Release/Debug x64, deploy to Xbox Dev Mode console (Remote Machine, device IP, PIN).
```xml
<Package xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap mp rescap">
 <Applications><Application ...><Extensions>
  <uap:Extension Category="windows.fileTypeAssociation"><uap:FileTypeAssociation Name="any">
   <uap:SupportedFileTypes><uap:FileType>.txt</uap:FileType><uap:FileType>.zip</uap:FileType><uap:FileType>.mp4</uap:FileType><uap:FileType>.png</uap:FileType><uap:FileType>.jpg</uap:FileType><uap:FileType>.json</uap:FileType></uap:SupportedFileTypes>
  </uap:FileTypeAssociation></uap:Extension></Extensions></Application></Applications>
 <Capabilities><Capability Name="internetClient"/><uap:Capability Name="removableStorage"/><rescap:Capability Name="broadFileSystemAccess"/><rescap:Capability Name="packageManagement"/></Capabilities>
</Package>
```
Remove any rescap capability the console rejects at deploy time.
## Real limits
- Other apps' LocalState etc. are not reachable from a UWP app; shown as Access Denied.
- Removable drives list only files of declared types (wildcards are not allowed); extend the list.
- PackageManager / broadFileSystemAccess often fail on Xbox; the app reports that, never guesses.
- Total size isn't known before a scan, so progress shows live counts, not a percentage.
