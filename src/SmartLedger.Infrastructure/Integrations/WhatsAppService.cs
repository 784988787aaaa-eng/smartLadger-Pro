using System.Diagnostics;
using System.Text;

namespace SmartLedger.Infrastructure.Integrations;

/// <summary>Builds and dispatches WhatsApp links without silently sending messages.</summary>
public sealed class WhatsAppService
{
    public static string NormalizePhone(string phone, string defaultCountryCode = "967")
    {
        ArgumentNullException.ThrowIfNull(phone);
        var trimmed = phone.Trim();
        var hadInternationalPrefix = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length == 0) throw new ArgumentException("رقم الهاتف مطلوب لإرسال رسالة واتساب.", nameof(phone));
        if (!hadInternationalPrefix && digits.StartsWith('0')) digits = digits.TrimStart('0');
        if (!hadInternationalPrefix && !digits.StartsWith(defaultCountryCode, StringComparison.Ordinal))
            digits = defaultCountryCode + digits;
        if (digits.Length is < 7 or > 15)
            throw new ArgumentException("رقم الهاتف غير صالح وفق صيغة E.164.", nameof(phone));
        return digits;
    }

    public string BuildUrl(string phone, string template, IReadOnlyDictionary<string,string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var message = template;
        foreach (var pair in values) message = message.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return $"https://wa.me/{NormalizePhone(phone)}?text={Uri.EscapeDataString(message)}";
    }

    public (Uri AppUri, Uri WebUri) BuildDispatchUris(string phone, string message)
    {
        var normalized = NormalizePhone(phone);
        var encoded = Uri.EscapeDataString(message ?? string.Empty);
        return (new Uri($"whatsapp://send?phone={normalized}&text={encoded}"),
            new Uri($"https://web.whatsapp.com/send?phone={normalized}&text={encoded}"));
    }

    public bool OpenAfterCommit(bool committed, string phone, string template, IReadOnlyDictionary<string,string> values)
    {
        if (!committed) return false;
        var url = BuildUrl(phone, template, values);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return true;
    }

    public bool OpenMessage(string phone, string message)
    {
        var (appUri, webUri) = BuildDispatchUris(phone, message);
        try
        {
            Process.Start(new ProcessStartInfo(appUri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Process.Start(new ProcessStartInfo(webUri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
    }

    public static string BuildStatementSummary(string accountName, string storeName, decimal openingBalance,
        decimal totalMovements, decimal currentBalance, string currency, DateTimeOffset date)
    {
        var status = currentBalance < 0 ? "له (دائن)" : currentBalance > 0 ? "عليه (مدين)" : "رصيد خالص";
        return $"مرحباً {accountName}،\nكشف حسابكم لدى {storeName}:\nالرصيد السابق: {openingBalance:N2} {currency}\nإجمالي الحركات: {totalMovements:N2} {currency}\nالرصيد المستحق الحالي: {Math.Abs(currentBalance):N2} {currency} ({status})\nالتاريخ: {date:yyyy/MM/dd HH:mm}\nشكراً لتعاملكم معنا.";
    }

    public bool OpenWhatsApp(string phone, string message) => OpenMessage(phone, message);

    public static string BuildVoucherMessage(string accountName, string docNo, decimal amount, string currency,
        string movementType, string description, decimal newBalance, DateTimeOffset date)
        => BuildVoucher(accountName, docNo, amount, currency, movementType, description, newBalance, date);

    public static string BuildVoucher(string accountName, string docNo, decimal amount, string currency,
        string movementType, string description, decimal newBalance, DateTimeOffset date)
        => $"مرحباً {accountName}،\nتم تسجيل سند رقم: #{docNo}\nالمبلغ: {amount:N2} {currency} ({movementType})\nالبيان: {description}\nالرصيد المتبقي: {newBalance:N2} {currency}\nالتاريخ: {date:yyyy/MM/dd HH:mm}";
}
