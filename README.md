# Therefore Launcher

Native Windows WPF launcher for saved Therefore Online tenant connections. The installed Therefore client applications are required separately. Standalone downloads include the .NET runtime; building from source requires the .NET 8 SDK.

## Download

- [Windows x64 (Intel/AMD)](https://github.com/Fybre/therefore-launcher/releases/latest/download/ThereforeLauncher-win-x64.exe)
- [Windows ARM64](https://github.com/Fybre/therefore-launcher/releases/latest/download/ThereforeLauncher-win-arm64.exe)
- [All releases](https://github.com/Fybre/therefore-launcher/releases)

Download the EXE for your Windows architecture and run it. No additional launcher files or .NET installation are needed. Your tenant profiles remain in Local AppData when replacing the EXE.

## Single-file distribution

Publish a self-contained EXE using `dotnet publish ThereforeLauncher.csproj -p:PublishProfile=Standalone -p:MSBuildEnableWorkloadResolver=false -r win-x64`. For Windows ARM devices, use `-r win-arm64`. Output is in `bin\Standalone\win-x64\` or `bin\Standalone\win-arm64\`. Distribute just `ThereforeLauncher.exe`; it includes the .NET runtime, application libraries and icons. The published version does not need a separate .NET installation. WPF native runtime components extract to a per-user temporary cache when needed; there are no DLLs to distribute beside the EXE. Profiles remain in `%LOCALAPPDATA%\ThereforeLauncher\profiles.json` so they survive replacing or moving the EXE. Therefore client applications still need to be installed separately.

## Run

Build with `dotnet build -c Release -p:MSBuildEnableWorkloadResolver=false`, then run `bin\Release\net8.0-windows\ThereforeLauncher.exe`.

In Add tenant or Edit selected, click **Read current configuration** after saving the connection dialogs in Therefore. This prefills the tenant, exact XML server URL, login type and Entra settings when applicable. Give the profile a display name and click **Save profile**. Reading only updates the editor; saving persists the configuration. Future launches apply that saved configuration. Fields remain editable for manual setup.

Profiles and the installation folder override are saved to `%LOCALAPPDATA%\ThereforeLauncher\profiles.json`. Therefore handles authentication in the current Windows account.

## Compact mode

Click the collapse icon in the header (tooltip **Compact mode**) to switch to a narrow tenant strip. Tenant names
are shown without connection details. Right-click a tenant or click its **⋯**
button to choose Navigator, Designer or Console using the actual application
icons at 32×32 pixels and taller menu rows. Double-click a tenant or press Enter to launch Navigator. The menu also
provides profile editing; the expand icon (tooltip **Full window**) restores the detailed window. Drag reordering
and the Add/Edit/More toolbar remain available. Mode and separate window sizes
are saved locally. App menus identify the selected tenant; connection switching
uses the same safeguards in both modes.

Entra authorization and token URLs are captured and reapplied exactly, including
v1 and v2.0 endpoints. The launcher does not upgrade or replace saved endpoints
automatically, because they belong to each individual tenant configuration.

Drag tenant tiles above or below one another to reorder them. A yellow insertion line marks the drop position; dragging near the list edges scrolls longer lists. The order is saved immediately and retained on restart. You can also use **More → Move tenant up/down**, or select a tenant and press **Ctrl+Up / Ctrl+Down**. Reordering leaves the connection settings unchanged. If saving fails, the previous order is retained.

The installation directory is detected from both registry views under `HKLM\Software\Therefore\Installation`, using `InstallDir64` or `InstallDir`, with a Program Files fallback. Use Installation folder to override it.

## Connection behavior

The launcher updates `Tenant`, `XMLServer`, `XMLEnabled = 1`, `DCOMEnabled = 0` and `FederationMode` in `HKCU\Software\Therefore\Client`, then starts the installed executable. Supported login types are Username and password (`FederationMode = 0`) and Entra ID (`FederationMode = 2`), observed by comparing the installed client's settings before and after changing the provider. There is no Keep current setting option. Older profiles without a login type require reading or selecting a configuration before launching. Unsupported providers and DCOM connections cannot be imported; no fallback provider is substituted.

Entra profiles also save and apply the authorization endpoint, optional token endpoint, Therefore client ID, scopes, optional additional parameters, PKCE, browser login and Windows SSO options. Blank optional fields explicitly clear the previous tenant values. Reading automatically selects the detected login type and replaces the editor fields with that connection, including the tenant. The editor shows Entra fields only for Entra profiles. Capture uses an explicit allowlist of settings and never reads the credential, username or login-token registry values. Additional parameters are connection configuration and are copied; do not include secrets in them. Connection settings are stored as local plain JSON; saved login data is encrypted with Windows DPAPI.

The key must already exist: configure the Therefore client once first. Machine defaults are left untouched. Switching to a username/password connection without a saved login clears the previous connection's remembered username and token. Proxy and unrelated client settings are not captured. Existing Entra profiles without OIDC settings must be edited before launch. Actual Entra sign-in still requires validation with the target tenant.

Run `dotnet run --project checks/AuthChecks.csproj -p:MSBuildEnableWorkloadResolver=false` for offline authentication checks. They do not access the registry or start Therefore.

Client detection checks the current Windows session and configured installation folder (when the executable path can be read). Viewer and Loader processes without a main window are treated as background helpers; open Viewer/Loader windows and all primary client processes block configuration changes. Additional apps are allowed when the saved tenant, server URL, connection flags, login type and applicable Entra settings exactly match the current registry configuration; that launch does not rewrite settings. Selecting another tenant in the launcher only changes the selection until Launch is clicked. Warnings list the process names and PIDs. Exited processes and matching names from other folders are ignored. The launcher cannot infer the tenant of an existing process. Do not switch connections manually or through other tools while clients are open. Its mutex coordinates launcher requests in the same Windows session only; it cannot prevent external registry changes or clients starting outside the launcher. Background helpers may retain their prior session; this behavior still needs validation against actual client use.

All values changed by a launch, including FederationMode when selected, are restored if a registry write or process creation throws. On successful process creation the connection remains selected. A subsequent client login error or early exit does not trigger restoration. The registry writes are not transactional; a machine/process crash between writes can leave partial settings.

## Scope

Implemented: persistent add/edit/remove profiles, tenant validation, installation folder selection, embedded actual executable icons, guarded launching and immediate failure recovery.

Not implemented: alternate Windows identities, isolated simultaneous tenant sessions, import/export, installer or signing. Registry consumption and cached sign-in behavior must be verified against the installed Therefore version before production use. The source build does not launch clients or alter connection registry values.


## Saved profile logins

For a username and password profile, sign in using the installed Therefore client and enable its remember-password option. In the launcher, edit the matching profile, click **Save current Therefore login**, then **Save profile**. The launcher checks that the current connection matches before capturing the native remembered username and token. Launching that profile restores its saved login. You can save different logins in separate profiles, including profiles for the same tenant.

**Remove saved login**, followed by **Save profile**, removes the login from the profile. Changing the tenant or server URL in the editor clears the captured login; capture the matching login again. Entra profiles continue to use Therefore sign-in. **Read current configuration** does not import credentials.

Saved usernames and native login tokens are encrypted together using Windows DPAPI for the current Windows account. No plaintext password is requested or stored. Copying profiles to another Windows account or machine does not transfer usable saved logins. Expired or revoked logins may require signing in through Therefore again and recapturing the login. Account changes use the existing open-client guard and immediate-failure rollback. Real client sign-in with a restored token still requires manual validation against the installed Therefore version.