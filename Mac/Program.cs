using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Momonga.Mac;

public static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<CompanionApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}

public sealed class CompanionApp : Application
{
    public override void Initialize() { RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light; Styles.Add(new FluentTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode=Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            if(desktop.Args?.Contains("--self-test")==true)
            {
                Dispatcher.UIThread.Post(()=>
                {
                    try { Simulation.MotionChecks.Run(); Simulation.LifeChecks.Run(); MacChecks.Run(); desktop.Shutdown(0); }
                    catch(Exception error) { Console.Error.WriteLine(error); desktop.Shutdown(1); }
                });
            }
            else
            {
                var controller=new CompanionController(desktop,desktop.Args?.Contains("--ui-check")==true);
                desktop.Exit+=(_,_)=>controller.Dispose(); controller.Start();
                if(desktop.Args?.Contains("--ui-check")==true) DispatcherTimer.RunOnce(()=>
                {
                    try { controller.CheckUI(); desktop.Shutdown(0); }
                    catch(Exception error) { Console.Error.WriteLine(error); desktop.Shutdown(1); }
                },TimeSpan.FromSeconds(2));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
