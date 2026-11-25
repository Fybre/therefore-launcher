using Microsoft.Win32;

namespace ThereforeLauncher;

public sealed record OidcSettings
{
    public string AuthorizationEndpoint { get; init; } = "";
    public string TokenEndpoint { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string Scopes { get; init; } = "openid";
    public string AdditionalParameters { get; init; } = "";
    public bool EnablePkce { get; init; }
    public bool UseBrowser { get; init; } = true;
    public bool UseWindowsSso { get; init; }

    public void Validate()
    {
        static bool Https(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
        if (!Https(AuthorizationEndpoint)) throw new InvalidOperationException("Enter a valid HTTPS authorization endpoint.");
        if (TokenEndpoint.Length > 0 && !Https(TokenEndpoint)) throw new InvalidOperationException("Token endpoint must be blank or a valid HTTPS URL.");
        if (!Guid.TryParse(ClientId, out _)) throw new InvalidOperationException("Enter the Therefore Entra client ID as a GUID.");
        if (string.IsNullOrWhiteSpace(Scopes)) throw new InvalidOperationException("Enter the required scopes (for example openid).");
    }

    public Dictionary<string, object> RegistryValues() => new()
    {
        ["OIDCAuthUrl"] = AuthorizationEndpoint,
        ["OIDCTokenUrl"] = TokenEndpoint,
        ["OIDCClientId"] = ClientId,
        ["OIDCScopes"] = Scopes,
        // This spelling is the actual installed Therefore registry value name.
        ["OIDCAddionalParams"] = AdditionalParameters,
        ["OIDCUsePKCE"] = EnablePkce ? 1 : 0,
        ["OIDCUseSystemBrowser"] = UseBrowser ? 1 : 0,
        ["OIDCUseWindowsSSO"] = UseWindowsSso ? 1 : 0
    };

    public static OidcSettings Read(Func<string, object?> read)
    {
        string Text(string name) => read(name) as string ?? "";
        bool Flag(string name) => read(name) is int value && value != 0;
        var settings = new OidcSettings
        {
            AuthorizationEndpoint = Text("OIDCAuthUrl"), TokenEndpoint = Text("OIDCTokenUrl"),
            ClientId = Text("OIDCClientId"), Scopes = Text("OIDCScopes"),
            AdditionalParameters = Text("OIDCAddionalParams"),
            EnablePkce = Flag("OIDCUsePKCE"), UseBrowser = Flag("OIDCUseSystemBrowser"),
            UseWindowsSso = Flag("OIDCUseWindowsSSO")
        };
        settings.Validate();
        return settings;
    }
}
