using QuestPDF.Drawing;

namespace SmartLedger.Infrastructure.Reporting;

/// <summary>Centralized registration and selection for system PDF fonts used by QuestPDF reports.</summary>
internal static class PdfFontConfig
{
    private static int _registered;
    public const string ArabicFontFamily = "Tahoma";

    public static void RegisterFonts()
    {
        QuestPDF.Settings.UseSystemFonts = true;
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        var fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var fontName in new[] { "tahoma.ttf", "arial.ttf" })
        {
            var fontPath = Path.Combine(fontsDirectory, fontName);
            if (!File.Exists(fontPath))
                continue;

            using var fontStream = File.OpenRead(fontPath);
            FontManager.RegisterFontFromStream(fontStream);
        }
    }
}
