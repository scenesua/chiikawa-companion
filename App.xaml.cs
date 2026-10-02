using System;
using System.Windows;
using Momonga.Core;
using System.Threading;
using System.Windows.Threading;

namespace Momonga;

public partial class App : Application
{
    private AppController? controller;
    private Mutex? instance;
    private EventWaitHandle? showPet;
    private RegisteredWaitHandle? showWait;
    private bool verification;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Array.IndexOf(e.Args, "--self-test") >= 0)
        {
            try { UI.Theme.Apply(Character.CharacterDefinition.Load().Theme); Simulation.MotionChecks.Run(); Animation.PetAnimator.RunChecks(); UI.PetWindow.RunSizeChecks(); Simulation.LifeChecks.Run(); Shutdown(0); }
            catch (Exception error)
            {
                System.IO.File.WriteAllText("self-test-error.txt", error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (Array.IndexOf(e.Args, "--ui-check") >= 0)
        {
            verification = true;
            controller = new AppController(true); controller.Start();
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    controller.CaptureUI(); var data = controller.Life.Data; var ap = data.ActivityPoints; var items = data.Items;
                    SwitchCharacter("usagi");
                    if (controller.Life.Character.CharacterId != "usagi" || !ReferenceEquals(controller.Life.Data, data) || data.ActivityPoints != ap || data.Items != items)
                        throw new InvalidOperationException("Character switch lost shared state");
                    SwitchCharacter("momonga"); Shutdown(0);
                }
                catch (Exception error) { System.IO.File.WriteAllText("self-test-error.txt", error.ToString()); Shutdown(1); }
            }));
            return;
        }
        showPet = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MomongaDesktopCompanion.Show");
        instance = new Mutex(true, @"Local\MomongaDesktopCompanion", out var first);
        if (!first) { showPet.Set(); instance.Dispose(); instance = null; Shutdown(0); return; }
        Platform.ToolCursor.Recover();
        var recoveryCheck = Array.IndexOf(e.Args, "--recovery-check") >= 0;
        controller = new AppController(recoveryCheck);
        controller.Start();
        showWait = ThreadPool.RegisterWaitForSingleObject(showPet, (_, _) => Dispatcher.BeginInvoke(new Action(() => controller?.ShowPet())), null, Timeout.Infinite, false);
        if (recoveryCheck) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => controller.CheckRecovery(() => Shutdown(0))));
    }

    public void SwitchCharacter(string id)
    {
        if (controller == null || controller.Life.Data.CharacterId == id) return;
        var definition = Character.CharacterDefinition.Load(id);
        _ = new Animation.PetAnimator(definition.AssetSet); // Validate resources before replacing the running companion.
        var data = controller.Life.Data; data.CharacterId = id;
        controller.Dispose(); controller = new AppController(verification, data); controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        showWait?.Unregister(null); showPet?.Dispose();
        controller?.Dispose();
        if (instance != null) { instance.ReleaseMutex(); instance.Dispose(); }
        base.OnExit(e);
    }
}
