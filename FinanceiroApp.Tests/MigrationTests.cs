using System.Data;
using System.Text.RegularExpressions;
using FinanceiroApp.Data;
using FinanceiroApp.Models;
using FinanceiroApp.Services;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Tests;

public sealed class MigrationTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void LegacyRealDatabaseMigratesOncePreservingIdsLinksAndBalances()
    {
        _db.Service.SaveIncome(new IncomeInput { Description = "Centavos", Amount = 100.29m, CategoryId = _db.IncomeCategoryId, AccountId = _db.AccountId });
        var legacy = BuildLegacy();
        var service = new FinanceService(legacy);
        service.Initialize();
        Assert.Equal(1100.29m, service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(_db.AccountId, service.GetAccounts(false).Single().Id);
        Assert.True(File.Exists(legacy + ".before-migration-v1"));
        service.Initialize();
        Assert.Equal(1100.29m, service.GetDashboardSummary().CurrentBalance);
        using var connection = new SqliteConnection($"Data Source={legacy};Pooling=False"); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT Amount FROM Incomes;";
        Assert.Equal(10029L, command.ExecuteScalar());
        command.CommandText = "PRAGMA foreign_key_check;"; using var reader = command.ExecuteReader(); Assert.False(reader.Read());
    }

    [Fact]
    public void LegacyBackupIsMigratedBeforeReplacingLiveDatabase()
    {
        var legacy = BuildLegacy();
        _db.Service.ClearFinancialData();
        _db.Service.RestoreDatabase(legacy);
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void FutureSchemaCannotBeOpenedOrRestored()
    {
        var file = Path.Combine(_db.DirectoryPath, "future.db");
        _db.Service.BackupDatabase(file);
        using (var connection = new SqliteConnection($"Data Source={file};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=999;"; command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => new FinanceService(file).Initialize());
        Assert.Throws<InvalidOperationException>(() => _db.Service.RestoreDatabase(file));
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    private string BuildLegacy()
    {
        var file = Path.Combine(_db.DirectoryPath, "legacy.db");
        using var source = new SqliteConnection($"Data Source={_db.Service.DatabasePath};Pooling=False"); source.Open();
        using var target = new SqliteConnection($"Data Source={file};Pooling=False;Foreign Keys=False"); target.Open();
        foreach (var table in MoneyStorage.Columns.Keys.Concat(new[] { "Categories", "Settings" }))
        {
            using var schema = source.CreateCommand(); schema.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=$table;"; schema.Parameters.AddWithValue("$table", table);
            var sql = (string)schema.ExecuteScalar()!;
            if (MoneyStorage.Columns.TryGetValue(table, out var money))
                foreach (var column in money) sql = Regex.Replace(sql, $@"\b{column}\s+INTEGER\b", $"{column} REAL");
            using var create = target.CreateCommand(); create.CommandText = sql; create.ExecuteNonQuery();
            using var select = source.CreateCommand(); select.CommandText = $"SELECT * FROM [{table}];";
            using var reader = select.ExecuteReader();
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            while (reader.Read())
            {
                using var insert = target.CreateCommand();
                insert.CommandText = $"INSERT INTO [{table}] ({string.Join(',', names.Select(n => $"[{n}]"))}) VALUES ({string.Join(',', names.Select((_, i) => "$p" + i))});";
                for (var i = 0; i < names.Length; i++)
                {
                    object value = reader.GetValue(i);
                    if (value is not DBNull && money?.Contains(names[i]) == true) value = Convert.ToDouble(value) / 100d;
                    insert.Parameters.AddWithValue("$p" + i, value);
                }
                insert.ExecuteNonQuery();
            }
        }
        return file;
    }

    public void Dispose() => _db.Dispose();
}
