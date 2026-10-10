using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Data.Sqlite;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Core.Models;

namespace SmartLedger.Infrastructure.Audit;
public sealed class AuditRepository(Database database)
{
    private static readonly string[] Sensitive={"password","passwordhash","passwordsalt","recovery","secret","token","privatekey","credential"};
    public async Task LogActionAsync(Guid? userId,string action,string entityType,Guid? entityId,object? details,CancellationToken ct=default){var json=Sanitize(details);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="INSERT INTO AuditLogs VALUES($id,$user,$action,$entity,$entityId,$details,$when);";c.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("N"));c.Parameters.AddWithValue("$user",userId?.ToString("N")??(object)DBNull.Value);c.Parameters.AddWithValue("$action",action);c.Parameters.AddWithValue("$entity",entityType);c.Parameters.AddWithValue("$entityId",entityId?.ToString("N")??(object)DBNull.Value);c.Parameters.AddWithValue("$details",json);c.Parameters.AddWithValue("$when",DateTimeOffset.UtcNow.ToString("O"));await c.ExecuteNonQueryAsync(ct);}
    public async Task<IReadOnlyList<AuditEntry>> SearchAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, Guid? userId = null, string? action = null, string? entityType = null, string? text = null, CancellationToken ct = default)
    {
        var items = new List<AuditEntry>();
        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "SELECT a.Id,a.UserId,COALESCE(u.DisplayName,u.UserName,'—'),a.Action,a.EntityType,a.EntityId,a.Details,a.OccurredAt FROM AuditLogs a LEFT JOIN Users u ON u.Id=a.UserId WHERE($from IS NULL OR a.OccurredAt >= $from) AND($to IS NULL OR a.OccurredAt <= $to) AND($user IS NULL OR a.UserId=$user) AND($action='' OR a.Action LIKE '%'||$action||'%') AND($entity='' OR a.EntityType LIKE '%'||$entity||'%') AND($text='' OR a.Details LIKE '%'||$text||'%' OR a.Action LIKE '%'||$text||'%') ORDER BY a.OccurredAt DESC LIMIT 200;";
        c.Parameters.AddWithValue("$from", from?.ToString("O") ?? (object)DBNull.Value);
        c.Parameters.AddWithValue("$to", to?.ToString("O") ?? (object)DBNull.Value);
        c.Parameters.AddWithValue("$user", userId?.ToString("N") ?? (object)DBNull.Value);
        c.Parameters.AddWithValue("$action", (action ?? "").Trim());
        c.Parameters.AddWithValue("$entity", (entityType ?? "").Trim());
        c.Parameters.AddWithValue("$text", (text ?? "").Trim());
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(new AuditEntry(Guid.Parse(r.GetString(0)), r.IsDBNull(1) ? null : Guid.Parse(r.GetString(1)), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : Guid.Parse(r.GetString(5)), r.GetString(6), DateTimeOffset.Parse(r.GetString(7))));
        return items;
    }

    private static string Sanitize(object? value){if(value is null)return "{}";using var doc=JsonDocument.Parse(JsonSerializer.Serialize(value));using var stream=new MemoryStream();using(var w=new Utf8JsonWriter(stream,new JsonWriterOptions{Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping})){Write(w,doc.RootElement);w.Flush();}return System.Text.Encoding.UTF8.GetString(stream.ToArray());}
    private static void Write(Utf8JsonWriter w,JsonElement e){if(e.ValueKind==JsonValueKind.Object){w.WriteStartObject();foreach(var p in e.EnumerateObject()){if(Sensitive.Any(s=>p.Name.Contains(s,StringComparison.OrdinalIgnoreCase)))continue;w.WritePropertyName(p.Name);Write(w,p.Value);}w.WriteEndObject();}else if(e.ValueKind==JsonValueKind.Array){w.WriteStartArray();foreach(var x in e.EnumerateArray())Write(w,x);w.WriteEndArray();}else e.WriteTo(w);}
}
