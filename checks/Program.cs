using System.Text.Json;
using ThereforeLauncher;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var oidc = new OidcSettings { AuthorizationEndpoint = "https://login.microsoftonline.com/example.onmicrosoft.com/oauth2/authorize", ClientId = "11111111-1111-1111-1111-111111111111" };
oidc.Validate();
var profile = new TenantProfile("Example", "example", 2, oidc);
var restored = JsonSerializer.Deserialize<TenantProfile>(JsonSerializer.Serialize(profile))!;
Check(restored == profile, "OIDC settings must survive profile serialization.");
var old = JsonSerializer.Deserialize<TenantProfile>("{\"Name\":\"Existing\",\"Tenant\":\"existing\"}")!;
Check(old.AuthenticationMode is null && old.Oidc is null, "Old profiles must load without inventing authentication settings.");
bool migrationBlocked = false;
try { old.Validate(); } catch (InvalidOperationException) { migrationBlocked = true; }
Check(migrationBlocked, "Unconfigured old profiles must require configuration before launching.");
var values = oidc.RegistryValues();
Check(values.Count == 8, "Exactly eight OIDC settings should be applied.");
Check((string)values["OIDCTokenUrl"] == "" && (string)values["OIDCAddionalParams"] == "", "Blank settings must clear the previous tenant values.");
Check(values["OIDCUseSystemBrowser"] is int enabled && enabled == 1, "Flags must be DWORD-compatible integers.");
Check(!values.ContainsKey("Token") && !values.ContainsKey("User"), "Credentials must not be included.");
foreach (var invalid in new[] { oidc with { AuthorizationEndpoint = "http://example.com" }, oidc with { TokenEndpoint = "invalid" }, oidc with { ClientId = "invalid" }, oidc with { Scopes = "" } })
{
    bool rejected = false;
    try { invalid.Validate(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Invalid OIDC configuration must be rejected.");
}
Console.WriteLine("Authentication checks passed: serialization, legacy profiles, registry mapping, clearing, credential exclusion and validation. No registry reads or writes performed.");
var source = oidc.RegistryValues();
source["Tenant"] = "example"; source["XMLServer"] = "https://example.thereforeonline.com/custom/TheXMLServer";
source["XMLEnabled"] = 1; source["DCOMEnabled"] = 0; source["FederationMode"] = 2;
source["OIDCAddionalParams"] = "prompt=select_account";
var readNames = new HashSet<string>();
object? Read(string name) { readNames.Add(name); return source.GetValueOrDefault(name); }
var captured = ConnectionConfiguration.Read(Read);
Check(captured.AuthenticationMode == 2 && captured.Oidc?.AdditionalParameters == "prompt=select_account", "Import must include the authentication type and OIDC settings.");
Check(captured.Url.EndsWith("/custom/TheXMLServer"), "Import must preserve the actual server URL.");
Check(!readNames.Contains("Token") && !readNames.Contains("User"), "Import must not access credentials.");
Check(JsonSerializer.Deserialize<TenantProfile>(JsonSerializer.Serialize(captured)) == captured, "The captured configuration must survive saving.");
source["FederationMode"] = 0; readNames.Clear();
captured = ConnectionConfiguration.Read(Read);
Check(captured.AuthenticationMode == 0 && captured.Oidc is null && !readNames.Contains("OIDCAuthUrl"), "Password imports must not inherit stale Entra settings.");
source["FederationMode"] = 99;
bool unsupportedBlocked = false;
try { ConnectionConfiguration.Read(Read); } catch (InvalidOperationException) { unsupportedBlocked = true; }
Check(unsupportedBlocked, "Unsupported providers must fail without silently falling back.");
source["FederationMode"] = 0; source["DCOMEnabled"] = 1;
bool dcomBlocked = false;
try { ConnectionConfiguration.Read(Read); } catch (InvalidOperationException) { dcomBlocked = true; }
Check(dcomBlocked, "Unsupported DCOM connections must fail explicitly.");
Console.WriteLine("Configuration import checks passed: provider detection, exact URL, complete OIDC capture, serialization, credential exclusion and unsupported configuration handling.");
var install = @"C:\Program Files\Therefore";
var backgroundViewer = new ClientProcess("TheViewer", 16032, 1, false, false, install + @"\TheViewer.exe");
Check(!backgroundViewer.BlocksLaunch(1, install), "A windowless Viewer helper must not block launching.");
Check((backgroundViewer with { HasWindow = true }).BlocksLaunch(1, install), "An open Viewer must block switching.");
Check((backgroundViewer with { Name = "TheNavigator", Executable = install + @"\TheNavigator.exe" }).BlocksLaunch(1, install), "A hidden primary client must still block switching.");
Check(!(backgroundViewer with { HasWindow = true, SessionId = 2 }).BlocksLaunch(1, install), "An unrelated Windows session must not block launching.");
Check(!(backgroundViewer with { HasWindow = true, HasExited = true }).BlocksLaunch(1, install), "Exited processes must not block launching.");
Check(!(backgroundViewer with { HasWindow = true, Executable = @"C:\Other\TheViewer.exe" }).BlocksLaunch(1, install), "Unrelated executables with matching names must not block launching.");
Console.WriteLine("Client detection checks passed: background helpers, visible clients, hidden primary clients, sessions, exited processes and executable paths.");
var desired = ConnectionConfiguration.ValuesFor(profile);
var currentValues = new Dictionary<string, object>(desired);
object? CurrentValue(string name) => currentValues.GetValueOrDefault(name);
Check(ConnectionConfiguration.Matches(desired, CurrentValue), "Matching connections must permit additional app launches.");
Check(ConnectionConfiguration.Matches(ConnectionConfiguration.ValuesFor(profile with { Name = "Different label" }), CurrentValue), "Display labels must not affect connection identity.");
foreach (var key in desired.Keys)
{
    currentValues[key] = desired[key] is int number ? number + 1 : "changed";
    Check(!ConnectionConfiguration.Matches(desired, CurrentValue), $"Changed {key} must be treated as a connection switch.");
    currentValues[key] = desired[key];
}
currentValues["Token"] = "ignored-for-configuration-comparison";
Check(ConnectionConfiguration.Matches(desired, CurrentValue), "Login tokens must not affect connection identity.");
currentValues.Remove("FederationMode");
Check(!ConnectionConfiguration.Matches(desired, CurrentValue), "Missing settings must require configuration.");
Console.WriteLine("Shared connection checks passed: matching configurations, labels, tenant/server/auth/SSO changes, credentials and missing values.");
var tenants = new[] { "A", "B", "C", "D" };
Check(ProfileOrder.Move(tenants, 0, 4).SequenceEqual(new[] { "B", "C", "D", "A" }), "Move first tenant to last.");
Check(ProfileOrder.Move(tenants, 3, 0).SequenceEqual(new[] { "D", "A", "B", "C" }), "Move last tenant to first.");
Check(ProfileOrder.Move(tenants, 1, 3).SequenceEqual(new[] { "A", "C", "B", "D" }), "Move a tenant down one position.");
Check(ProfileOrder.Move(tenants, 2, 1).SequenceEqual(new[] { "A", "C", "B", "D" }), "Move a tenant up one position.");
Check(ProfileOrder.Move(tenants, 1, 1).SequenceEqual(tenants) && ProfileOrder.Move(tenants, 1, 2).SequenceEqual(tenants), "Adjacent insertion boundaries must be no-ops.");
var duplicates = new[] { profile, profile with { }, old };
var reordered = ProfileOrder.Move(duplicates, 1, 0);
Check(ReferenceEquals(reordered[0], duplicates[1]), "Reordering must preserve the exact profile instance even when values match.");
Check(JsonSerializer.Deserialize<List<TenantProfile>>(JsonSerializer.Serialize(reordered))!.SequenceEqual(reordered), "Profile order must survive serialization.");
Check(tenants.SequenceEqual(new[] { "A", "B", "C", "D" }), "Reordering must leave the original list available for rollback.");
Console.WriteLine("Profile reordering checks passed: moves in both directions, boundaries, duplicates, serialization and original-list preservation.");
var v2 = oidc with
{
    AuthorizationEndpoint = "https://login.microsoftonline.com/example.onmicrosoft.com/oauth2/v2.0/authorize",
    TokenEndpoint = "https://login.microsoftonline.com/example.onmicrosoft.com/oauth2/v2.0/token",
    Scopes = "openid;profile;email", EnablePkce = true, UseBrowser = false, UseWindowsSso = true
};
var v2Values = v2.RegistryValues();
var capturedV2 = OidcSettings.Read(name => v2Values.GetValueOrDefault(name));
Check(capturedV2 == v2, "Entra v2 endpoints, scopes and SSO flags must be preserved exactly.");
var preferences = new Settings { Window = new WindowPreferences { CompactMode = true, CompactWidth = 310, CompactHeight = 480, FullWidth = 1100, FullHeight = 700 } };
var restoredPreferences = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(preferences))!;
Check(restoredPreferences.Window.CompactMode && restoredPreferences.Window.CompactWidth == 310 && restoredPreferences.Window.FullWidth == 1100, "Window mode and independent dimensions must survive serialization.");
Check(!JsonSerializer.Deserialize<Settings>("{\"Profiles\":[]}")!.Window.CompactMode, "Older settings must open in full mode by default.");
Console.WriteLine("Compact preferences and Entra v2 capture checks passed.");
