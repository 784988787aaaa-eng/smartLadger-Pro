using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLedger.App.Behaviors;

/// <summary>
/// Enables pointer dragging and double-click maximize/restore for a dialog host
/// contained in the current visual tree. State is kept per host, not globally.
/// </summary>
public static class DialogInteractionBehavior
{
    private sealed class HostState
    {
        public bool IsMaximized { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Thickness Margin { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
        public Transform? RenderTransform { get; set; }
    }

    private sealed class DragState
    {
        public Point LastPoint { get; set; }
        public FrameworkElement? Host { get; set; }
        public TranslateTransform? Transform { get; set; }
        public bool IsDragging { get; set; }
    }

    private static readonly ConditionalWeakTable<FrameworkElement, HostState> Hosts = new();
    private static readonly ConditionalWeakTable<FrameworkElement, DragState> Drags = new();

    public static readonly DependencyProperty IsDraggableProperty =
        DependencyProperty.RegisterAttached(
            "IsDraggable",
            typeof(bool),
            typeof(DialogInteractionBehavior),
            new PropertyMetadata(false, OnIsDraggableChanged));

    public static bool GetIsDraggable(DependencyObject element) =>
        (bool)element.GetValue(IsDraggableProperty);

    public static void SetIsDraggable(DependencyObject element, bool value) =>
        element.SetValue(IsDraggableProperty, value);

    private static void OnIsDraggableChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement header)
            return;

        if (args.NewValue is true)
        {
            header.MouseLeftButtonDown += OnMouseLeftButtonDown;
            header.MouseMove += OnMouseMove;
            header.MouseLeftButtonUp += OnMouseLeftButtonUp;
            header.LostMouseCapture += OnLostMouseCapture;
        }
        else
        {
            header.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            header.MouseMove -= OnMouseMove;
            header.MouseLeftButtonUp -= OnMouseLeftButtonUp;
            header.LostMouseCapture -= OnLostMouseCapture;
            EndDrag(header);
        }
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is not FrameworkElement header)
            return;

        var host = FindDialogHost(header);
        if (host is null)
            return;

        if (args.ClickCount == 2)
        {
            ToggleMaximize(host);
            args.Handled = true;
            return;
        }

        if (args.ChangedButton != MouseButton.Left)
            return;

        var state = Drags.GetValue(header, static _ => new DragState());
        state.Host = host;
        state.LastPoint = args.GetPosition(host);
        state.IsDragging = true;

        var transform = new TranslateTransform();
        if (host.RenderTransform is TransformGroup group)
        {
            group.Children.Add(transform);
        }
        else if (host.RenderTransform is { } original && original != Transform.Identity)
        {
            var replacement = new TransformGroup();
            replacement.Children.Add(original);
            replacement.Children.Add(transform);
            host.RenderTransform = replacement;
        }
        else
        {
            host.RenderTransform = transform;
        }
        state.Transform = transform;

        header.CaptureMouse();
        args.Handled = true;
    }

    private static void OnMouseMove(object sender, MouseEventArgs args)
    {
        if (sender is not FrameworkElement header || !Drags.TryGetValue(header, out var state) ||
            !state.IsDragging || state.Host is null || state.Transform is null ||
            args.LeftButton != MouseButtonState.Pressed)
            return;

        var point = args.GetPosition(state.Host);
        state.Transform.X += point.X - state.LastPoint.X;
        state.Transform.Y += point.Y - state.LastPoint.Y;
        state.LastPoint = point;
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
    {
        if (sender is FrameworkElement header)
            EndDrag(header);
    }

    private static void OnLostMouseCapture(object sender, MouseEventArgs args)
    {
        if (sender is FrameworkElement header)
            EndDrag(header);
    }

    private static void EndDrag(FrameworkElement header)
    {
        if (Drags.TryGetValue(header, out var state))
        {
            state.IsDragging = false;
            state.Host = null;
            state.Transform = null;
        }

        if (Mouse.Captured == header)
            header.ReleaseMouseCapture();
    }

    private static void ToggleMaximize(FrameworkElement host)
    {
        var state = Hosts.GetValue(host, static _ => new HostState());
        if (!state.IsMaximized)
        {
            state.Width = host.Width;
            state.Height = host.Height;
            state.Margin = host.Margin;
            state.HorizontalAlignment = host.HorizontalAlignment;
            state.VerticalAlignment = host.VerticalAlignment;
            state.RenderTransform = host.RenderTransform;

            host.Width = double.NaN;
            host.Height = double.NaN;
            host.HorizontalAlignment = HorizontalAlignment.Stretch;
            host.VerticalAlignment = VerticalAlignment.Stretch;
            host.Margin = new Thickness(20);
            host.RenderTransform = Transform.Identity;
            state.IsMaximized = true;
        }
        else
        {
            host.Width = state.Width;
            host.Height = state.Height;
            host.Margin = state.Margin;
            host.HorizontalAlignment = state.HorizontalAlignment;
            host.VerticalAlignment = state.VerticalAlignment;
            host.RenderTransform = state.RenderTransform ?? Transform.Identity;
            state.IsMaximized = false;
        }
    }

    private static FrameworkElement? FindDialogHost(DependencyObject child)
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.Name == "DialogHostContainer")
                return element;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
