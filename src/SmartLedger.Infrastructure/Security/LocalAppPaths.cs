namespace SmartLedger.Infrastructure.Security;
public static class LocalAppPaths{public static string Root=>Environment.GetEnvironmentVariable("SMARTLEDGER_DATA_DIR") is { Length: >0 } configuredRoot?Path.GetFullPath(configuredRoot):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SmartLedger");}
