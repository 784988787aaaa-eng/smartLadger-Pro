using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SmartLedger.App.Converters;

namespace SmartLedger.App.Behaviors;

public static class DataGridSerialBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DataGridSerialBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue)
        {
            grid.Loaded += OnLoaded;
            if (grid.IsLoaded) AddSerialColumn(grid);
        }
        else
        {
            grid.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid) AddSerialColumn(grid);
    }

    private static void AddSerialColumn(DataGrid grid)
    {
        if (grid.Columns.Any(c => (c.Header is string s && s == "م") || (c.Header is TextBlock tb && tb.Text == "م"))) return;

        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.FontFamilyProperty, SystemFonts.MessageFontFamily);
        factory.SetValue(TextBlock.FontSizeProperty, 11.0);
        factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        factory.SetBinding(TextBlock.TextProperty, new Binding("AlternationIndex")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1),
            Converter = new SerialNumberConverter()
        });

        var template = new DataTemplate { VisualTree = factory };
        var column = new DataGridTemplateColumn
        {
            Header = new TextBlock { Text = "م", HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center },
            Width = new DataGridLength(48),
            CellTemplate = template
        };
        grid.Columns.Insert(0, column);
        column.DisplayIndex = 0;
    }

}
