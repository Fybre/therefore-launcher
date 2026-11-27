using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ThereforeLauncher;

public sealed partial class MainWindow
{
    TenantGroup GroupFor(TenantProfile profile) => settings.Groups.FirstOrDefault(g => g.Id == profile.GroupId) ?? settings.Ungrouped;

    List<TenantGroup> OrderedGroups() => settings.Groups.Append(settings.Ungrouped)
        .OrderBy(g => settings.GroupOrder.IndexOf(g.Id) is var index && index >= 0 ? index : int.MaxValue).ToList();

    bool SaveGroupOrder(TenantGroup group, int slot)
    {
        var groups = OrderedGroups(); var source = groups.IndexOf(group);
        if (source < 0 || slot < 0 || slot > groups.Count) return false;
        if (slot == source || slot == source + 1) return true;
        var previous = settings.GroupOrder;
        settings.GroupOrder = ProfileOrder.Move(groups, source, slot).Select(g => g.Id).ToList();
        try
        {
            Launcher.Save(settings); RefreshProfiles(profiles.SelectedItem as TenantProfile);
            status.Text = "Group order saved."; return true;
        }
        catch (Exception ex) { settings.GroupOrder = previous; status.Text = "Could not save group order: " + ex.Message; return false; }
    }

    ListCollectionView CreateGroupedProfiles()
    {
        var view = new ListCollectionView(settings.Profiles);
        var grouping = new TenantGrouping(GroupFor);
        foreach (var group in OrderedGroups()) grouping.GroupNames.Add(group);
        view.GroupDescriptions.Add(grouping);
        return view;
    }

    void InitializeTenantGroups()
    {
        profiles.GroupStyle.Add(new GroupStyle { ContainerStyle = (Style)Resources["TenantGroupStyle"] });
        profiles.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is not Button { Tag: "GroupMenu", DataContext: CollectionViewGroup { Name: TenantGroup group } } button) return;
            var menu = new ContextMenu();
            var ordered = OrderedGroups(); var index = ordered.IndexOf(group);
            var up = new MenuItem { Header = "Move group up", IsEnabled = index > 0 };
            up.Click += (_, _) => SaveGroupOrder(group, index - 1); menu.Items.Add(up);
            var down = new MenuItem { Header = "Move group down", IsEnabled = index < ordered.Count - 1 };
            down.Click += (_, _) => SaveGroupOrder(group, index + 2); menu.Items.Add(down);
            menu.Items.Add(new Separator());
            if (group != settings.Ungrouped)
            {
                var rename = new MenuItem { Header = "Rename group…" };
                rename.Click += (_, _) => EditGroup(group); menu.Items.Add(rename);
                var delete = new MenuItem { Header = "Delete group (keep tenants)" };
                delete.Click += (_, _) => DeleteGroup(group); menu.Items.Add(delete);
            }
            else menu.Items.Add(new MenuItem { Header = "Ungrouped tenants", IsEnabled = false });
            menu.PlacementTarget = button; menu.IsOpen = true; e.Handled = true;
        }));
        // Persist the expander binding after the routed event completes.
        RoutedEventHandler remember = (_, e) =>
        {
            if (e.OriginalSource is not Expander { IsLoaded: true }) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Launcher.Save(settings); } catch (Exception ex) { status.Text = "Could not save group state: " + ex.Message; }
            }));
        };
        profiles.AddHandler(Expander.ExpandedEvent, remember);
        profiles.AddHandler(Expander.CollapsedEvent, remember);
    }

    MenuItem CreateMoveToGroupMenu()
    {
        var menu = new MenuItem { Header = "Move to group" };
        menu.SubmenuOpened += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var group in OrderedGroups())
            {
                var item = new MenuItem { Header = group.Name, IsCheckable = true,
                    IsChecked = profiles.SelectedItem is TenantProfile p && GroupFor(p) == group };
                item.Click += (_, _) =>
                {
                    if (profiles.SelectedItem is TenantProfile selected)
                        SaveProfileMove(selected, group, settings.Profiles.Count(p => GroupFor(p) == group));
                };
                menu.Items.Add(item);
            }
        };
        return menu;
    }

    void EditGroup(TenantGroup? existing)
    {
        var dialog = new Window { Title = existing is null ? "New tenant group" : "Rename group", Owner = this, Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background };
        var panel = new StackPanel { Margin = new(20) }; dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Group name" });
        var name = new TextBox { Text = existing?.Name ?? "", Padding = new(8), Margin = new(0,6,0,8) }; panel.Children.Add(name);
        var validation = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(validation);
        panel.Children.Add(Button(existing is null ? "Create group" : "Save name", (_, _) =>
        {
            var value = name.Text.Trim();
            if (value.Length == 0 || value.Equals("Ungrouped", StringComparison.OrdinalIgnoreCase) || settings.Groups.Any(g => g != existing && g.Name.Equals(value, StringComparison.OrdinalIgnoreCase)))
            { validation.Text = "Enter a unique group name."; return; }
            var group = existing ?? new TenantGroup(); var previous = group.Name;
            group.Name = value; if (existing is null) settings.Groups.Add(group);
            try { Launcher.Save(settings); RefreshProfiles(profiles.SelectedItem as TenantProfile); dialog.Close(); }
            catch (Exception ex) { group.Name = previous; if (existing is null) settings.Groups.Remove(group); validation.Text = ex.Message; }
        }, true));
        panel.Children.Add(Button("Cancel", (_, _) => dialog.Close()));
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); }; dialog.ShowDialog();
    }

    void DeleteGroup(TenantGroup group)
    {
        var previous = settings.Profiles; var index = settings.Groups.IndexOf(group);
        var selected = profiles.SelectedItem as TenantProfile;
        settings.Profiles = previous.Select(p => p.GroupId == group.Id ? p with { GroupId = null } : p).ToList();
        var selectedIndex = previous.FindIndex(p => ReferenceEquals(p, selected));
        settings.Groups.RemoveAt(index);
        try { Launcher.Save(settings); RefreshProfiles(selectedIndex < 0 ? null : settings.Profiles[selectedIndex]); }
        catch (Exception ex) { settings.Profiles = previous; settings.Groups.Insert(index, group); status.Text = "Could not delete group: " + ex.Message; }
    }

    static T? Ancestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match) return match;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }

    sealed class TenantGrouping(Func<TenantProfile, TenantGroup> groupFor) : GroupDescription
    {
        public override object GroupNameFromItem(object item, int level, CultureInfo culture) => groupFor((TenantProfile)item);
    }
}
