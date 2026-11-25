using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThereforeLauncher;

public sealed record TenantProfile(string Name, string Tenant, int? AuthenticationMode = null, OidcSettings? Oidc = null, string? ServerUrl = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string TenantSubtitle => string.Equals(Name.Trim(), Tenant, StringComparison.OrdinalIgnoreCase) ? "" : Tenant;
    public string LoginType => AuthenticationMode switch { null => "Configuration required", 0 => "Username and password", 2 => "Entra ID", _ => "Unsupported login type" };
    public string Url => ServerUrl ?? $"https://{Tenant}.thereforeonline.com/TheXMLServer";
    public void Validate()
    {
        if (!Launcher.ValidTenant(Tenant)) throw new InvalidOperationException("Invalid tenant subdomain.");
        if (AuthenticationMode is not (0 or 2)) throw new InvalidOperationException("Edit this profile and read the current configuration or select a login type before launching.");
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Enter a valid HTTPS XML server URL.");
        if (AuthenticationMode == 2)
        {
            if (Oidc is null) throw new InvalidOperationException("Read the current configuration or enter this tenant's Entra settings first.");
            Oidc.Validate();
        }
    }
    public override string ToString() => $"{Name}\n{Tenant}";
}
public sealed class Settings
{
    public List<TenantProfile> Profiles { get; set; } = [];
    public string InstallDirectory { get; set; } = "";
}
public static class Launcher
{
    public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThereforeLauncher", "profiles.json");
    public static Settings Load() => File.Exists(SettingsPath)
        ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? throw new InvalidDataException("Empty configuration.")
        : new Settings();
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, SettingsPath, true);
    }
    public static bool ValidTenant(string tenant) => tenant.Length <= 63 && Regex.IsMatch(tenant, @"\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\z");
    public static string FindInstallation()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = hive.OpenSubKey(@"Software\Therefore\Installation");
            foreach (var name in new[] { "InstallDir64", "InstallDir" })
                if (key?.GetValue(name) is string path && Directory.Exists(path)) return path;
        }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Therefore");
        return Directory.Exists(fallback) ? fallback : "";
    }
    public static void Launch(TenantProfile profile, string application, string directory)
    {
        profile.Validate();
        if (application is not ("Navigator" or "Designer" or "Console")) throw new InvalidOperationException("Unknown application.");
        var executable = Path.Combine(directory, $"The{application}.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("Application not found. Choose the Therefore installation folder.", executable);
        using var gate = new Mutex(false, @"Local\ThereforeLauncher.ConnectionSwitch");
        bool acquired;
        try { acquired = gate.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new InvalidOperationException("Another launcher is changing the connection. Try again shortly.");
        try
        {
            var blockers = ClientDetection.FindBlocking(directory);
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = hive.OpenSubKey(@"Software\Therefore\Client", true)
                ?? throw new InvalidOperationException("Therefore client settings were not found. Open and configure the installed client once first.");
            var desired = ConnectionConfiguration.ValuesFor(profile);
            void StartClient()
            {
                using var launched = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false })
                    ?? throw new InvalidOperationException("Windows did not start the application.");
            }
            // An additional application can share the existing configuration. Do
            // not rewrite settings (or restore them on failure) in this path.
            if (ConnectionConfiguration.Matches(desired, name => key.GetValue(name)))
            {
                StartClient();
                return;
            }
            if (blockers.Count > 0)
                throw new InvalidOperationException($"Cannot switch to the saved connection for {profile.Name} while these clients are open: " +
                    string.Join(", ", blockers.Select(p => $"{p.Name}.exe (PID {p.Id}{(p.HasWindow ? "" : ", background process")})")) +
                    ". Close them before changing the tenant or login settings. Apps using the current configuration can be launched together.");
            var previous = new Dictionary<string, (object? Value, RegistryValueKind Kind)>();
            foreach (var name in desired.Keys)
            {
                var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                previous[name] = (value, value is null ? RegistryValueKind.String : key.GetValueKind(name));
            }
            try
            {
                foreach (var (name, value) in desired)
                    key.SetValue(name, value, value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
                key.Flush();
                StartClient();
            }
            catch
            {
                foreach (var (name, old) in previous)
                    if (old.Value is null) key.DeleteValue(name, false); else key.SetValue(name, old.Value, old.Kind);
                key.Flush();
                throw;
            }
        }
        finally { gate.ReleaseMutex(); }
    }
}
