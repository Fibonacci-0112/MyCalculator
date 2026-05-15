using Microsoft.Maui.Storage;
using PaycheckCalc.CloudSync;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Loads and saves <see cref="CloudSyncOptions"/> using MAUI storage primitives:
/// Preferences for non-sensitive fields, SecureStorage for the connection string.
/// </summary>
public static class MauiCloudSyncOptionsProvider
{
    private const string KeyEnabled = "CloudSync.Enabled";
    private const string KeyDatabase = "CloudSync.DatabaseId";
    private const string KeyContainer = "CloudSync.ContainerId";
    private const string SecureKeyConnStr = "CloudSync.ConnectionString";

    public static async Task<CloudSyncOptions> LoadAsync()
    {
        var opts = new CloudSyncOptions
        {
            Enabled = Preferences.Default.Get(KeyEnabled, false),
            DatabaseId = Preferences.Default.Get(KeyDatabase, "PaycheckCalc"),
            ContainerId = Preferences.Default.Get(KeyContainer, "Paychecks"),
        };
        try
        {
            opts.ConnectionString = await SecureStorage.Default
                .GetAsync(SecureKeyConnStr).ConfigureAwait(false) ?? "";
        }
        catch (Exception)
        {
            opts.ConnectionString = "";
        }
        return opts;
    }

    public static async Task SaveAsync(CloudSyncOptions opts)
    {
        Preferences.Default.Set(KeyEnabled, opts.Enabled);
        Preferences.Default.Set(KeyDatabase, opts.DatabaseId);
        Preferences.Default.Set(KeyContainer, opts.ContainerId);
        try
        {
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
                SecureStorage.Default.Remove(SecureKeyConnStr);
            else
                await SecureStorage.Default
                    .SetAsync(SecureKeyConnStr, opts.ConnectionString).ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
