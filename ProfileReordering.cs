using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ThereforeLauncher;

public sealed partial class MainWindow
{
    readonly string dragFormat = "ThereforeLauncher.ProfileOrder." + Guid.NewGuid();
    Point dragStart;
    TenantProfile? dragProfile;
    TenantGroup? dragGroup;
    InsertionMarker? insertionMarker;
    AdornerLayer? insertionLayer;

    void EnableProfileReordering()
    {
        profiles.AllowDrop = true;
        profiles.ToolTip = "Drag tenants to reorder, or select a tenant and press Ctrl+Up / Ctrl+Down.";
        profiles.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragGroup = null;
            if (Ancestor<Button>(e.OriginalSource as DependencyObject)?.Tag is "GroupMenu") { dragProfile = null; return; }
            if (IsMenuButton(e.OriginalSource as DependencyObject)) { dragProfile = null; return; }
            dragStart = e.GetPosition(profiles);
            dragProfile = (ItemsControl.ContainerFromElement(profiles, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as TenantProfile;
            if (dragProfile is null && Ancestor<GroupItem>(e.OriginalSource as DependencyObject)?.DataContext is System.Windows.Data.CollectionViewGroup { Name: TenantGroup group }) dragGroup = group;
        };
        profiles.PreviewMouseLeftButtonUp += (_, _) => { dragProfile = null; dragGroup = null; };
        profiles.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || (dragProfile is null && dragGroup is null)) return;
            var delta = e.GetPosition(profiles) - dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            if (dragProfile is not null) profiles.SelectedItem = dragProfile;
            try { DragDrop.DoDragDrop(profiles, new DataObject(dragFormat, true), DragDropEffects.Move); }
            finally { dragProfile = null; dragGroup = null; ClearInsertionMarker(); }
            e.Handled = true;
        };
        profiles.DragOver += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None;
            if ((dragProfile is null && dragGroup is null) || !e.Data.GetDataPresent(dragFormat)) { ClearInsertionMarker(); return; }
            e.Effects = DragDropEffects.Move;
            var point = e.GetPosition(profiles);
            var scroll = FindChild<ScrollViewer>(profiles);
            if (point.Y < 24) scroll?.LineUp();
            else if (point.Y > profiles.ActualHeight - 24) scroll?.LineDown();
            var (_, _, item, below) = DropTarget(point);
            if (dragGroup is not null) { var target = GroupDropTarget(point); item = target.Item; below = target.Below; }
            ClearInsertionMarker();
            if (item is not null && AdornerLayer.GetAdornerLayer(item) is { } layer)
            {
                insertionLayer = layer; insertionMarker = new InsertionMarker(item, below);
                layer.Add(insertionMarker);
            }
        };
        profiles.DragLeave += (_, _) => ClearInsertionMarker();
        profiles.Drop += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None; ClearInsertionMarker();
            if (!e.Data.GetDataPresent(dragFormat)) return;
            if (dragGroup is not null)
            {
                if (SaveGroupOrder(dragGroup, GroupDropTarget(e.GetPosition(profiles)).Slot)) e.Effects = DragDropEffects.Move;
                return;
            }
            if (dragProfile is null) return;
            var (group, slot, _, _) = DropTarget(e.GetPosition(profiles));
            if (SaveProfileMove(dragProfile, group, slot)) e.Effects = DragDropEffects.Move;
        };
        profiles.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || e.Key is not (Key.Up or Key.Down)) return;
            MoveSelectedProfile(e.Key == Key.Up ? -1 : 1); e.Handled = true;
        };
    }

    (int Slot, GroupItem? Item, bool Below) GroupDropTarget(Point point)
    {
        var groups = OrderedGroups();
        var item = Ancestor<GroupItem>(profiles.InputHitTest(point) as DependencyObject);
        if (item?.DataContext is System.Windows.Data.CollectionViewGroup { Name: TenantGroup group })
        {
            var below = point.Y >= item.TranslatePoint(new Point(), profiles).Y + item.ActualHeight / 2;
            return (groups.IndexOf(group) + (below ? 1 : 0), item, below);
        }
        GroupItem? last = null;
        foreach (var container in GroupContainers(profiles))
        {
            if (point.Y < container.TranslatePoint(new Point(), profiles).Y)
            {
                var nextGroup = (TenantGroup)((System.Windows.Data.CollectionViewGroup)container.DataContext).Name;
                return (groups.IndexOf(nextGroup), container, false);
            }
            last = container;
        }
        return (groups.Count, last, true);
    }

    static IEnumerable<GroupItem> GroupContainers(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is GroupItem group) yield return group;
            else foreach (var nested in GroupContainers(child)) yield return nested;
        }
    }

    (TenantGroup Group, int Slot, FrameworkElement? Item, bool Below) DropTarget(Point point)
    {
        var hit = profiles.InputHitTest(point) as DependencyObject;
        var row = Ancestor<ListBoxItem>(hit);
        if (row?.DataContext is TenantProfile target)
        {
            var group = GroupFor(target);
            var members = settings.Profiles.Where(p => GroupFor(p) == group).ToList();
            var below = point.Y >= row.TranslatePoint(new Point(), profiles).Y + row.ActualHeight / 2;
            return (group, members.FindIndex(p => ReferenceEquals(p, target)) + (below ? 1 : 0), row, below);
        }
        if (Ancestor<GroupItem>(hit) is { DataContext: System.Windows.Data.CollectionViewGroup { Name: TenantGroup destination } } header)
            return (destination, settings.Profiles.Count(p => GroupFor(p) == destination), header, false);
        // Blank space beneath the groups belongs to Ungrouped.
        return (settings.Ungrouped, settings.Profiles.Count(p => GroupFor(p) == settings.Ungrouped), null, true);
    }

    bool CanMoveSelectedProfile(int direction)
    {
        if (profiles.SelectedItem is not TenantProfile selected) return false;
        var members = settings.Profiles.Where(p => GroupFor(p) == GroupFor(selected)).ToList();
        var index = members.FindIndex(p => ReferenceEquals(p, selected));
        return index + direction >= 0 && index + direction < members.Count;
    }

    void MoveSelectedProfile(int direction)
    {
        if (!CanMoveSelectedProfile(direction) || profiles.SelectedItem is not TenantProfile selected) return;
        var group = GroupFor(selected);
        var members = settings.Profiles.Where(p => GroupFor(p) == group).ToList();
        var index = members.FindIndex(p => ReferenceEquals(p, selected));
        SaveProfileMove(selected, group, direction < 0 ? index - 1 : index + 2);
    }

    bool SaveProfileMove(TenantProfile selected, TenantGroup group, int slot)
    {
        var previous = settings.Profiles;
        var previousExpanded = group.IsExpanded;
        var result = ProfileOrder.MoveToGroup(previous, selected, group == settings.Ungrouped ? null : group.Id, slot,
            p => GroupFor(p) == group);
        settings.Profiles = result.Profiles;
        group.IsExpanded = true;
        try
        {
            Launcher.Save(settings);
            RefreshProfiles(result.Selected); profiles.ScrollIntoView(result.Selected);
            status.Text = "Tenant group and order saved.";
            return true;
        }
        catch (Exception ex)
        {
            settings.Profiles = previous;
            group.IsExpanded = previousExpanded;
            status.Text = "Could not save tenant move: " + ex.Message;
            return false;
        }
    }

    void ClearInsertionMarker()
    {
        if (insertionMarker is not null) insertionLayer?.Remove(insertionMarker);
        insertionMarker = null; insertionLayer = null;
    }
    static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
    sealed class InsertionMarker(UIElement element, bool below) : Adorner(element)
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            IsHitTestVisible = false;
            var y = below ? AdornedElement.RenderSize.Height : 0;
            drawingContext.DrawLine(new Pen(Gold, 3), new Point(4, y), new Point(AdornedElement.RenderSize.Width - 4, y));
        }
    }
}
