using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Momonga.Updates;
using System.Diagnostics;
using System.Threading;

namespace Momonga.Mac;

public sealed partial class CompanionController
{
    public async Task CheckUpdatesAsync()
    {
        try
        {
            var release=await UpdateService.CheckAsync(updateCancellation.Token);
            if(release!=null&&!disposed) await Dispatcher.UIThread.InvokeAsync(()=>ShowUpdate(release));
        }
        catch(Exception) { /* No startup popup when offline or already current. */ }
    }
    private void ShowUpdate(AvailableUpdate release)
    {
        var body=new StackPanel {Spacing=10};
        TextBlock Text(string value,int size=13)=>new() {Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Foreground=Brush(life.Character.Theme.Ink)};
        body.Children.Add(Text($"현재 {UpdateService.CurrentLabel} → 새 버전 {release.Version.ToString(3)}",15));
        body.Children.Add(Text($"다운로드 · {release.Size/1024d/1024:F1} MB"));body.Children.Add(new ScrollViewer {Content=Text(release.Notes),MaxHeight=140});
        body.Children.Add(Text("다운로드 후 현재 앱을 종료하고, 열린 폴더의 새 앱을 응용 프로그램 폴더에 덮어써 주세요. 저장 데이터는 유지됩니다."));
        var status=Text("");var progress=new ProgressBar {Minimum=0,Maximum=100,Height=7,IsVisible=false};body.Children.Add(status);body.Children.Add(progress);
        var later=Button("나중에",()=>panel?.Close());var install=Button("바로 업데이트하기",()=>{});body.Children.Add(install);body.Children.Add(later);
        ShowPanel("업데이트",body);var win=panel!;
        var cancellation=CancellationTokenSource.CreateLinkedTokenSource(updateCancellation.Token);win.Closed+=(_,_)=>cancellation.Cancel();
        install.Click+=async (_,_)=>
        {
            install.IsEnabled=false;later.Content="다운로드 취소";progress.IsVisible=true;
            try
            {
                var folder=await UpdateService.DownloadAsync(release,new Progress<int>(value=>{progress.Value=value;status.Text=$"다운로드 중 · {value}%";}),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();await save.SaveAsync(life.Data);
                var open=new ProcessStartInfo("/usr/bin/open") {UseShellExecute=false};open.ArgumentList.Add(folder);
                using var process=Process.Start(open);status.Text="새 앱 폴더를 열었습니다.";
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(!cancellation.IsCancellationRequested){status.Text="업데이트 실패: "+error.Message;install.IsEnabled=true;later.Content="나중에";}}
        };
    }
}
