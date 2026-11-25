using System.Windows;
using System.Windows.Controls;

namespace ThereforeLauncher;

public sealed partial class MainWindow
{
    static readonly string[] AvailableApplications = ["Navigator", "Designer", "Console"];
    IEnumerable<string> OrderedApplications() => (settings.ApplicationOrder ?? []).Concat(AvailableApplications)
        .Where(AvailableApplications.Contains).Distinct();
    IEnumerable<string> VisibleApplications() => OrderedApplications().Where(app => !(settings.HiddenApplications ?? []).Contains(app));

    void LaunchDefaultApplication()
    {
        if (VisibleApplications().FirstOrDefault() is string app) Launch(app);
        else status.Text = "All application icons are hidden. Use More → Application icons to show an application.";
    }

    void EditApplications()
    {
        var order = OrderedApplications().ToList();
        var hidden = new HashSet<string>(settings.HiddenApplications ?? []);
        var dialog = new Window { Title = "Application icons", Owner = this, Width = 420, Height = 410,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Background };
        var panel = new StackPanel { Margin = new(24) }; dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Choose which apps to show and their order.", Margin = new(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "The first visible app opens on double-click or Enter in compact mode.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) });
        var rows = new StackPanel(); panel.Children.Add(rows);
        void Refresh()
        {
            rows.Children.Clear();
            foreach (var app in order.ToArray())
            {
                var row = new DockPanel { Margin = new(0, 3, 0, 3) };
                var down = Button("↓", (_, _) => Move(app, 1)); down.IsEnabled = order.IndexOf(app) < order.Count - 1;
                var up = Button("↑", (_, _) => Move(app, -1)); up.IsEnabled = order.IndexOf(app) > 0;
                System.Windows.Automation.AutomationProperties.SetName(up, "Move " + app + " up");
                System.Windows.Automation.AutomationProperties.SetName(down, "Move " + app + " down");
                DockPanel.SetDock(down, Dock.Right); row.Children.Add(down);
                DockPanel.SetDock(up, Dock.Right); row.Children.Add(up);
                var enabled = new CheckBox { Content = app, IsChecked = !hidden.Contains(app), VerticalAlignment = VerticalAlignment.Center };
                enabled.Checked += (_, _) => hidden.Remove(app); enabled.Unchecked += (_, _) => hidden.Add(app);
                row.Children.Add(enabled); rows.Children.Add(row);
            }
        }
        void Move(string app, int offset)
        {
            var index = order.IndexOf(app); var target = index + offset;
            if (target < 0 || target >= order.Count) return;
            (order[index], order[target]) = (order[target], order[index]); Refresh();
        }
        Refresh();
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        panel.Children.Add(Button("Restore defaults", (_, _) => { order = AvailableApplications.ToList(); hidden.Clear(); Refresh(); }));
        panel.Children.Add(Button("Save", (_, _) =>
        {
            var previousOrder = settings.ApplicationOrder; var previousHidden = settings.HiddenApplications ?? [];
            settings.ApplicationOrder = order.ToList(); settings.HiddenApplications = hidden.ToList();
            try { Launcher.Save(settings); RefreshApplications(); dialog.Close(); }
            catch (Exception ex) { settings.ApplicationOrder = previousOrder; settings.HiddenApplications = previousHidden; error.Text = ex.Message; }
        }, true));
        dialog.ShowDialog();
    }
}
