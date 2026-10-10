using SmartLedger.Core.Models;
using Xunit;

namespace SmartLedger.Tests;

public sealed class MessageTemplateEngineTests
{
    [Fact]
    public void Render_UsesFallbackWhenPersistedTemplateIsMissingOrBlank()
    {
        var values = new Dictionary<string, string?> { ["CustomerName"] = "أحمد", ["Currency"] = "ر.ي" };
        Assert.Equal("مرحباً أحمد — ر.ي", MessageTemplateEngine.Render(null, values, "مرحباً {CustomerName} — {Currency}"));
        Assert.Equal("مرحباً أحمد — ر.ي", MessageTemplateEngine.Render("  ", values, "مرحباً {CustomerName} — {Currency}"));
    }

    [Fact]
    public void Render_ReplacesKnownTokensCaseInsensitivelyAndKeepsUnknownTokens()
    {
        var result = MessageTemplateEngine.Render("{customername}: {TotalAmount} {Unknown}", new Dictionary<string, string?>
        {
            ["CustomerName"] = "Maha",
            ["TotalAmount"] = "125.00"
        });
        Assert.Equal("Maha: 125.00 {Unknown}", result);
    }

    [Fact]
    public void Render_ConvertsNullValuesToEmptyText()
    {
        Assert.Equal("Name: ", MessageTemplateEngine.Render("Name: {CustomerName}", new Dictionary<string, string?> { ["CustomerName"] = null }));
    }
}
