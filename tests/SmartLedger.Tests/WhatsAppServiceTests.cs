using SmartLedger.Infrastructure.Integrations;
using Xunit;

namespace SmartLedger.Tests;

public sealed class WhatsAppServiceTests
{
    [Theory]
    [InlineData("0777 123 456", "967777123456")]
    [InlineData("777-123-456", "967777123456")]
    [InlineData("+967 777 123 456", "967777123456")]
    [InlineData("00967-777-123-456", "967777123456")]
    [InlineData("447700900123", "967447700900123")]
    public void NormalizePhone_ProducesDigitsWithCountryCode(string input, string expected)
        => Assert.Equal(expected, WhatsAppService.NormalizePhone(input));

    [Fact]
    public void BuildUrl_EncodesArabicMessageAndUsesNormalizedPhone()
    {
        var service = new WhatsAppService();
        var url = service.BuildUrl("0777 123 456", "مرحباً {name}", new Dictionary<string, string> { ["{name}"] = "أحمد" });
        Assert.StartsWith("https://wa.me/967777123456?text=", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("مرحباً أحمد"), url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDispatchUris_ProvidesApplicationAndWebFallback()
    {
        var service = new WhatsAppService();
        var (app, web) = service.BuildDispatchUris("+967777123456", "كشف حساب");
        Assert.Equal("whatsapp", app.Scheme);
        Assert.Equal("https", web.Scheme);
        Assert.Contains("phone=967777123456", app.Query, StringComparison.Ordinal);
        Assert.Contains("web.whatsapp.com", web.Host, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("123", false)]
    [InlineData("0777 123 456", true)]
    public void NormalizePhone_RejectsMissingOrTooShortNumbers(string phone, bool valid)
    {
        if (valid) Assert.Equal("967777123456", WhatsAppService.NormalizePhone(phone));
        else Assert.ThrowsAny<ArgumentException>(() => WhatsAppService.NormalizePhone(phone));
    }
}
