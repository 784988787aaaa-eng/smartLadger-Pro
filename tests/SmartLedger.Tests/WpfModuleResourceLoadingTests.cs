using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SmartLedger.App.ViewModels;
using Xunit;

namespace SmartLedger.Tests;

public sealed class WpfModuleResourceLoadingTests
{
    [Fact]
    public void Inventory_sales_and_expenses_templates_load_on_sta_without_xaml_resource_errors()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = Application.Current as global::SmartLedger.App.App;
                if (app is null)
                {
                    app = new global::SmartLedger.App.App();
                    app.InitializeComponent();
                }

                foreach (var viewModelType in new[] { typeof(InventoryViewModel), typeof(SalesViewModel), typeof(ExpensesViewModel) })
                {
                    var template = app.FindResource(new DataTemplateKey(viewModelType)) as DataTemplate;
                    Assert.NotNull(template);
                    Assert.IsAssignableFrom<FrameworkElement>(template!.LoadContent());
                }

                Assert.NotNull(app.FindResource("ProductDialogControlTemplate"));
                Assert.NotNull(app.FindResource("ExpenseVoucherDialogTemplate"));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF resource-loading test timed out.");
        if (failure is not null)
            throw new Xunit.Sdk.XunitException($"WPF module template load failed: {failure}");
    }
}
