using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Momonga.Updates;

namespace Momonga.UI;

public sealed class UpdateWindow : CompanionPanelWindow
{
    public UpdateWindow(AvailableUpdate release) : base($"{release.Version.ToString(3)} 업데이트", "Chiikawa Companion · 현재 " + UpdateService.CurrentLabel, true)
    {
        var cancellation=new CancellationTokenSource();Closed+=(_,_)=>cancellation.Cancel();
        Body.Children.Add(Theme.Text($"새 버전이 있어요 · {release.Size/1024d/1024:F1} MB",14,true));
        Body.Children.Add(Theme.Text("릴리즈 노트",15,true));Body.Children.Add(Theme.Card(new ScrollViewer {Content=Theme.Text(release.Notes,12),MaxHeight=210,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}));
        var progress=new ProgressBar {Minimum=0,Maximum=100,Height=7,Visibility=Visibility.Collapsed,Margin=new Thickness(0,10,0,10)};Body.Children.Add(progress);
        Body.Children.Add(Theme.Text("저장한 뒤 앱을 업데이트하고 다시 실행합니다.",12));
        var later=Theme.Button("나중에",Close);var install=Theme.Button("바로 업데이트하기",()=>{});
        install.Click+=async (_,_)=>
        {
            install.IsEnabled=false;later.Content="다운로드 취소";progress.Visibility=Visibility.Visible;
            try
            {
                var folder=await UpdateService.DownloadAsync(release,new Progress<int>(value=>{progress.Value=value;Notice.Text=$"다운로드 중 · {value}%";}),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();Notice.Text="저장 후 업데이트합니다…";
                UpdateService.InstallWindows(folder);Application.Current.Shutdown();
            }
            catch(OperationCanceledException){}
            catch(Exception error) {if(!cancellation.IsCancellationRequested){Notice.Text="업데이트 실패: "+error.Message;install.IsEnabled=true;later.Content="나중에";}}
        };
        Body.Children.Add(install);Body.Children.Add(later);
    }
}
