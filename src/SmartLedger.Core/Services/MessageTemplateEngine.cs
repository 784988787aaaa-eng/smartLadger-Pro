using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartLedger.Core.Services;

/// <summary>Renders persisted business message templates using invariant-safe token substitution.</summary>
public static class MessageTemplateEngine
{
    private static readonly Regex TokenPattern = new(@"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Render(string? template, IReadOnlyDictionary<string, string?>? values, string fallback = "")
    {
        var source = string.IsNullOrWhiteSpace(template) ? fallback : template;
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        if (values is null || values.Count == 0) return TokenPattern.Replace(source, string.Empty);
        return TokenPattern.Replace(source, match =>
        {
            var key = match.Groups["name"].Value;
            return values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        });
    }

    public static string FormatAmount(decimal value, string? format = "N2") =>
        value.ToString(string.IsNullOrWhiteSpace(format) ? "N2" : format, CultureInfo.InvariantCulture);
}
