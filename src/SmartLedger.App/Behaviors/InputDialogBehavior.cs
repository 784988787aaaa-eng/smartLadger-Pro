using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLedger.App.Behaviors;

public static class InputDialogBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(InputDialogBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty EnterSavesOnRadioProperty =
        DependencyProperty.RegisterAttached(
            "EnterSavesOnRadio",
            typeof(bool),
            typeof(InputDialogBehavior),
            new PropertyMetadata(false));

    public static void SetEnterSavesOnRadio(DependencyObject element, bool value) =>
        element.SetValue(EnterSavesOnRadioProperty, value);

    public static bool GetEnterSavesOnRadio(DependencyObject element) =>
        (bool)element.GetValue(EnterSavesOnRadioProperty);

    public static readonly DependencyProperty SuggestionPopupProperty =
        DependencyProperty.RegisterAttached(
            "SuggestionPopup",
            typeof(Popup),
            typeof(InputDialogBehavior),
            new PropertyMetadata(null));

    public static void SetSuggestionPopup(DependencyObject element, Popup? value) =>
        element.SetValue(SuggestionPopupProperty, value);

    public static Popup? GetSuggestionPopup(DependencyObject element) =>
        element.GetValue(SuggestionPopupProperty) as Popup;
    public static readonly DependencyProperty SuggestionPopupNameProperty =
        DependencyProperty.RegisterAttached(
            "SuggestionPopupName",
            typeof(string),
            typeof(InputDialogBehavior),
            new PropertyMetadata(null));

    public static void SetSuggestionPopupName(DependencyObject element, string? value) =>
        element.SetValue(SuggestionPopupNameProperty, value);

    public static string? GetSuggestionPopupName(DependencyObject element) =>
        element.GetValue(SuggestionPopupNameProperty) as string;
    public static readonly DependencyProperty RegisterSuggestionPopupProperty =
        DependencyProperty.RegisterAttached(
            "RegisterSuggestionPopup",
            typeof(bool),
            typeof(InputDialogBehavior),
            new PropertyMetadata(false, OnRegisterSuggestionPopupChanged));

    public static void SetRegisterSuggestionPopup(DependencyObject element, bool value) =>
        element.SetValue(RegisterSuggestionPopupProperty, value);

    public static bool GetRegisterSuggestionPopup(DependencyObject element) =>
        (bool)element.GetValue(RegisterSuggestionPopupProperty);

    private static void OnRegisterSuggestionPopupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup) return;
        if ((bool)e.NewValue)
            popup.Opened += RegisterPopupTarget;
        else
            popup.Opened -= RegisterPopupTarget;
    }

    private static void RegisterPopupTarget(object? sender, EventArgs e)
    {
        if (sender is Popup popup && popup.PlacementTarget is DependencyObject target)
            SetSuggestionPopup(target, popup);
    }

    public static readonly DependencyProperty SuggestionKeysEnabledProperty =
        DependencyProperty.RegisterAttached(
            "SuggestionKeysEnabled",
            typeof(bool),
            typeof(InputDialogBehavior),
            new PropertyMetadata(false, OnSuggestionKeysEnabledChanged));

    public static void SetSuggestionKeysEnabled(DependencyObject element, bool value) =>
        element.SetValue(SuggestionKeysEnabledProperty, value);

    public static bool GetSuggestionKeysEnabled(DependencyObject element) =>
        (bool)element.GetValue(SuggestionKeysEnabledProperty);

    private static void OnSuggestionKeysEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        if ((bool)e.NewValue)
            box.PreviewKeyDown += OnSuggestionPreviewKeyDown;
        else
            box.PreviewKeyDown -= OnSuggestionPreviewKeyDown;
    }

    private static void OnSuggestionPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (!TryGetOpenSuggestionList(box, out var list) || list.Items.Count == 0) return;
        var popup = GetSuggestionPopup(box);
        if (popup is null && !string.IsNullOrWhiteSpace(GetSuggestionPopupName(box)))
            popup = LogicalTreeHelper.FindLogicalNode(FindRoot(box), GetSuggestionPopupName(box)!) as Popup;
        if (popup is null)
        {
            for (DependencyObject? current = LogicalTreeHelper.GetParent(box); current is not null; current = LogicalTreeHelper.GetParent(current))
            {
                popup = LogicalTreeHelper.GetChildren(current)
                    .OfType<Popup>()
                    .FirstOrDefault(x => x.IsOpen && x.PlacementTarget == box);
                if (popup is not null) break;
            }
        }
        if (popup is null) return;

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            var next = list.SelectedIndex < 0
                ? (e.Key == Key.Down ? 0 : list.Items.Count - 1)
                : Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, list.Items.Count - 1);
            list.SelectedIndex = next;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (list.SelectedIndex < 0) list.SelectedIndex = 0;
            list.GetBindingExpression(ListBox.SelectedValueProperty)?.UpdateSource();
            popup.IsOpen = false;
            popup.GetBindingExpression(Popup.IsOpenProperty)?.UpdateSource();
            var root = FindRoot(box);
            var inputs = FindInputs(root).ToList();
            MoveToNextInput(inputs, inputs.IndexOf(box));
            e.Handled = true;
        }
    }

    private static FrameworkElement FindRoot(FrameworkElement element)
    {
        FrameworkElement current = element;
        while (current.Parent is FrameworkElement parent)
            current = parent;
        return current;
    }

    public static readonly DependencyProperty SpaceOpensComboBoxProperty =
        DependencyProperty.RegisterAttached("SpaceOpensComboBox", typeof(bool), typeof(InputDialogBehavior), new PropertyMetadata(false, OnSpaceOpensComboBoxChanged));

    public static void SetSpaceOpensComboBox(DependencyObject element, bool value) => element.SetValue(SpaceOpensComboBoxProperty, value);
    public static bool GetSpaceOpensComboBox(DependencyObject element) => (bool)element.GetValue(SpaceOpensComboBoxProperty);

    private static void OnSpaceOpensComboBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox combo) return;
        if ((bool)e.NewValue) combo.PreviewKeyDown += OnComboBoxPreviewKeyDown;
        else combo.PreviewKeyDown -= OnComboBoxPreviewKeyDown;
    }

    private static void OnComboBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox combo || !combo.IsEnabled) return;
        if (e.Key == Key.Space)
        {
            combo.IsDropDownOpen = !combo.IsDropDownOpen;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && combo.IsDropDownOpen)
        {
            combo.IsDropDownOpen = false;
            e.Handled = true;
        }
    }
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        if ((bool)e.NewValue)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
            element.Loaded += OnLoaded;
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
        else
        {
            element.PreviewKeyDown -= OnPreviewKeyDown;
            element.Loaded -= OnLoaded;
            element.IsVisibleChanged -= OnIsVisibleChanged;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement root) return;
        root.Dispatcher.BeginInvoke(() =>
        {
            var first = FindInputs(root).FirstOrDefault();
            first?.Focus();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            OnLoaded(sender, new RoutedEventArgs());
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement root) return;

        if (e.Key == Key.Escape)
        {
            if (e.OriginalSource is Control escapeSource && GetSuggestionPopup(escapeSource) is Popup popup && popup.IsOpen)
            {
                popup.IsOpen = false;
                popup.GetBindingExpression(Popup.IsOpenProperty)?.UpdateSource();
                e.Handled = true;
                return;
            }

            if (root.DataContext is null) return;
            if (TryExecuteCommand(root.DataContext, "CloseDialogCommand") || TryExecuteCommand(root.DataContext, "CloseBookDialogCommand") || TryExecuteCommand(root.DataContext, "CancelCommand"))
                e.Handled = true;
            return;
        }

        if ((e.Key != Key.Enter && e.Key != Key.Down && e.Key != Key.Up) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;

        if (e.OriginalSource is not DependencyObject source) return;

        if (source is RadioButton radio && GetEnterSavesOnRadio(root) && e.Key == Key.Enter)
        {
            if (radio.IsChecked != true) radio.IsChecked = true;
            if (root.DataContext is not null && TryExecuteCommand(root.DataContext, "SaveNewCommand"))
                e.Handled = true;
            return;
        }

        if (source is Button || (source is RadioButton && !GetEnterSavesOnRadio(root))) return;
        if ((e.Key == Key.Down || e.Key == Key.Up) && source is TextBox) return;

        var inputs = FindInputs(root, GetEnterSavesOnRadio(root)).ToList();
        var current = source as Control;
        var index = current is null ? -1 : inputs.IndexOf(current);

        if (current is TextBox && TryGetOpenSuggestionList(current, out var suggestionList))
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (suggestionList.Items.Count > 0)
                {
                    if (suggestionList.SelectedIndex < 0)
                        suggestionList.SelectedIndex = e.Key == Key.Down ? 0 : suggestionList.Items.Count - 1;
                    else
                        suggestionList.SelectedIndex = Math.Clamp(
                            suggestionList.SelectedIndex + (e.Key == Key.Down ? 1 : -1),
                            0,
                            suggestionList.Items.Count - 1);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter && suggestionList.Items.Count > 0)
            {
                if (suggestionList.SelectedIndex < 0)
                    suggestionList.SelectedIndex = 0;
                current.Focus();
                MoveToNextInput(inputs, index);
                e.Handled = true;
                return;
            }
        }

        if (index >= 0 && index < inputs.Count - 1)
        {
            MoveToNextInput(inputs, index);
            e.Handled = true;
            return;
        }

        if (index == inputs.Count - 1 && root.DataContext is not null)
        {
            if (TryExecuteCommand(root.DataContext, "SaveNewCommand") || TryExecuteCommand(root.DataContext, "SaveCommand") || TryExecuteCommand(root.DataContext, "SaveBookCommand"))
                e.Handled = true;
        }
    }

    private static void MoveToNextInput(List<Control> inputs, int index)
    {
        if (index >= 0 && index < inputs.Count - 1)
            inputs[index + 1].Focus();
    }

    private static bool TryGetOpenSuggestionList(Control source, out ListBox list)
    {
        list = null!;
        var popup = GetSuggestionPopup(source);

        if (popup is null)
        {
            var popupName = GetSuggestionPopupName(source);
            if (!string.IsNullOrWhiteSpace(popupName))
            {
                var root = FindRoot(source);
                popup = LogicalTreeHelper.FindLogicalNode(root, popupName) as Popup;
            }
        }

        if (popup is null || !popup.IsOpen)
        {
            for (DependencyObject? current = LogicalTreeHelper.GetParent(source); current is not null; current = LogicalTreeHelper.GetParent(current))
            {
                var candidate = LogicalTreeHelper.GetChildren(current)
                    .OfType<Popup>()
                    .FirstOrDefault(x => x.IsOpen && x.PlacementTarget == source);
                if (candidate is not null)
                {
                    popup = candidate;
                    break;
                }
            }
        }

        if (popup is null || !popup.IsOpen) return false;

        list = popup.Child is ListBox direct
            ? direct
            : EnumerateDescendants(popup.Child).OfType<ListBox>().FirstOrDefault()!;
        return list is not null;
    }

    private static IEnumerable<Control> FindInputs(DependencyObject root, bool includeRadioButtons = false) =>
        EnumerateDescendants(root)
            .OfType<Control>()
            .Where(c => c.IsVisible && c.IsEnabled && c.Focusable)
            .Where(c => c is TextBox || c is ComboBox || (includeRadioButtons && c is RadioButton))
            .OrderBy(c => KeyboardNavigation.GetTabIndex(c))
            .ThenBy(c => c.PointToScreen(new Point(0, 0)).Y)
            // Equal-tab-index controls follow the Arabic reading direction: right to left.
            .ThenByDescending(c => c.PointToScreen(new Point(0, 0)).X);

    private static IEnumerable<DependencyObject> EnumerateDescendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
                yield return descendant;
        }
    }

    private static bool TryExecuteCommand(object dataContext, string propertyName)
    {
        var property = dataContext.GetType().GetProperty(propertyName);
        if (property?.GetValue(dataContext) is not ICommand command || !command.CanExecute(null))
            return false;
        command.Execute(null);
        return true;
    }
}
