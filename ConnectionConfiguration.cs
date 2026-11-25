using Microsoft.Win32;

namespace ThereforeLauncher;

public static class ConnectionConfiguration
{
    public static Dictionary<string, object> ValuesFor(TenantProfile profile)
    {
        profile.Validate();
        var values = new Dictionary<string, object>
        {
            ["Tenant"] = profile.Tenant, ["XMLServer"] = profile.Url,
            ["XMLEnabled"] = 1, ["DCOMEnabled"] = 0, ["FederationMode"] = profile.AuthenticationMode!.Value
        };
        if (profile.AuthenticationMode == 2)
            foreach (var (name, value) in profile.Oidc!.RegistryValues()) values.Add(name, value);
        return values;
    }

    public static bool Matches(IReadOnlyDictionary<string, object> desired, Func<string, object?> read)
        => desired.All(pair => Equals(pair.Value, read(pair.Key)));

    public static TenantProfile ReadCurrent()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = hive.OpenSubKey(@"Software\Therefore\Client")
            ?? throw new InvalidOperationException("Therefore client settings not found. Configure and save a connection in Therefore first.");
        return Read(name => key.GetValue(name));
    }

    // Explicit allowlist: never enumerate values or read credentials/tokens.
    public static TenantProfile Read(Func<string, object?> read)
    {
        if (read("XMLEnabled") is not int xml || xml != 1 || read("DCOMEnabled") is not int dcom || dcom != 0)
            throw new InvalidOperationException("Only XML Web Service connections are supported. Configure that connection in Therefore and save it first.");
        var tenant = (read("Tenant") as string ?? "").Trim().ToLowerInvariant();
        if (!Launcher.ValidTenant(tenant)) throw new InvalidOperationException("The current Therefore tenant is missing or invalid.");
        if (read("FederationMode") is not int mode || mode is not (0 or 2))
            throw new InvalidOperationException("The current authentication provider is not supported yet. Supported providers are Username and password and Entra ID.");
        var server = read("XMLServer") as string;
        if (string.IsNullOrWhiteSpace(server)) throw new InvalidOperationException("The current XML server URL is missing. Save the connection in Therefore first.");
        var profile = new TenantProfile(tenant, tenant, mode, mode == 2 ? OidcSettings.Read(read) : null, server);
        profile.Validate();
        return profile;
    }
}
