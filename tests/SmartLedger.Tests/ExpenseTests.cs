using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class ExpenseTests
{
    [Fact]
    public async Task CreateExpenseAsync_StoresExpenseAndCashOutflowAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-Expense-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var userId = Guid.NewGuid();
            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Users(Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive,CreatedAt) VALUES($id,'phase4-test','Phase 4 Test','hash','','',1,1,$created);";
                command.Parameters.AddWithValue("$id", userId.ToString("N"));
                command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }

            var repository = new AppRepository(database);
            var expense = new Expense(Guid.NewGuid(), null, "كهرباء", 350m, "فاتورة الكهرباء", DateTimeOffset.UtcNow, userId)
            {
                CategoryId = "utilities",
                PaymentMethod = "Cash"
            };
            await repository.CreateExpenseAsync(expense);
            var saved = await repository.GetExpensesAsync();
            var entry = Assert.Single(saved);
            Assert.Equal(expense.Id, entry.Id);
            Assert.Equal("Cash", entry.PaymentMethod);

            await using var verify = database.Open();
            await verify.OpenAsync();
            await using var verifyCommand = verify.CreateCommand();
            verifyCommand.CommandText = "SELECT Amount,EntryType FROM ExpenseCashEntries WHERE ExpenseId=$id;";
            verifyCommand.Parameters.AddWithValue("$id", expense.Id.ToString("N"));
            await using var reader = await verifyCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("350", reader.GetString(0));
            Assert.Equal("EXPENSE_PAYMENT:Cash", reader.GetString(1));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
