using Microsoft.Data.Sqlite;
var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartLedger", "smartledger.db");
var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true, DefaultTimeout = 15 }.ToString();
await using var db = new SqliteConnection(cs);
await db.OpenAsync();
await using (var pragma = db.CreateCommand()) { pragma.CommandText = "PRAGMA busy_timeout=15000;"; await pragma.ExecuteNonQueryAsync(); }
Console.WriteLine("DB=" + dbPath);
Console.WriteLine("BEFORE:");
await using (var q = db.CreateCommand()) {
 q.CommandText = @"SELECT p.Name, p.Note AS PersonNote, o.Id AS OperationId, o.Note AS OperationNote, o.Amount, o.OccurredAt FROM People p JOIN Operations o ON o.PersonId=p.Id WHERE p.Name IN ($person1,$person2) ORDER BY p.Name,o.OccurredAt;";
 q.Parameters.AddWithValue("$person1","المنتصر بالله منصور"); q.Parameters.AddWithValue("$person2","يحيى ثابت");
 await using var rd=await q.ExecuteReaderAsync();
 while(await rd.ReadAsync()) Console.WriteLine($"{rd.GetString(0)} | PersonNote={rd.GetString(1)} | OpId={rd.GetString(2)} | OpNote={rd.GetString(3)} | Amount={rd.GetString(4)} | Date={rd.GetString(5)}");
}
await using var tx = await db.BeginTransactionAsync();
await using var update = db.CreateCommand(); update.Transaction=(SqliteTransaction)tx;
update.CommandText = @"UPDATE Operations SET Note=(SELECT trim(People.Note) FROM People WHERE People.Id=Operations.PersonId) WHERE trim(Note)=$opening AND PersonId IN (SELECT Id FROM People WHERE Name IN ($person1,$person2) AND Note IS NOT NULL AND trim(Note)<>'');";
update.Parameters.AddWithValue("$opening","رصيد افتتاحي"); update.Parameters.AddWithValue("$person1","المنتصر بالله منصور"); update.Parameters.AddWithValue("$person2","يحيى ثابت");
var affected=await update.ExecuteNonQueryAsync();
await tx.CommitAsync();
Console.WriteLine("ROWS_AFFECTED="+affected);
Console.WriteLine("VERIFY_SQL=SELECT p.Name,p.Note AS PersonNote,o.Id,o.Note AS OperationNote,o.Amount,o.OccurredAt FROM People p JOIN Operations o ON o.PersonId=p.Id WHERE p.Name IN ('المنتصر بالله منصور','يحيى ثابت') ORDER BY p.Name,o.OccurredAt;");
await using (var q = db.CreateCommand()) {
 q.CommandText = @"SELECT p.Name, p.Note AS PersonNote, o.Id AS OperationId, o.Note AS OperationNote, o.Amount, o.OccurredAt FROM People p JOIN Operations o ON o.PersonId=p.Id WHERE p.Name IN ($person1,$person2) ORDER BY p.Name,o.OccurredAt;";
 q.Parameters.AddWithValue("$person1","المنتصر بالله منصور"); q.Parameters.AddWithValue("$person2","يحيى ثابت");
 await using var rd=await q.ExecuteReaderAsync();
 Console.WriteLine("AFTER:");
 while(await rd.ReadAsync()) Console.WriteLine($"{rd.GetString(0)} | PersonNote={rd.GetString(1)} | OpId={rd.GetString(2)} | OpNote={rd.GetString(3)} | Amount={rd.GetString(4)} | Date={rd.GetString(5)}");
}
