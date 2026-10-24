using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Tests;

public sealed class BackupTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void BackupAndRestorePreserveDataAndCreateRecoverySnapshot()
    {
        var backup = Path.Combine(_db.DirectoryPath, "backup.db");
        _db.Service.BackupDatabase(backup);
        _db.Service.ClearFinancialData();
        _db.Service.RestoreDatabase(backup);
        Assert.Single(_db.Service.GetAccounts(false));
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
        Assert.True(File.Exists(_db.Service.DatabasePath + ".before-restore"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidBackupNeverReplacesExistingData(bool sqliteWithoutAppSchema)
    {
        var invalid = Path.Combine(_db.DirectoryPath, "invalid.db");
        if (sqliteWithoutAppSchema)
        {
            using var connection = new SqliteConnection($"Data Source={invalid};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Unrelated(Id INTEGER);";
            command.ExecuteNonQuery();
        }
        else
        {
            File.WriteAllText(invalid, "Nao e um banco de dados.");
        }

        var previous = File.ReadAllBytes(_db.Service.DatabasePath);
        Assert.Throws<InvalidOperationException>(() => _db.Service.RestoreDatabase(invalid));
        Assert.Equal(previous, File.ReadAllBytes(_db.Service.DatabasePath));
        Assert.Single(_db.Service.GetAccounts(false));
    }

    [Fact]
    public void BackupWithMissingColumnsIsRejected()
    {
        var invalid = Path.Combine(_db.DirectoryPath, "missing-column.db");
        _db.Service.BackupDatabase(invalid);
        using (var connection = new SqliteConnection($"Data Source={invalid};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE Accounts DROP COLUMN Notes;";
            command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => _db.Service.RestoreDatabase(invalid));
        Assert.Single(_db.Service.GetAccounts(false));
    }

    [Fact]
    public void SnapshotIncludesCommittedWalData()
    {
        var backup = Path.Combine(_db.DirectoryPath, "wal-backup.db");
        using (var connection = new SqliteConnection($"Data Source={_db.Service.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; INSERT INTO Accounts(Name, Type) VALUES('Conta WAL', 'Outros');";
            command.ExecuteNonQuery();
            _db.Service.BackupDatabase(backup);
        }
        _db.Service.ClearFinancialData();
        _db.Service.RestoreDatabase(backup);
        Assert.Equal(2, _db.Service.GetAccounts(false).Count);
    }

    [Fact]
    public void ActiveDatabaseCannotBeUsedAsBackupPath()
    {
        Assert.Throws<InvalidOperationException>(() => _db.Service.BackupDatabase(_db.Service.DatabasePath));
        Assert.Throws<InvalidOperationException>(() => _db.Service.RestoreDatabase(_db.Service.DatabasePath));
    }

    public void Dispose() => _db.Dispose();
}
