using SmartLedger.Infrastructure.Data;
namespace SmartLedger.Infrastructure;
public static class Infrastructure
{
    public static async Task<Database>CreateDatabaseAsync(string root,CancellationToken ct=default){var db=new Database(root);await db.InitializeAsync(ct);return db;}
}
