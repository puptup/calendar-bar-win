using System.Text.Json;

namespace CalendarBar;

public sealed class SettingsStore : ObservableObject
{
    public static SettingsStore Shared { get; } = new();

    private AccountSettings _account = AccountSettings.Empty;
    private bool _isLoggedIn;
    private int _syncIntervalMinutes = 5;
    private int _notifyMinutesBefore = 15;
    private bool _launchAtLogin = true;
    private bool _mailEnabled = true;
    private bool _mailImagesEnabled = true;
    private bool _mailHtmlRenderingEnabled = true;

    public AccountSettings Account
    {
        get => _account;
        set { if (SetProperty(ref _account, value)) SaveAccount(); }
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set { if (SetProperty(ref _isLoggedIn, value)) AppData.SetBool("isLoggedIn", value); }
    }

    public int SyncIntervalMinutes
    {
        get => _syncIntervalMinutes;
        set { if (SetProperty(ref _syncIntervalMinutes, value)) AppData.SetInt("syncIntervalMinutes", value); }
    }

    public int NotifyMinutesBefore
    {
        get => _notifyMinutesBefore;
        set { if (SetProperty(ref _notifyMinutesBefore, value)) AppData.SetInt("notifyMinutesBefore", value); }
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        private set => SetProperty(ref _launchAtLogin, value);
    }

    public bool MailEnabled
    {
        get => _mailEnabled;
        set { if (SetProperty(ref _mailEnabled, value)) AppData.SetBool("mailEnabled", value); }
    }

    public bool MailImagesEnabled
    {
        get => _mailImagesEnabled;
        set { if (SetProperty(ref _mailImagesEnabled, value)) AppData.SetBool("mailImagesEnabled", value); }
    }

    public bool MailHtmlRenderingEnabled
    {
        get => _mailHtmlRenderingEnabled;
        set { if (SetProperty(ref _mailHtmlRenderingEnabled, value)) AppData.SetBool("mailHTMLRenderingEnabled", value); }
    }

    public string AccountCacheKey => Account.Email.Trim().ToLowerInvariant();

    public string? Password => CredentialStore.LoadPassword();

    public bool DraftsUnsupported
    {
        get => AppData.GetBool($"mailDraftsUnsupported.{AccountCacheKey}");
        set => AppData.SetBool($"mailDraftsUnsupported.{AccountCacheKey}", value);
    }

    private SettingsStore()
    {
        var json = AppData.GetString("accountSettings");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var decoded = JsonSerializer.Deserialize<AccountSettings>(json);
                if (decoded is not null)
                {
                    if (string.IsNullOrEmpty(decoded.DeviceId) || !AccountSettings.IsValidDeviceId(decoded.DeviceId))
                        decoded.DeviceId = AccountSettings.GenerateDeviceId();
                    _account = decoded;
                }
            }
            catch { }
        }
        _isLoggedIn = AppData.GetBool("isLoggedIn");
        var stored = AppData.GetInt("syncIntervalMinutes", 5);
        _syncIntervalMinutes = stored <= 0 ? 5 : stored;
        _notifyMinutesBefore = AppData.HasKey("notifyMinutesBefore") ? AppData.GetInt("notifyMinutesBefore", 15) : 15;
        _launchAtLogin = AppData.HasKey("launchAtLogin") ? AppData.GetBool("launchAtLogin", true) : true;
        _mailEnabled = AppData.HasKey("mailEnabled") ? AppData.GetBool("mailEnabled", true) : true;
        _mailImagesEnabled = AppData.HasKey("mailImagesEnabled") ? AppData.GetBool("mailImagesEnabled", true) : true;
        _mailHtmlRenderingEnabled = AppData.HasKey("mailHTMLRenderingEnabled") ? AppData.GetBool("mailHTMLRenderingEnabled", true) : true;
    }

    public void RefreshLaunchAtLoginStatus() => LaunchAtLogin = LaunchAtLoginService.IsEnabled;

    public void SetLaunchAtLogin(bool enabled)
    {
        try
        {
            LaunchAtLoginService.SetEnabled(enabled);
            LaunchAtLogin = LaunchAtLoginService.IsEnabled;
            AppData.SetBool("launchAtLogin", LaunchAtLogin);
        }
        catch
        {
            AppData.SetBool("launchAtLogin", enabled);
            LaunchAtLogin = LaunchAtLoginService.IsEnabled;
        }
    }

    public void ApplyLaunchAtLoginPreference()
    {
        var preferred = AppData.HasKey("launchAtLogin") ? AppData.GetBool("launchAtLogin", true) : true;
        LaunchAtLoginService.ApplyStoredPreference(preferred);
        LaunchAtLogin = LaunchAtLoginService.IsEnabled;
    }

    private void SaveAccount()
    {
        AppData.SetString("accountSettings", JsonSerializer.Serialize(_account));
    }

    public void Logout()
    {
        var cacheKey = AccountCacheKey;
        ActiveSyncSyncKeyStore.Shared.Reset(cacheKey);
        DraftsUnsupported = false;
        _ = MailBodyCache.Shared.Clear(cacheKey);
        IsLoggedIn = false;
        CredentialStore.DeletePassword();
        var deviceId = Account.ResolvedDeviceId;
        Account = new AccountSettings { DeviceId = deviceId };
    }

    public void SaveCredentials(string email, string server, string domain, string username, string password)
    {
        var deviceId = string.IsNullOrEmpty(Account.ResolvedDeviceId) || !AccountSettings.IsValidDeviceId(Account.DeviceId)
            ? AccountSettings.GenerateDeviceId()
            : Account.ResolvedDeviceId;
        Account = new AccountSettings
        {
            Email = email, Server = server, Domain = domain, Username = username, DeviceId = deviceId
        };
        CredentialStore.SavePassword(password);
        IsLoggedIn = true;
    }
}
