using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Momonga.Updates;

public sealed record AvailableUpdate(Version Version, string Notes, string Url, long Size, string Sha256);

public static class UpdateService
{
    private const string Repository = "https://github.com/scenesua/chiikawa-companion/releases/download/";
    private static readonly HttpClient http = CreateClient();
    public static Version Current => System.Reflection.Assembly.GetEntryAssembly()!.GetName().Version!;
    public static string CurrentLabel => Current.ToString(3);
    public static string AssetName => OperatingSystem.IsMacOS()
        ? "Chiikawa-Companion-macOS-" + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "Apple-Silicon" : "Intel") + ".zip"
        : "Chiikawa-Companion-Windows-x64.zip";
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Chiikawa-Companion/"+CurrentLabel);
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        return client;
    }
    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(12));
        // These numeric releases also carry the macOS preview; /latest excludes them.
        var json = await http.GetStringAsync("https://api.github.com/repos/scenesua/chiikawa-companion/releases?per_page=20", timeout.Token);
        return Select(json, Current, AssetName);
    }
    internal static AvailableUpdate? Select(string json, Version current, string assetName)
    {
        using var document = JsonDocument.Parse(json);
        AvailableUpdate? newest = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || !Version.TryParse(release.GetProperty("tag_name").GetString()?.TrimStart('v'), out var version)
                || version.Build < 0 || version.Revision > 0 || version <= new Version(current.Major, current.Minor, Math.Max(0,current.Build)) || newest != null && version <= newest.Version) continue;
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != assetName) continue;
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
                var expectedUrl = Repository + "v" + version.ToString(3) + "/" + assetName;
                if (url != expectedUrl || !digest.StartsWith("sha256:",StringComparison.Ordinal) || digest.Length != 71
                    || !digest[7..].All(Uri.IsHexDigit)) continue;
                var notes = release.TryGetProperty("body",out var body) ? body.GetString() ?? "" : "";
                newest = new AvailableUpdate(version,notes.Length > 8000 ? notes[..8000] : notes,url,asset.GetProperty("size").GetInt64(),digest[7..]);
            }
        }
        return newest;
    }
    public static async Task<string> DownloadAsync(AvailableUpdate release, IProgress<int> progress, CancellationToken token)
    {
        if (release.Url != Repository + "v" + release.Version.ToString(3) + "/" + AssetName || release.Size <= 0 || release.Size > 512L*1024*1024)
            throw new InvalidDataException("올바른 업데이트 파일이 아닙니다.");
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MomongaDesktopCompanion", "updates", release.Version + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var zip = Path.Combine(folder, "update.zip");
        try
        {
            using var response = await http.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var output = File.Create(zip))
            {
                var buffer = new byte[81920]; long total=0; int count;
                while ((count=await source.ReadAsync(buffer,token))>0)
                {
                    total+=count; if(total>release.Size) throw new InvalidDataException("업데이트 파일 크기가 다릅니다.");
                    await output.WriteAsync(buffer.AsMemory(0,count),token); progress.Report((int)(total*100/release.Size));
                }
                if(total!=release.Size) throw new InvalidDataException("다운로드가 완료되지 않았습니다.");
            }
            await using(var file=File.OpenRead(zip))
                if(!Convert.ToHexString(await SHA256.HashDataAsync(file,token)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("업데이트 파일 검증에 실패했습니다.");
            token.ThrowIfCancellationRequested(); var unpacked=Path.Combine(folder,"app");
            using(var archive=ZipFile.OpenRead(zip))
                if(archive.Entries.Any(e=>((e.ExternalAttributes>>16)&0xF000)==0xA000)) throw new InvalidDataException("지원하지 않는 업데이트 파일입니다.");
            if(OperatingSystem.IsMacOS())
            {
                var start=new ProcessStartInfo("/usr/bin/ditto") {UseShellExecute=false};
                foreach(var arg in new[]{"-x","-k",zip,unpacked})start.ArgumentList.Add(arg);
                using var process=Process.Start(start)!;await process.WaitForExitAsync();token.ThrowIfCancellationRequested();
                if(process.ExitCode!=0)throw new IOException("앱 압축 해제에 실패했습니다.");
                if(!File.Exists(Path.Combine(unpacked,"Chiikawa Companion.app","Contents","MacOS","Chiikawa.Companion")))throw new InvalidDataException("앱 파일이 없습니다.");
                return unpacked;
            }
            await Task.Run(()=>ZipFile.ExtractToDirectory(zip,unpacked),token);
            var app=Path.Combine(unpacked,"Chiikawa-Companion-Windows-x64");
            if(!File.Exists(Path.Combine(app,"Momonga.Desktop.exe")))throw new InvalidDataException("실행 파일이 없습니다.");
            return app;
        }
        finally { if(File.Exists(zip))File.Delete(zip); }
    }
    public static void InstallWindows(string source)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        source=Path.GetFullPath(source);
        var staging=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MomongaDesktopCompanion","updates"))+Path.DirectorySeparatorChar;
        if(!source.StartsWith(staging,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("업데이트 경로가 올바르지 않습니다.");
        var target=Path.GetFullPath(AppContext.BaseDirectory);
        if(!File.Exists(Path.Combine(source,"Momonga.Desktop.exe")))throw new InvalidDataException("실행 파일이 없습니다.");
        // Fail before shutdown when the installed folder is read-only.
        var probe=Path.Combine(target,"update-"+Guid.NewGuid().ToString("N")+".tmp");using(File.Create(probe)){}File.Delete(probe);
        var script=Path.Combine(Path.GetDirectoryName(source)!,"install.ps1");
        File.WriteAllText(script,InstallerScript(source,target,Environment.ProcessId),new System.Text.UTF8Encoding(true));
        var info=new ProcessStartInfo("powershell.exe") {UseShellExecute=false,CreateNoWindow=true};
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",script})info.ArgumentList.Add(arg);
        using var helper=Process.Start(info) ?? throw new IOException("업데이트 도우미를 시작하지 못했습니다.");
    }
    private static string InstallerScript(string source,string target,int pid) => "$ErrorActionPreference='Stop'\n" +
            "$sourceDir="+Quote(source)+"\n$targetDir="+Quote(target)+"\n$backupDir="+Quote(Path.Combine(Path.GetDirectoryName(source)!,"backup"))+"\n" +
            "$appProcess=Get-Process -Id "+pid+" -ErrorAction SilentlyContinue\nif($appProcess){if(-not $appProcess.WaitForExit(60000)){exit 1}}\n" +
            "$copiedFiles=@(); $newFiles=@()\ntry {\n" +
            "foreach($file in Get-ChildItem -LiteralPath $sourceDir -File -Recurse){\n" +
            "$relative=$file.FullName.Substring($sourceDir.TrimEnd([IO.Path]::DirectorySeparatorChar).Length+1);$dest=Join-Path $targetDir $relative;$backup=Join-Path $backupDir $relative\n" +
            "if(Test-Path -LiteralPath $dest){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup))|Out-Null;Copy-Item -LiteralPath $dest -Destination $backup;$copiedFiles+=$relative}else{$newFiles+=$relative}\n" +
            "[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))|Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $dest -Force\n}\n" +
            "} catch {\nforeach($relative in $copiedFiles){Copy-Item -LiteralPath (Join-Path $backupDir $relative) -Destination (Join-Path $targetDir $relative) -Force}\n" +
            "foreach($relative in $newFiles){$dest=Join-Path $targetDir $relative;if(Test-Path -LiteralPath $dest){Remove-Item -LiteralPath $dest -Force}}\n" +
            "$_ | Out-String | Set-Content -LiteralPath (Join-Path $targetDir 'update-error.txt')\n}\n" +
            "Start-Process -FilePath (Join-Path $targetDir 'Momonga.Desktop.exe') -WorkingDirectory $targetDir -WindowStyle Hidden\n";
    private static string Quote(string text)=>"'"+text.Replace("'","''")+"'";
    public static void RunChecks()
    {
        var valid="[{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v0.3.2\",\"body\":\"notes\",\"assets\":[{\"name\":\""+AssetName+"\",\"browser_download_url\":\""+Repository+"v0.3.2/"+AssetName+"\",\"size\":123,\"digest\":\"sha256:"+new string('a',64)+"\"}]}]";
        if(Select(valid,new Version(0,3,1),AssetName)?.Notes!="notes" || Select(valid,new Version(0,3,2),AssetName)!=null || Select(valid,new Version(0,4,0),AssetName)!=null
            || Select(valid.Replace("\"draft\":false","\"draft\":true"),new Version(0,3,1),AssetName)!=null
            || Select(valid,new Version(0,3,1),"other.zip")!=null || Select(valid.Replace(Repository,"https://example.com/"),new Version(0,3,1),AssetName)!=null
            || Select(valid.Replace("sha256:","invalid:"),new Version(0,3,1),AssetName)!=null || Quote("C:\\a'b")!="'C:\\a''b'")
            throw new InvalidOperationException("Update selection or trust validation failed");
        if(!OperatingSystem.IsWindows())return;
        var root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"chiikawa-update-check-"+Guid.NewGuid().ToString("N")));
        var source=Path.Combine(root,"app","source");var target=Path.Combine(root,"target");
        Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source,"a.txt"),"new");File.WriteAllText(Path.Combine(target,"a.txt"),"old");
        void CopyCheck()
        {
            var script=Path.Combine(root,"check.ps1");
            // Exercise the actual copier and rollback without stopping or starting an app.
            var text=InstallerScript(source,target,int.MaxValue);text=text[..text.LastIndexOf("Start-Process",StringComparison.Ordinal)];
            File.WriteAllText(script,text,new System.Text.UTF8Encoding(true));
            var start=new ProcessStartInfo("powershell.exe") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",script})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            if(!process.WaitForExit(15000)){process.Kill();throw new InvalidOperationException("Update copier timed out");}
            if(process.ExitCode!=0)throw new InvalidOperationException("Update copier failed: "+process.StandardError.ReadToEnd());
        }
        try
        {
            CopyCheck();
            if(File.ReadAllText(Path.Combine(target,"a.txt"))!="new"||File.ReadAllText(Path.Combine(root,"app","backup","a.txt"))!="old")throw new InvalidOperationException("Update copy or backup failed");
            File.WriteAllText(Path.Combine(source,"a.txt"),"second");Directory.CreateDirectory(Path.Combine(source,"b"));
            File.WriteAllText(Path.Combine(source,"b","inner.txt"),"blocked");File.WriteAllText(Path.Combine(target,"b"),"blocking file");
            CopyCheck();
            if(File.ReadAllText(Path.Combine(target,"a.txt"))!="new"||!File.Exists(Path.Combine(target,"update-error.txt")))throw new InvalidOperationException("Update rollback failed");
        }
        finally { Directory.Delete(root,true); } // Exact, newly-created temporary test directory only.
    }
}
