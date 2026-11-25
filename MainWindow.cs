using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThereforeLauncher;
public sealed partial class MainWindow : Window
{
    readonly Settings settings = new();
    readonly ListBox profiles = new();
    readonly System.Windows.Controls.Primitives.UniformGrid apps = new() { Columns = 3 };
    readonly TextBlock heading = new() { FontSize = 28, FontWeight = FontWeights.SemiBold };
    readonly TextBlock address = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 24) };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 24, 0, 12) };
    static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(228, 185, 0));
    public MainWindow()
    {
        Title = "Therefore Launcher"; Width = 1020; Height = 640; MinWidth = 850; MinHeight = 570;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/icons/launcher.ico"));
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 242));
        Foreground = new SolidColorBrush(Color.FromRgb(53, 53, 53));
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/TenantStyles.xaml") });
        try { settings = Launcher.Load(); }
        catch (Exception ex) { throw new InvalidOperationException("Unable to read profiles. The existing file has been preserved.\n\n" + ex.Message, ex); }
        if (string.IsNullOrWhiteSpace(settings.InstallDirectory)) settings.InstallDirectory = Launcher.FindInstallation();
        var shell = new DockPanel(); Content = shell;
        var title = new Border { Background = Brushes.White, BorderBrush = Gold, BorderThickness = new(0, 0, 0, 3), Padding = new(24, 14, 24, 14), Child = new TextBlock { Text = "∴  Therefore Launcher", FontSize = 21, FontWeight = FontWeights.SemiBold } };
        DockPanel.SetDock(title, Dock.Top); shell.Children.Add(title);
        var sidebar = new DockPanel { Width = 260, Margin = new(16) }; DockPanel.SetDock(sidebar, Dock.Left); shell.Children.Add(sidebar);
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new(0, 8, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom); sidebar.Children.Add(actions);
        Button CompactAction(string label, string tooltip, RoutedEventHandler handler)
        {
            var action = Button(label, handler);
            action.Padding = new(8, 6, 8, 6);
            action.Margin = new(2, 0, 2, 0);
            action.ToolTip = tooltip;
            action.MinHeight = 32;
            return action;
        }
        actions.Children.Add(CompactAction("+ Add", "Add tenant profile", (_, _) => Edit(null)));
        var editAction = CompactAction("Edit", "Edit selected tenant", (_, _) => { if (profiles.SelectedItem is TenantProfile p) Edit(p); });
        actions.Children.Add(editAction);
        var menu = new ContextMenu();
        var removeItem = new MenuItem { Header = "Remove selected tenant…" };
        removeItem.Click += (_, _) => Remove(); menu.Items.Add(removeItem);
        var moveUp = new MenuItem { Header = "Move tenant up", InputGestureText = "Ctrl+↑" };
        moveUp.Click += (_, _) => MoveSelectedProfile(-1); menu.Items.Add(moveUp);
        var moveDown = new MenuItem { Header = "Move tenant down", InputGestureText = "Ctrl+↓" };
        moveDown.Click += (_, _) => MoveSelectedProfile(1); menu.Items.Add(moveDown);
        menu.Items.Add(new Separator());
        var folderItem = new MenuItem { Header = "Installation folder…" };
        folderItem.Click += (_, _) => ChooseFolder(); menu.Items.Add(folderItem);
        var appSettings = new MenuItem { Header = "Application icons…" };
        appSettings.Click += (_, _) => EditApplications(); menu.Items.Add(appSettings);
        var moreAction = CompactAction("More ⋯", "Tenant actions and settings", (sender, _) =>
        {
            removeItem.IsEnabled = profiles.SelectedItem is TenantProfile;
            menu.PlacementTarget = (Button)sender;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        });
        moreAction.ContextMenu = menu;
        menu.Opened += (_, _) =>
        {
            removeItem.IsEnabled = profiles.SelectedItem is TenantProfile;
            moveUp.IsEnabled = profiles.SelectedIndex > 0;
            moveDown.IsEnabled = profiles.SelectedIndex >= 0 && profiles.SelectedIndex < settings.Profiles.Count - 1;
        };
        actions.Children.Add(moreAction);
        void UpdateActions() => editAction.IsEnabled = profiles.SelectedItem is TenantProfile;
        profiles.SelectionChanged += (_, _) => UpdateActions(); UpdateActions();
        var tenantLabel = new TextBlock { Text = "TENANT PROFILES", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DimGray, Margin = new(4, 8, 0, 16) };
        DockPanel.SetDock(tenantLabel, Dock.Top); sidebar.Children.Add(tenantLabel);
        profiles.BorderThickness = new(0); profiles.Background = Brushes.Transparent;
        profiles.Padding = new(2);
        profiles.ItemContainerStyle = (Style)Resources["TenantItemStyle"];
        profiles.ItemTemplate = (DataTemplate)Resources["TenantItemTemplate"];
        ScrollViewer.SetHorizontalScrollBarVisibility(profiles, ScrollBarVisibility.Disabled);
        EnableProfileReordering();
        profiles.SelectionChanged += (_, _) => RefreshDetail(); sidebar.Children.Add(profiles);
        var main = new StackPanel { Margin = new(28) }; shell.Children.Add(main);
        main.Children.Add(heading); main.Children.Add(address);
        main.Children.Add(new TextBlock { Text = "SIGN-IN CONTEXT", Foreground = Brushes.DimGray, FontSize = 12 });
        main.Children.Add(new TextBlock { Text = "Current Windows session", Margin = new(0, 8, 0, 4), FontWeight = FontWeights.SemiBold });
        main.Children.Add(new TextBlock { Text = "Saved profiles apply the connection and selected login type. Therefore handles sign-in.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 24) });
        main.Children.Add(apps); RefreshApplications();
        main.Children.Add(status);
        main.Children.Add(new TextBlock { Text = "Open multiple apps with the same connection. Close clients before switching tenants or login settings.\nThe selected connection remains configured after launch.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        RefreshProfiles(); status.Text = "Choose a saved tenant, or add your first connection.";
        InitializeCompactMode(sidebar, main, title, tenantLabel);
    }
    void RefreshApplications()
    {
        apps.Children.Clear();
        foreach (var name in VisibleApplications())
        {
            var panel = new StackPanel { Margin = new(12) };
            panel.Children.Add(new Image { Source = new BitmapImage(new Uri($"pack://application:,,,/assets/icons/{name.ToLowerInvariant()}.png")), Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 12) });
            panel.Children.Add(new TextBlock { Text = name, FontSize = 17, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(Button("Launch", (_, _) => Launch(name), true));
            apps.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new(1), CornerRadius = new(8), Margin = new(0, 0, 10, 0), Child = panel });
        }
    }
    static Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button { Content = text, Padding = new(12, 9, 12, 9), Margin = new(0, 6, 0, 0), Background = primary ? Gold : Brushes.White, BorderBrush = Brushes.LightGray };
        button.Click += click; return button;
    }
    void RefreshProfiles(TenantProfile? selection = null)
    {
        profiles.ItemsSource = null; profiles.ItemsSource = settings.Profiles;
        profiles.SelectedItem = selection ?? settings.Profiles.FirstOrDefault(); RefreshDetail();
    }
    void RefreshDetail()
    {
        var p = profiles.SelectedItem as TenantProfile;
        heading.Text = p?.Name ?? "Your tenant connections";
        address.Text = p is null ? "Add a tenant to get started." : $"{p.Url}\nLogin type: {p.LoginType}";
    }
    void Launch(string app)
    {
        if (profiles.SelectedItem is not TenantProfile p) { status.Text = "Select a tenant first."; return; }
        try { Launcher.Launch(p, app, settings.InstallDirectory); status.Text = $"Started {app} for {p.Name}. Complete sign-in in Therefore."; }
        catch (Exception ex)
        {
            status.Text = ex.Message;
            if (settings.Window.CompactMode) MessageBox.Show(this, ex.Message, "Unable to launch Therefore", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    void Edit(TenantProfile? existing)
    {
        var dialog = new Window { Title = existing is null ? "Add tenant" : "Edit tenant", Owner = this, Width = 650, Height = 760, MinWidth = 480, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background };
        var panel = new StackPanel { Margin = new(24) };
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Display name" });
        var name = new TextBox { Text = existing?.Name ?? "", Padding = new(8), Margin = new(0, 5, 0, 16) }; panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = "Tenant subdomain" });
        var tenant = new TextBox { Text = existing?.Tenant ?? "", Padding = new(8), Margin = new(0, 5, 0, 16) }; panel.Children.Add(tenant);
        panel.Children.Add(new TextBlock { Text = "XML server URL" });
        var server = new TextBox { Text = existing?.Url ?? "", Padding = new(8), Margin = new(0, 5, 0, 16) }; panel.Children.Add(server);
        tenant.TextChanged += (_, _) => server.Text = $"https://{tenant.Text.Trim()}.thereforeonline.com/TheXMLServer";
        panel.Children.Add(new TextBlock { Text = "Login type" });
        var options = new[] { new LoginOption(0, "Username and password"), new LoginOption(2, "Entra ID") };
        var login = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = options.FirstOrDefault(x => x.Mode == existing?.AuthenticationMode), Padding = new(8), Margin = new(0, 5, 0, 8) };
        panel.Children.Add(login);
        var protectedLogin = existing?.ProtectedLogin;
        var loginPanel = new StackPanel(); panel.Children.Add(loginPanel);
        var savedLoginStatus = new TextBlock { Text = protectedLogin is null ? "No login saved for this profile." : "A Therefore login is saved for this profile.", TextWrapping = TextWrapping.Wrap };
        loginPanel.Children.Add(new TextBlock { Text = "Sign in to this connection in Therefore with Remember password enabled, then save that login here. Saved logins are encrypted for your Windows account.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 8) });
        loginPanel.Children.Add(savedLoginStatus);
        loginPanel.Children.Add(Button("Save current Therefore login", (_, _) =>
        {
            try
            {
                var candidate = new TenantProfile(name.Text.Trim(), tenant.Text.Trim().ToLowerInvariant(), 0, null, server.Text.Trim());
                using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using var key = hive.OpenSubKey(@"Software\Therefore\Client")
                    ?? throw new InvalidOperationException("Configure Therefore and sign in first.");
                protectedLogin = SavedLogin.Capture(candidate, keyName => key.GetValue(keyName));
                savedLoginStatus.Text = "Current Therefore login captured. Save the profile to keep it.";
            }
            catch (Exception ex) { savedLoginStatus.Text = ex.Message; }
        }));
        loginPanel.Children.Add(Button("Remove saved login", (_, _) =>
        {
            protectedLogin = null; savedLoginStatus.Text = "No login saved. Save the profile to apply this change.";
        }));
        void ClearSavedLogin()
        {
            protectedLogin = null; savedLoginStatus.Text = "Connection changed. Save its Therefore login again if needed.";
        }
        tenant.TextChanged += (_, _) => ClearSavedLogin();
        server.TextChanged += (_, _) => ClearSavedLogin();
        var oidcPanel = new StackPanel(); panel.Children.Add(oidcPanel);
        TextBox Field(string label, string value)
        {
            oidcPanel.Children.Add(new TextBlock { Text = label });
            var input = new TextBox { Text = value, Padding = new(6), Margin = new(0, 4, 0, 10) };
            oidcPanel.Children.Add(input); return input;
        }
        var oidc = existing?.Oidc ?? new OidcSettings();
        var authUrl = Field("Authorization endpoint", oidc.AuthorizationEndpoint);
        var tokenUrl = Field("Token endpoint (optional)", oidc.TokenEndpoint);
        var clientId = Field("Therefore client ID", oidc.ClientId);
        var scopes = Field("Scopes", oidc.Scopes);
        var additional = Field("Additional parameters (optional; no secrets)", oidc.AdditionalParameters);
        var pkce = new CheckBox { Content = "Enable PKCE", IsChecked = oidc.EnablePkce, Margin = new(0, 4, 0, 4) }; oidcPanel.Children.Add(pkce);
        var browser = new CheckBox { Content = "Use the browser for login", IsChecked = oidc.UseBrowser, Margin = new(0, 4, 0, 4) }; oidcPanel.Children.Add(browser);
        var sso = new CheckBox { Content = "Use Windows Accounts and Single Sign-On (SSO)", IsChecked = oidc.UseWindowsSso, Margin = new(0, 4, 0, 4) }; oidcPanel.Children.Add(sso);
        // Match Therefore's dependency without silently changing saved values.
        void UpdateSso() => sso.IsEnabled = browser.IsChecked != true;
        browser.Checked += (_, _) => UpdateSso(); browser.Unchecked += (_, _) => UpdateSso(); UpdateSso();
        void UpdateVisibility()
        {
            oidcPanel.Visibility = (login.SelectedItem as LoginOption)?.Mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            loginPanel.Visibility = (login.SelectedItem as LoginOption)?.Mode == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        login.SelectionChanged += (_, _) => UpdateVisibility(); UpdateVisibility();
        var validation = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) }; panel.Children.Add(validation);
        panel.Children.Insert(0, Button("Read current configuration", (_, _) =>
        {
            try
            {
                var captured = ConnectionConfiguration.ReadCurrent();
                // The entire snapshot is validated before changing any editor fields.
                var previousTenant = tenant.Text.Trim();
                if (string.IsNullOrWhiteSpace(name.Text) || string.Equals(name.Text.Trim(), previousTenant, StringComparison.OrdinalIgnoreCase)) name.Text = captured.Tenant;
                tenant.Text = captured.Tenant; server.Text = captured.Url;
                login.SelectedItem = options.First(x => x.Mode == captured.AuthenticationMode);
                var loaded = captured.Oidc ?? new OidcSettings();
                authUrl.Text = loaded.AuthorizationEndpoint; tokenUrl.Text = loaded.TokenEndpoint;
                clientId.Text = loaded.ClientId; scopes.Text = loaded.Scopes; additional.Text = loaded.AdditionalParameters;
                pkce.IsChecked = loaded.EnablePkce; browser.IsChecked = loaded.UseBrowser; sso.IsChecked = loaded.UseWindowsSso;
                validation.Text = $"Read {captured.Tenant}: {captured.LoginType}. Review and save to store this configuration. Passwords and login tokens are not copied.";
            }
            catch (Exception ex) { validation.Text = ex.Message; }
        }));
        panel.Children.Add(Button("Save profile", (_, _) =>
        {
            var subdomain = tenant.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name.Text) || !Launcher.ValidTenant(subdomain)) { validation.Text = "Enter a name and valid tenant subdomain (letters, digits and internal hyphens)."; return; }
            if (login.SelectedItem is not LoginOption selectedLogin) { validation.Text = "Select a supported login type."; return; }
            var editedOidc = new OidcSettings
            {
                AuthorizationEndpoint = authUrl.Text.Trim(), TokenEndpoint = tokenUrl.Text.Trim(),
                ClientId = clientId.Text.Trim(), Scopes = scopes.Text.Trim(), AdditionalParameters = additional.Text.Trim(),
                EnablePkce = pkce.IsChecked == true, UseBrowser = browser.IsChecked == true, UseWindowsSso = sso.IsChecked == true
            };
            if (selectedLogin.Mode == 2)
            {
                try { editedOidc.Validate(); } catch (Exception ex) { validation.Text = ex.Message; return; }
            }
            var replacement = new TenantProfile(name.Text.Trim(), subdomain, selectedLogin.Mode, selectedLogin.Mode == 2 ? editedOidc : null, server.Text.Trim()) { ProtectedLogin = selectedLogin.Mode == 0 ? protectedLogin : null };
            try { replacement.Validate(); } catch (Exception ex) { validation.Text = ex.Message; return; }
            var updated = settings.Profiles.ToList();
            if (existing is null) updated.Add(replacement); else updated[updated.IndexOf(existing)] = replacement;
            var previous = settings.Profiles; settings.Profiles = updated;
            try { Launcher.Save(settings); RefreshProfiles(replacement); dialog.Close(); }
            catch (Exception ex) { settings.Profiles = previous; validation.Text = ex.Message; }
        }, true));
        dialog.ShowDialog();
    }
    sealed record LoginOption(int? Mode, string Label);
    void Remove()
    {
        if (profiles.SelectedItem is not TenantProfile p || MessageBox.Show($"Remove saved profile '{p.Name}'?", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var previous = settings.Profiles; settings.Profiles = previous.Where(x => !ReferenceEquals(x, p)).ToList();
        try { Launcher.Save(settings); RefreshProfiles(); } catch (Exception ex) { settings.Profiles = previous; status.Text = ex.Message; }
    }
    void ChooseFolder()
    {
        var picker = new OpenFolderDialog { Title = "Choose the Therefore installation folder", InitialDirectory = settings.InstallDirectory };
        if (picker.ShowDialog(this) != true) return;
        var previous = settings.InstallDirectory; settings.InstallDirectory = picker.FolderName;
        try { Launcher.Save(settings); status.Text = "Installation folder: " + settings.InstallDirectory; }
        catch (Exception ex) { settings.InstallDirectory = previous; status.Text = ex.Message; }
    }
}
