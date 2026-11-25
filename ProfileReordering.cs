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
    InsertionMarker? insertionMarker;
    AdornerLayer? insertionLayer;

    void EnableProfileReordering()
    {
        profiles.AllowDrop = true;
        profiles.ToolTip = "Drag tenants to reorder, or select a tenant and press Ctrl+Up / Ctrl+Down.";
        profiles.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragStart = e.GetPosition(profiles);
            dragProfile = (ItemsControl.ContainerFromElement(profiles, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as TenantProfile;
        };
        profiles.PreviewMouseLeftButtonUp += (_, _) => dragProfile = null;
        profiles.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragProfile is null) return;
            var delta = e.GetPosition(profiles) - dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            profiles.SelectedItem = dragProfile;
            try { DragDrop.DoDragDrop(profiles, new DataObject(dragFormat, true), DragDropEffects.Move); }
            finally { dragProfile = null; ClearInsertionMarker(); }
            e.Handled = true;
        };
        profiles.DragOver += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None;
            if (dragProfile is null || !e.Data.GetDataPresent(dragFormat)) { ClearInsertionMarker(); return; }
            e.Effects = DragDropEffects.Move;
            var point = e.GetPosition(profiles);
            var scroll = FindChild<ScrollViewer>(profiles);
            if (point.Y < 24) scroll?.LineUp();
            else if (point.Y > profiles.ActualHeight - 24) scroll?.LineDown();
            var (_, item, below) = DropTarget(point);
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
            if (dragProfile is null || !e.Data.GetDataPresent(dragFormat)) return;
            var source = settings.Profiles.FindIndex(p => ReferenceEquals(p, dragProfile));
            if (source < 0) return;
            var (slot, _, _) = DropTarget(e.GetPosition(profiles));
            if (SaveProfileOrder(source, slot)) e.Effects = DragDropEffects.Move;
        };
        profiles.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || e.Key is not (Key.Up or Key.Down)) return;
            MoveSelectedProfile(e.Key == Key.Up ? -1 : 1); e.Handled = true;
        };
    }

    (int Slot, ListBoxItem? Item, bool Below) DropTarget(Point point)
    {
        ListBoxItem? last = null; var lastIndex = -1;
        for (var i = 0; i < settings.Profiles.Count; i++)
        {
            if (profiles.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item) continue;
            var top = item.TranslatePoint(new Point(), profiles).Y;
            if (point.Y < top + item.ActualHeight / 2) return (i, item, false);
            last = item; lastIndex = i;
        }
        return (lastIndex >= 0 ? lastIndex + 1 : settings.Profiles.Count, last, true);
    }

    void MoveSelectedProfile(int direction)
    {
        var index = profiles.SelectedIndex;
        if (index < 0 || index + direction < 0 || index + direction >= settings.Profiles.Count) return;
        SaveProfileOrder(index, direction < 0 ? index - 1 : index + 2);
    }

    bool SaveProfileOrder(int source, int slot)
    {
        if (slot == source || slot == source + 1) return true;
        var previous = settings.Profiles;
        var selected = previous[source];
        settings.Profiles = ProfileOrder.Move(previous, source, slot);
        try
        {
            Launcher.Save(settings);
            RefreshProfiles(selected); profiles.ScrollIntoView(selected);
            status.Text = "Tenant order saved.";
            return true;
        }
        catch (Exception ex)
        {
            settings.Profiles = previous;
            status.Text = "Could not save tenant order: " + ex.Message;
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
