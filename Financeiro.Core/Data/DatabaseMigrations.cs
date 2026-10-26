using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Data;

internal static class DatabaseMigrations
{
    internal const int CurrentVersion = 1;

    internal static void Apply(AppDatabase database, SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(command.ExecuteScalar());
        if (version > CurrentVersion)
            throw new InvalidOperationException("Este banco foi criado por uma versao mais recente. Atualize o aplicativo antes de abri-lo.");
        if (version == CurrentVersion) return;

        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Accounts';";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;
        command.CommandText = "SELECT type FROM pragma_table_info('Accounts') WHERE name='InitialBalance';";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "REAL", StringComparison.OrdinalIgnoreCase)) return;

        var recovery = database.DatabasePath + ".before-migration-v1";
        if (!File.Exists(recovery))
        {
            using var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = recovery, Pooling = false }.ToString());
            snapshot.Open();
            connection.BackupDatabase(snapshot);
        }

        command.CommandText = "PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=ON;";
        command.ExecuteNonQuery();
        try
        {
            using var transaction = connection.BeginTransaction();
            var objects = new List<string>();
            using (var metadata = connection.CreateCommand())
            {
                metadata.Transaction = transaction;
                metadata.CommandText = $"SELECT sql FROM sqlite_master WHERE type IN ('index','trigger') AND sql IS NOT NULL AND tbl_name IN ({string.Join(',', MoneyStorage.Columns.Keys.Select(t => "'" + t + "'"))});";
                using var reader = metadata.ExecuteReader();
                while (reader.Read()) objects.Add(reader.GetString(0));
            }
            foreach (var (table, moneyColumns) in MoneyStorage.Columns)
            {
                var sql = Convert.ToString(Repository.Scalar(connection, transaction,
                    "SELECT sql FROM sqlite_master WHERE type='table' AND name=$name;", new Dictionary<string, object?> { ["$name"] = table }))!;
                var definition = sql[sql.IndexOf('(')..];
                foreach (var column in moneyColumns)
                    definition = Regex.Replace(definition, $@"\b{column}\s+REAL\b", $"{column} INTEGER", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var temporary = "__cents_" + table;
                Repository.Execute(connection, transaction, $"CREATE TABLE [{temporary}] {definition};");

                using (var select = connection.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = $"SELECT * FROM [{table}];";
                    using var reader = select.ExecuteReader();
                    var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = $"INSERT INTO [{temporary}] ({string.Join(',', names.Select(n => $"[{n}]"))}) VALUES ({string.Join(',', names.Select((_, i) => "$p" + i))});";
                    for (var i = 0; i < names.Length; i++) insert.Parameters.Add(new SqliteParameter("$p" + i, DBNull.Value));
                    while (reader.Read())
                    {
                        for (var i = 0; i < names.Length; i++)
                        {
                            var value = reader.GetValue(i);
                            if (value is not DBNull && moneyColumns.Contains(names[i], StringComparer.OrdinalIgnoreCase))
                                value = MoneyStorage.ToCents(decimal.Round(Convert.ToDecimal(value, CultureInfo.InvariantCulture), 2, MidpointRounding.AwayFromZero));
                            insert.Parameters[i].Value = value;
                        }
                        insert.ExecuteNonQuery();
                    }
                }
                Repository.Execute(connection, transaction, $"DROP TABLE [{table}]; ALTER TABLE [{temporary}] RENAME TO [{table}];");
            }
            foreach (var sql in objects) Repository.Execute(connection, transaction, sql);
            using (var check = connection.CreateCommand())
            {
                check.Transaction = transaction;
                check.CommandText = "PRAGMA foreign_key_check;";
                using var reader = check.ExecuteReader();
                if (reader.Read()) throw new InvalidOperationException("A migracao encontrou vinculos invalidos. O banco original foi preservado.");
            }
            Repository.Execute(connection, transaction, $"PRAGMA user_version={CurrentVersion};");
            transaction.Commit();
        }
        finally
        {
            command.CommandText = "PRAGMA legacy_alter_table=OFF; PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
        }
    }
}
