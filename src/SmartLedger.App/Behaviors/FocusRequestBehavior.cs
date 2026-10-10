using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartLedger.App.Behaviors;

public static class FocusRequestBehavior
{
    public static readonly DependencyProperty RequestFocusProperty =
        DependencyProperty.RegisterAttached(
            "RequestFocus",
            typeof(bool),
            typeof(FocusRequestBehavior),
            new PropertyMetadata(false, OnRequestFocusChanged));

    public static bool GetRequestFocus(DependencyObject element) =>
        (bool)element.GetValue(RequestFocusProperty);

    public static void SetRequestFocus(DependencyObject element, bool value) =>
        element.SetValue(RequestFocusProperty, value);

    private static void OnRequestFocusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not TextBox textBox || args.NewValue is not true)
            return;

        if (textBox.IsLoaded)
            FocusTextBox(textBox);
        else
            textBox.Loaded += OnTextBoxLoaded;
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not TextBox textBox)
            return;

        textBox.Loaded -= OnTextBoxLoaded;
        FocusTextBox(textBox);
    }

    private static void FocusTextBox(TextBox textBox)
    {
        textBox.Focus();
        Keyboard.Focus(textBox);
        textBox.SelectAll();
        textBox.SetCurrentValue(RequestFocusProperty, false);
    }
}
