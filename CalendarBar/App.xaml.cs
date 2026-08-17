using System.Windows;

namespace CalendarBar;

public partial class App : Application
{
    public const string AppUserModelId = "com.organization.CalendarBar";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        WbxmlCodec.SelfCheck();

        SettingsStore.Shared.ApplyLaunchAtLoginPreference();
        TrayManager.Shared.InstallCalendar();
        _ = CalendarSyncService.Shared;
        _ = MailSyncService.Shared;
        NetworkReachabilityService.Shared.Start();
        NotificationService.Shared.Configure();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CalendarSyncService.Shared.StopPeriodicSync();
        MailSyncService.Shared.StopPeriodicSync();
        NetworkReachabilityService.Shared.Stop();
        TrayManager.Shared.Dispose();
        base.OnExit(e);
    }

    public static void Quit()
    {
        Current.Shutdown();
    }
}
