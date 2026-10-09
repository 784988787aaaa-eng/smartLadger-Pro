using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLedger.App.Behaviors;

public static class QuickActionDragBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(QuickActionDragBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static readonly DependencyProperty DragStartProperty =
        DependencyProperty.RegisterAttached("DragStart", typeof(Point?), typeof(QuickActionDragBehavior));
    private static readonly DependencyProperty DragItemProperty =
        DependencyProperty.RegisterAttached("DragItem", typeof(object), typeof(QuickActionDragBehavior));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl items) return;
        if ((bool)e.NewValue)
        {
            items.PreviewMouseLeftButtonDown += OnMouseDown;
            items.PreviewMouseMove += OnMouseMove;
            items.DragOver += OnDragOver;
            items.Drop += OnDrop;
            items.AllowDrop = true;
        }
        else
        {
            items.PreviewMouseLeftButtonDown -= OnMouseDown;
            items.PreviewMouseMove -= OnMouseMove;
            items.DragOver -= OnDragOver;
            items.Drop -= OnDrop;
            items.AllowDrop = false;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl items || e.OriginalSource is not DependencyObject source) return;
        var button = FindParent<Button>(source);
        if (button?.DataContext is null) return;
        items.SetValue(DragStartProperty, e.GetPosition(items));
        items.SetValue(DragItemProperty, button.DataContext);
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ItemsControl items || e.LeftButton != MouseButtonState.Pressed) return;
        var start = (Point?)items.GetValue(DragStartProperty);
        var item = items.GetValue(DragItemProperty);
        if (start is null || item is null) return;
        var current = e.GetPosition(items);
        if (Math.Abs(current.X - start.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var source = FindParent<Button>(e.OriginalSource as DependencyObject);
        if (source is null) return;
        DragDrop.DoDragDrop(source, new DataObject(typeof(object), item), DragDropEffects.Move);
        items.ClearValue(DragStartProperty);
        items.ClearValue(DragItemProperty);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(object)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl items || !e.Data.GetDataPresent(typeof(object))) return;
        if (items.ItemsSource is not IList list) return;
        var sourceItem = e.Data.GetData(typeof(object));
        if (sourceItem is null) return;
        var target = FindParent<Button>(e.OriginalSource as DependencyObject)?.DataContext;
        if (target is null || ReferenceEquals(sourceItem, target)) return;

        var sourceIndex = list.IndexOf(sourceItem);
        var targetIndex = list.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0) return;
        list.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex) targetIndex--;
        list.Insert(targetIndex, sourceItem);
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
