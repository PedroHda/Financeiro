using FinanceiroApp.Data;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Services;

internal sealed class DatabaseBackupService(AppDatabase database)
{
    public void Backup(string destinationPath)
    {
        var destination = ValidatePath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var source = database.CreateConnection())
            using (var target = Open(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                source.BackupDatabase(target);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Restore(string sourcePath)
    {
        var sourceFile = ValidatePath(sourcePath);
        if (!File.Exists(sourceFile))
        {
            throw new InvalidOperationException("Arquivo de backup nao encontrado.");
        }

        var staging = database.DatabasePath + $".{Guid.NewGuid():N}.restore";
        try
        {
            using (var source = Open(sourceFile, SqliteOpenMode.ReadOnly))
            using (var target = Open(staging, SqliteOpenMode.ReadWriteCreate))
            {
                AppDatabase.ValidateBackup(source);
                source.BackupDatabase(target);
                AppDatabase.ValidateBackup(target);
            }

            new AppDatabase(staging).Initialize();

            // Keep a recoverable snapshot of the current data before replacement.
            var recovery = database.DatabasePath + ".before-restore";
            Backup(recovery);
            try
            {
                using var source = Open(staging, SqliteOpenMode.ReadOnly);
                using var target = database.CreateConnection();
                source.BackupDatabase(target);
            }
            catch
            {
                using var previous = Open(recovery, SqliteOpenMode.ReadOnly);
                using var target = database.CreateConnection();
                previous.BackupDatabase(target);
                throw;
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("Nao foi possivel restaurar. Selecione um backup SQLite valido do Financeiro.", ex);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
            if (File.Exists(staging + ".before-migration-v1")) File.Delete(staging + ".before-migration-v1");
        }
    }

    private string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Informe o caminho do arquivo de backup.");
        }

        var absolute = Path.GetFullPath(path);
        if (string.Equals(absolute, database.DatabasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O arquivo de backup deve ser diferente do banco em uso.");
        }

        return absolute;
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
            ForeignKeys = true
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
