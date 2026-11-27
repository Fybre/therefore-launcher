using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThereforeLauncher;

public sealed partial class MainWindow
{
    void InitializeCompactMode(DockPanel sidebar, FrameworkElement details, Border title, FrameworkElement tenantLabel)
    {
        settings.Window ??= new WindowPreferences();
        var caption = new TextBlock { Text = "∴  Therefore Launcher", FontSize = 21, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var modeIcon = new System.Windows.Shapes.Path
        {
            Width = 18, Height = 18, Stretch = Stretch.Uniform, Stroke = Foreground,
            StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false
        };
        var modeToggle = new Button { Content = modeIcon, Width = 32, Height = 32, Padding = new(6), Margin = new(8, 0, 0, 0), Background = Brushes.White, BorderBrush = Brushes.LightGray };
        var chrome = new DockPanel();
        DockPanel.SetDock(modeToggle, Dock.Right); chrome.Children.Add(modeToggle); chrome.Children.Add(caption); title.Child = chrome;
        var compactStatus = new TextBlock { FontSize = 12, Foreground = Brushes.DimGray, Margin = new(2, 6, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis };
        compactStatus.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = status });
        compactStatus.SetBinding(ToolTipProperty, new Binding(nameof(TextBlock.Text)) { Source = status });
        DockPanel.SetDock(compactStatus, Dock.Bottom); sidebar.Children.Insert(1, compactStatus);

        void RememberSize()
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (settings.Window.CompactMode) { settings.Window.CompactWidth = bounds.Width; settings.Window.CompactHeight = bounds.Height; }
            else { settings.Window.FullWidth = bounds.Width; settings.Window.FullHeight = bounds.Height; }
        }
        void ApplyMode()
        {
            var compact = settings.Window.CompactMode;
            WindowState = WindowState.Normal;
            MinWidth = compact ? 280 : 850; MinHeight = compact ? 300 : 570;
            var area = SystemParameters.WorkArea;
            static double Dimension(double saved, double fallback, double minimum, double maximum)
                => Math.Clamp(double.IsFinite(saved) ? saved : fallback, minimum, Math.Max(minimum, maximum));
            Width = Dimension(compact ? settings.Window.CompactWidth : settings.Window.FullWidth, compact ? 320 : 1020, MinWidth, area.Width);
            Height = Dimension(compact ? settings.Window.CompactHeight : settings.Window.FullHeight, compact ? 520 : 640, MinHeight, area.Height);
            if (IsLoaded)
            {
                Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
                Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
            }
            details.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            tenantLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            compactStatus.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            sidebar.Width = compact ? double.NaN : 260;
            sidebar.Margin = compact ? new Thickness(8) : new Thickness(16);
            DockPanel.SetDock(sidebar, Dock.Left);
            // Put the sidebar in the fill slot only while details are hidden.
            var shell = (DockPanel)Content;
            shell.Children.Remove(sidebar);
            if (compact) shell.Children.Add(sidebar); else shell.Children.Insert(1, sidebar);
            shell.LastChildFill = true;
            profiles.ItemContainerStyle = (Style)Resources[compact ? "CompactTenantItemStyle" : "TenantItemStyle"];
            profiles.ItemTemplate = (DataTemplate)Resources[compact ? "CompactTenantItemTemplate" : "TenantItemTemplate"];
            modeIcon.Data = Geometry.Parse(compact
                ? "M2,7 V2 H7 M2,2 L8,8 M12,17 H17 V12 M17,17 L11,11"
                : "M2,2 L8,8 M3,8 H8 V3 M17,17 L11,11 M11,16 V11 H16");
            modeToggle.ToolTip = compact ? "Full window" : "Compact mode";
            System.Windows.Automation.AutomationProperties.SetName(modeToggle, compact ? "Full window" : "Compact mode");
            caption.FontSize = compact ? 14 : 21;
            title.Padding = compact ? new Thickness(10, 7, 10, 7) : new Thickness(24, 14, 24, 14);
            profiles.ToolTip = compact ? "Double-click or Enter: first visible app. Right-click or ⋯: choose app. Drag to reorder." : "Drag tenants to reorder, or press Ctrl+Up / Ctrl+Down.";
        }
        modeToggle.Click += (_, _) =>
        {
            RememberSize(); settings.Window.CompactMode = !settings.Window.CompactMode; ApplyMode();
            try { Launcher.Save(settings); } catch (Exception ex) { status.Text = "Could not save window preferences: " + ex.Message; }
        };
        Closing += (_, _) =>
        {
            RememberSize();
            try { Launcher.Save(settings); }
            catch (Exception ex) { MessageBox.Show(this, "Could not save window preferences: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        };

        var appMenu = new ContextMenu();
        var menuTenant = new MenuItem { IsEnabled = false, FontWeight = FontWeights.SemiBold };
        void RefreshAppMenu()
        {
        appMenu.Items.Clear();
        appMenu.Items.Add(menuTenant);
        appMenu.Items.Add(new Separator());
        foreach (var app in VisibleApplications())
        {
            var item = new MenuItem
            {
                Header = app,
                Style = (Style)Resources["LargeAppMenuItemStyle"],
                Icon = new Image { Source = new BitmapImage(new Uri($"pack://application:,,,/assets/icons/{app.ToLowerInvariant()}.png")), Width = 32, Height = 32 },
                InputGestureText = app == VisibleApplications().FirstOrDefault() ? "Enter" : ""
            };
            item.Click += (_, _) => Launch(app); appMenu.Items.Add(item);
        }
        appMenu.Items.Add(new Separator());
        var edit = new MenuItem { Header = "Edit profile…" };
        edit.Click += (_, _) => { if (profiles.SelectedItem is TenantProfile p) Edit(p); }; appMenu.Items.Add(edit);
        appMenu.Items.Add(CreateMoveToGroupMenu());
        }
        profiles.ContextMenu = appMenu;
        appMenu.Opened += (_, _) =>
        {
            RefreshAppMenu();
            menuTenant.Header = (profiles.SelectedItem as TenantProfile)?.Name ?? "Select a tenant";
            foreach (var item in appMenu.Items.OfType<MenuItem>().Where(item => item != menuTenant)) item.IsEnabled = profiles.SelectedItem is TenantProfile;
        };
        profiles.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(profiles, e.OriginalSource as DependencyObject) is ListBoxItem row) row.IsSelected = true;
            else e.Handled = true;
        };
        profiles.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is not Button { Tag: "TenantLaunchMenu", DataContext: TenantProfile profile } button) return;
            profiles.SelectedItem = profile;
            appMenu.PlacementTarget = button; appMenu.Placement = PlacementMode.Bottom; appMenu.IsOpen = true; e.Handled = true;
        }));
        profiles.ContextMenuOpening += (_, _) =>
        {
            appMenu.PlacementTarget = profiles; appMenu.Placement = PlacementMode.MousePoint;
        };
        profiles.MouseDoubleClick += (_, e) =>
        {
            if (!settings.Window.CompactMode || e.ChangedButton != MouseButton.Left || IsMenuButton(e.OriginalSource as DependencyObject)) return;
            if (ItemsControl.ContainerFromElement(profiles, e.OriginalSource as DependencyObject) is ListBoxItem row)
            {
                profiles.SelectedItem = row.DataContext; LaunchDefaultApplication(); e.Handled = true;
            }
        };
        profiles.PreviewKeyDown += (_, e) =>
        {
            if (settings.Window.CompactMode && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && !IsMenuButton(Keyboard.FocusedElement as DependencyObject))
            { LaunchDefaultApplication(); e.Handled = true; }
        };
        ApplyMode();
    }

    static bool IsMenuButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button { Tag: "TenantLaunchMenu" }) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : null;
        }
        return false;
    }
}
