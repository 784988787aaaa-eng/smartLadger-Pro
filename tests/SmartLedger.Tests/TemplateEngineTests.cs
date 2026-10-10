using SmartLedger.Core.Models;
using Xunit;

namespace SmartLedger.Tests;

public sealed class TemplateEngineTests
{
    [Fact]
    public void Render_ReplacesAllKnownPlaceholders_AndLeavesUnknownOnesIntact()
    {
        const string template = "مرحباً {CustomerName} من {StoreName}، الفاتورة {InvoiceNumber}: {TotalAmount} {Currency} بتاريخ {Date}. {Unknown}";
        var values = new Dictionary<string, string?>
        {
            ["CustomerName"] = "أحمد",
            ["StoreName"] = "المتجر",
            ["InvoiceNumber"] = "INV-2026-0001",
            ["TotalAmount"] = "1500.00",
            ["Currency"] = "ر.ي",
            ["Date"] = "2026/10/10"
        };

        var rendered = MessageTemplateEngine.Render(template, values);

        Assert.Contains("مرحباً أحمد من المتجر", rendered);
        Assert.Contains("INV-2026-0001: 1500.00 ر.ي", rendered);
        Assert.Contains("2026/10/10", rendered);
        Assert.Contains("{Unknown}", rendered);
    }

    [Fact]
    public void Render_IsCaseInsensitiveAndReplacesNullWithEmptyText()
    {
        var rendered = MessageTemplateEngine.Render("{customername}/{Currency}", new Dictionary<string, string?>
        {
            ["CustomerName"] = null,
            ["currency"] = "YER"
        });

        Assert.Equal("/YER", rendered);
    }
}
