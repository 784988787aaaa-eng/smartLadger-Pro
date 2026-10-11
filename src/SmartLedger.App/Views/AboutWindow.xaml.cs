using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using SmartLedger.Infrastructure.Security;

namespace SmartLedger.App.Views;

public partial class AboutWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string _databasePath = Path.Combine(LocalAppPaths.Root, "smartledger.db");

    public AboutWindow()
    {
        InitializeComponent();
        RuntimeText.Text = Environment.Version.ToString() + " (Windows x64)";
        RefreshDiagnostics();
        _timer.Tick += (_, _) => RefreshDiagnostics();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void RefreshDiagnostics()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            MemoryText.Text = (process.WorkingSet64 / 1024d / 1024d).ToString("N1", CultureInfo.InvariantCulture) + " MB";
            DatabaseSizeText.Text = File.Exists(_databasePath)
                ? (new FileInfo(_databasePath).Length / 1024d / 1024d).ToString("N2", CultureInfo.InvariantCulture) + " MB"
                : "غير متاحة";
            if (!File.Exists(_databasePath))
            {
                RecordsCountText.Text = "غير متاحة";
                return;
            }

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var tables = connection.CreateCommand();
            tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            using var reader = tables.ExecuteReader();
            var names = new System.Collections.Generic.List<string>();
            while (reader.Read()) names.Add(reader.GetString(0));
            reader.Close();

            long total = 0;
            foreach (var table in names)
            {
                using var count = connection.CreateCommand();
                count.CommandText = $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"")}\";";
                total += Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            RecordsCountText.Text = total.ToString("N0", CultureInfo.InvariantCulture);
        }
        catch
        {
            DatabaseSizeText.Text = File.Exists(_databasePath) ? "متاح" : "غير متاحة";
            RecordsCountText.Text = "تعذر القراءة";
            MemoryText.Text = (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d).ToString("N1", CultureInfo.InvariantCulture) + " MB";
        }
    }

    private void CopySystemInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = $"SmartLedger Pro — Enterprise ERP Suite{Environment.NewLine}Commercial Enterprise Edition (Release 2026){Environment.NewLine}.NET {Environment.Version} Windows Desktop{Environment.NewLine}SQLite High-Performance Engine{Environment.NewLine}QuestPDF Vector Reporting{Environment.NewLine}Database: {DatabaseSizeText.Text}{Environment.NewLine}Records: {RecordsCountText.Text}{Environment.NewLine}Memory: {MemoryText.Text}{Environment.NewLine}Support: 774004399";
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر نسخ معلومات النظام: {ex.Message}", "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}