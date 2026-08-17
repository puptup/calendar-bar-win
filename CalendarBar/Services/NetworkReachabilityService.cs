using System.Net.NetworkInformation;

namespace CalendarBar;

public sealed class NetworkReachabilityService : ObservableObject
{
    public static NetworkReachabilityService Shared { get; } = new();

    private bool _isOnline = true;
    private bool _hasSeenInitial = false;
    private bool _wasOffline;
    private bool _recoverySyncInProgress;

    public bool IsOnline
    {
        get => _isOnline;
        private set => SetProperty(ref _isOnline, value);
    }

    public void Start()
    {
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        Handle(NetworkInterface.GetIsNetworkAvailable());
    }

    public void Stop()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Handle(e.IsAvailable));

    private void Handle(bool online)
    {
        IsOnline = online;
        if (!_hasSeenInitial)
        {
            _hasSeenInitial = true;
            _wasOffline = !online;
            return;
        }
        if (!online) { _wasOffline = true; return; }
        if (!_wasOffline) return;
        _wasOffline = false;
        TriggerRecoverySync();
    }

    private async void TriggerRecoverySync()
    {
        if (!SettingsStore.Shared.IsLoggedIn || _recoverySyncInProgress) return;
        _recoverySyncInProgress = true;
        await CalendarSyncService.Shared.SyncNow();
        if (SettingsStore.Shared.MailEnabled)
            await MailSyncService.Shared.SyncInboxForNetworkRecovery();
        _recoverySyncInProgress = false;
    }
}
