using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Financeiro.Sync.Contracts;
using FinanceiroApp.Data;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Services;

public sealed class SyncEngine
{
    private readonly AppDatabase _database;
    private readonly FinanceService _service;
    private sealed record Key(string Table, int LocalId, Guid Id, long Revision, string? Payload, bool Deleted);
    public sealed record Snapshot(long Cursor, IReadOnlyDictionary<Guid, SyncChange> Current, IReadOnlyList<SyncChange> Pending);

    public SyncEngine(FinanceService service) { _service = service; _database = new AppDatabase(service.DatabasePath); }

    public void BindWorkspace(string identity)
    {
        using var connection = _database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var previous = Convert.ToString(Repository.Scalar(connection, transaction, "SELECT Value FROM SyncState WHERE Key='Workspace';"));
        if (previous != identity)
        {
            Repository.Execute(connection, transaction, "UPDATE SyncKeys SET ServerRevision=0,LastPayload=NULL,Deleted=0; DELETE FROM SyncState;");
            Repository.Execute(connection, transaction, "INSERT INTO SyncState(Key,Value) VALUES('Workspace',$identity),('Cursor','0');", Parameters("$identity", identity));
        }
        transaction.Commit();
    }

    public Snapshot Capture()
    {
        using var connection = _database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var snapshot = Capture(connection, transaction);
        transaction.Commit();
        return snapshot;
    }

    private static Snapshot Capture(SqliteConnection connection, SqliteTransaction transaction)
    {
        var rows = SyncProtocol.Tables.ToDictionary(t => t, t => ReadRows(connection, transaction, t));
        var keys = ReadKeys(connection, transaction);
        var used = keys.Values.Select(k => k.Id).ToHashSet();
        foreach (var table in SyncProtocol.Tables)
        {
            foreach (var row in rows[table])
            {
                if (keys.ContainsKey((table, row.Key))) continue;
                var identity = StableIdentity(connection, transaction, table, row.Value, keys);
                if (used.Contains(identity)) identity = Guid.NewGuid();
                used.Add(identity);
                var key = new Key(table, row.Key, identity, 0, null, false);
                keys.Add((table, row.Key), key);
                Repository.Execute(connection, transaction, "INSERT INTO SyncKeys(TableName,LocalId,SyncId) VALUES($table,$id,$sync);", Parameters("$table", table, "$id", row.Key, "$sync", identity.ToString()));
            }
        }
        var current = new Dictionary<Guid, SyncChange>();
        var pending = new List<SyncChange>();
        foreach (var key in keys.Values)
        {
            if (!rows.TryGetValue(key.Table, out var tableRows)) continue;
            var deleted = !tableRows.TryGetValue(key.LocalId, out var row);
            var payload = deleted ? null : Serialize(connection, transaction, key.Table, key.LocalId, row!, keys);
            var change = new SyncChange(key.Id, key.Table, key.Revision, payload, deleted);
            current.Add(key.Id, change);
            if (payload != key.Payload || deleted != key.Deleted) pending.Add(change);
        }
        var cursor = Convert.ToInt64(Repository.Scalar(connection, transaction, "SELECT COALESCE((SELECT Value FROM SyncState WHERE Key='Cursor'),'0');"), CultureInfo.InvariantCulture);
        return new Snapshot(cursor, current, pending);
    }

    public void Merge(Snapshot snapshot, IReadOnlyList<SyncRecord> incoming, long cursor, IReadOnlyList<SyncResolution>? resolutions = null)
    {
        if (incoming.Count == 0 && cursor == snapshot.Cursor) return;
        var now = Capture();
        if (now.Current.Count != snapshot.Current.Count || now.Current.Any(pair => !snapshot.Current.TryGetValue(pair.Key, out var before) || before.Payload != pair.Value.Payload || before.Deleted != pair.Value.Deleted))
            throw new InvalidOperationException("Os dados locais mudaram durante a sincronizacao. Tente novamente.");
        var pending = snapshot.Pending.ToDictionary(c => c.Id);
        var selected = resolutions?.ToDictionary(r => r.Id) ?? [];
        var conflicts = new List<SyncConflict>();
        var preserve = new HashSet<Guid>();
        foreach (var record in incoming)
        {
            if (!pending.TryGetValue(record.Id, out var local) || local.BaseRevision == record.Revision || local.Payload == record.Payload && local.Deleted == record.Deleted) continue;
            if (!selected.TryGetValue(record.Id, out var resolution) || resolution.RemoteRevision != record.Revision)
                conflicts.Add(new SyncConflict(record.Id, record.Table, local.Payload, record));
            else if (resolution.KeepLocal) preserve.Add(record.Id);
        }
        if (conflicts.Count > 0) throw new SyncConflictException(conflicts);

        var recovery = _service.DatabasePath + ".before-sync";
        _service.BackupDatabase(recovery);
        using var connection = _database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var lockedSnapshot = Capture(connection, transaction);
        if (lockedSnapshot.Current.Count != snapshot.Current.Count || lockedSnapshot.Current.Any(pair => !snapshot.Current.TryGetValue(pair.Key, out var before) || before.Payload != pair.Value.Payload || before.Deleted != pair.Value.Deleted))
            throw new InvalidOperationException("Os dados locais mudaram durante a sincronizacao. Tente novamente.");
        Repository.Execute(connection, transaction, "PRAGMA defer_foreign_keys=ON;");
        var keys = ReadKeys(connection, transaction);
        var byGuid = keys.Values.ToDictionary(k => k.Id);
        foreach (var record in incoming)
        {
            if (!SyncProtocol.Tables.Contains(record.Table, StringComparer.Ordinal)) throw new InvalidOperationException("O servidor enviou um tipo de registro desconhecido.");
            if (byGuid.TryGetValue(record.Id, out var existing))
            {
                if (existing.Table != record.Table) throw new InvalidOperationException("Identidade de registro inconsistente.");
                continue;
            }
            var next = Math.Max(Convert.ToInt32(Repository.Scalar(connection, transaction, $"SELECT COALESCE(MAX(Id),0) FROM [{record.Table}];")), keys.Values.Where(k => k.Table == record.Table).Select(k => k.LocalId).DefaultIfEmpty().Max()) + 1;
            var key = new Key(record.Table, next, record.Id, 0, null, false);
            keys.Add((record.Table, next), key); byGuid.Add(record.Id, key);
            Repository.Execute(connection, transaction, "INSERT INTO SyncKeys(TableName,LocalId,SyncId) VALUES($table,$id,$sync);", Parameters("$table", record.Table, "$id", next, "$sync", record.Id.ToString()));
        }
        foreach (var table in SyncProtocol.Tables)
            foreach (var record in incoming.Where(r => r.Table == table && !r.Deleted && !preserve.Contains(r.Id)))
                Upsert(connection, transaction, byGuid[record.Id], record.Payload ?? throw new InvalidOperationException("Registro remoto sem dados."), byGuid);
        foreach (var table in SyncProtocol.Tables.Reverse())
            foreach (var record in incoming.Where(r => r.Table == table && r.Deleted && !preserve.Contains(r.Id)))
                Repository.Execute(connection, transaction, $"DELETE FROM [{table}] WHERE Id=$id;", Parameters("$id", byGuid[record.Id].LocalId));
        foreach (var record in incoming)
            Repository.Execute(connection, transaction, "UPDATE SyncKeys SET ServerRevision=$revision,LastPayload=$payload,Deleted=$deleted WHERE SyncId=$id;",
                Parameters("$revision", record.Revision, "$payload", record.Payload, "$deleted", record.Deleted, "$id", record.Id.ToString()));

        foreach (var goal in keys.Values.Where(k => k.Table == "Goals"))
        {
            var basePayload = incoming.LastOrDefault(r => r.Id == goal.Id)?.Payload ?? goal.Payload;
            if (preserve.Contains(goal.Id) || pending.TryGetValue(goal.Id, out var local) && !incoming.Any(r => r.Id == goal.Id)) basePayload = snapshot.Current[goal.Id].Payload;
            if (basePayload is null) continue;
            var baseAmount = JsonNode.Parse(basePayload)!["CurrentAmount"]!.GetValue<long>();
            Repository.Execute(connection, transaction, "UPDATE Goals SET CurrentAmount=$base+COALESCE((SELECT SUM(Amount) FROM GoalContributions WHERE GoalId=$id),0) WHERE Id=$id;", Parameters("$base", baseAmount, "$id", goal.LocalId));
        }
        ValidateMerged(connection, transaction);
        Repository.Execute(connection, transaction, "INSERT INTO SyncState(Key,Value) VALUES('Cursor',$cursor) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;", Parameters("$cursor", cursor.ToString(CultureInfo.InvariantCulture)));
        transaction.Commit();
        _service.RefreshDerivedData();
    }

    private static Dictionary<(string, int), Key> ReadKeys(SqliteConnection connection, SqliteTransaction transaction)
    {
        var result = new Dictionary<(string, int), Key>();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT TableName,LocalId,SyncId,ServerRevision,LastPayload,Deleted FROM SyncKeys;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = new Key(reader.GetString(0), reader.GetInt32(1), Guid.Parse(reader.GetString(2)), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5) != 0);
            result.Add((key.Table, key.LocalId), key);
        }
        return result;
    }

    private static Dictionary<int, SortedDictionary<string, object?>> ReadRows(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        var rows = new Dictionary<int, SortedDictionary<string, object?>>();
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"SELECT * FROM [{table}];";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++) values.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
            rows.Add(Convert.ToInt32(values["Id"]), values);
        }
        return rows;
    }

    private static Dictionary<string, string> References(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        var result = new Dictionary<string, string>();
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"PRAGMA foreign_key_list([{table}]);";
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(3), reader.GetString(2));
        return result;
    }

    private static string Serialize(SqliteConnection connection, SqliteTransaction transaction, string table, int id, SortedDictionary<string, object?> row, Dictionary<(string, int), Key> keys)
    {
        row = new SortedDictionary<string, object?>(row, StringComparer.Ordinal);
        row.Remove("Id"); row.Remove("CreatedAt");
        if (table == "Accounts") row.Remove("CurrentBalance");
        if (table == "CreditCards") row.Remove("AvailableLimit");
        if (table == "Expenses" && Convert.ToString(row["Status"]) == "Vencido") row["Status"] = "Pendente";
        if (table == "Goals")
        {
            var contributions = Convert.ToInt64(Repository.Scalar(connection, transaction, "SELECT COALESCE(SUM(Amount),0) FROM GoalContributions WHERE GoalId=$id;", Parameters("$id", id)));
            row["CurrentAmount"] = Convert.ToInt64(row["CurrentAmount"]) - contributions;
        }
        var references = References(connection, transaction, table);
        if (table == "Attachments") references["EntityId"] = Convert.ToString(row["EntityType"]) == "income" ? "Incomes" : "Expenses";
        if (table == "ImportedTransactions") row["EntryId"] = 0L;
        foreach (var (column, referenced) in references)
            if (row[column] is { } foreign)
                row[column] = keys.TryGetValue((referenced, Convert.ToInt32(foreign)), out var target) ? target.Id.ToString() : throw new InvalidOperationException("Vinculo local invalido para sincronizacao.");
        if (table == "Expenses")
        {
            var splits = ReadRows(connection, transaction, "ExpenseSplits").Values.Where(s => Convert.ToInt32(s["ExpenseId"]) == id)
                .GroupBy(s => keys[("Categories", Convert.ToInt32(s["CategoryId"]))].Id.ToString()).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new { CategoryId = g.Key, Amount = g.Sum(s => Convert.ToInt64(s["Amount"])) }).ToArray();
            row["_splits"] = splits;
        }
        return JsonSerializer.Serialize(row);
    }

    private static Guid StableIdentity(SqliteConnection connection, SqliteTransaction transaction, string table, SortedDictionary<string, object?> row, Dictionary<(string, int), Key> keys)
    {
        string? identity = table switch
        {
            "Categories" when Convert.ToInt32(row["Id"]) <= 12 => $"category:{row["Type"]}:{row["Name"]}",
            "Invoices" => $"invoice:{keys[("CreditCards", Convert.ToInt32(row["CreditCardId"]))].Id}:{row["InvoiceMonth"]}",
            "Budgets" => $"budget:{keys[("Categories", Convert.ToInt32(row["CategoryId"]))].Id}:{row["Month"]}",
            "CategoryRules" => $"rule:{row["Type"]}:{row["MatchText"]}",
            "ImportedTransactions" => $"import:{keys[("Accounts", Convert.ToInt32(row["AccountId"]))].Id}:{row["Fingerprint"]}",
            _ => null
        };
        if (table is "Incomes" or "Expenses")
        {
            var fingerprint = Convert.ToString(Repository.Scalar(connection, transaction,
                "SELECT Fingerprint FROM ImportedTransactions WHERE EntryKind=$kind AND EntryId=$id LIMIT 1;", Parameters("$kind", table == "Incomes" ? "income" : "expense", "$id", row["Id"])));
            if (!string.IsNullOrEmpty(fingerprint)) identity = $"entry:{keys[("Accounts", Convert.ToInt32(row["AccountId"]))].Id}:{fingerprint}";
        }
        return identity is null ? Guid.NewGuid() : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("Financeiro.Sync.v1:" + identity)).AsSpan(0, 16));
    }

    private static void Upsert(SqliteConnection connection, SqliteTransaction transaction, Key key, string payload, Dictionary<Guid, Key> keys)
    {
        var node = JsonNode.Parse(payload) as JsonObject ?? throw new InvalidOperationException("Dados remotos invalidos.");
        var values = new Dictionary<string, object?> { ["Id"] = key.LocalId };
        if (key.Table == "CreditCards") values["AvailableLimit"] = 0L;
        var references = References(connection, transaction, key.Table);
        if (key.Table == "Attachments") references["EntityId"] = node["EntityType"]?.GetValue<string>() == "income" ? "Incomes" : "Expenses";
        var columns = new HashSet<string>(StringComparer.Ordinal);
        using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction; metadata.CommandText = $"PRAGMA table_info([{key.Table}]);";
            using var reader = metadata.ExecuteReader(); while (reader.Read()) columns.Add(reader.GetString(1));
        }
        foreach (var pair in node)
        {
            if (pair.Key == "_splits") continue;
            if (pair.Key is "Id" or "CreatedAt" || !columns.Contains(pair.Key)) throw new InvalidOperationException("O servidor enviou um campo desconhecido.");
            if (pair.Value is not null && pair.Value is not JsonValue) throw new InvalidOperationException("Campo remoto invalido.");
            object? value = null;
            if (pair.Value is JsonValue field)
            {
                if (references.TryGetValue(pair.Key, out var targetTable))
                {
                    var guid = Guid.Parse(field.GetValue<string>());
                    if (!keys.TryGetValue(guid, out var target) || target.Table != targetTable) throw new InvalidOperationException("Vinculo remoto nao encontrado.");
                    value = target.LocalId;
                }
                else if (pair.Key == "Data" && key.Table == "Attachments") value = Convert.FromBase64String(field.GetValue<string>());
                else value = field.GetValueKind() switch { JsonValueKind.String => field.GetValue<string>(), JsonValueKind.Number => field.GetValue<long>(), _ => throw new InvalidOperationException("Tipo de dado remoto invalido.") };
            }
            values.Add(pair.Key, value);
        }
        var names = values.Keys.ToArray();
        var parameters = names.Select((name, i) => new KeyValuePair<string, object?>("$p" + i, values[name])).ToDictionary();
        Repository.Execute(connection, transaction,
            $"INSERT INTO [{key.Table}] ({string.Join(',', names.Select(n => $"[{n}]"))}) VALUES ({string.Join(',', parameters.Keys)}) ON CONFLICT(Id) DO UPDATE SET {string.Join(',', names.Where(n => n != "Id").Select((n) => $"[{n}]=excluded.[{n}]"))};", parameters);
        if (key.Table == "Expenses")
        {
            Repository.Execute(connection, transaction, "DELETE FROM ExpenseSplits WHERE ExpenseId=$id;", Parameters("$id", key.LocalId));
            if (node["_splits"] is not JsonArray splits || splits.Count > 100) throw new InvalidOperationException("Rateio remoto invalido.");
            if (splits.Count > 0)
                foreach (var split in splits)
                {
                    var category = keys[Guid.Parse(split!["CategoryId"]!.GetValue<string>())];
                    if (category.Table != "Categories") throw new InvalidOperationException("Categoria remota invalida.");
                    Repository.Execute(connection, transaction, "INSERT INTO ExpenseSplits(ExpenseId,CategoryId,Amount) VALUES($id,$category,$amount);", Parameters("$id", key.LocalId, "$category", category.LocalId, "$amount", split["Amount"]!.GetValue<long>()));
                }
        }
    }

    private static void ValidateMerged(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var check = connection.CreateCommand(); check.Transaction = transaction;
        check.CommandText = "PRAGMA foreign_key_check;";
        using (var reader = check.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("A sincronizacao deixaria vinculos invalidos. Revise os conflitos; os dados locais foram preservados.");
        check.CommandText = """
            SELECT (SELECT COUNT(*) FROM Expenses e WHERE EXISTS(SELECT 1 FROM ExpenseSplits s WHERE s.ExpenseId=e.Id) AND e.Amount<>(SELECT SUM(Amount) FROM ExpenseSplits s WHERE s.ExpenseId=e.Id))
             + (SELECT COUNT(*) FROM CreditCards c WHERE c.TotalLimit < COALESCE((SELECT SUM(p.Amount) FROM CardPurchases p WHERE p.CreditCardId=c.Id AND p.Status<>'Paga'),0))
             + (SELECT COUNT(*) FROM Incomes i JOIN Categories c ON c.Id=i.CategoryId WHERE c.Type<>'Receita')
             + (SELECT COUNT(*) FROM Expenses e JOIN Categories c ON c.Id=e.CategoryId WHERE c.Type<>'Despesa')
             + (SELECT COUNT(*) FROM Attachments a WHERE (a.EntityType='income' AND NOT EXISTS(SELECT 1 FROM Incomes i WHERE i.Id=a.EntityId)) OR (a.EntityType='expense' AND NOT EXISTS(SELECT 1 FROM Expenses e WHERE e.Id=a.EntityId)))
             + (SELECT COUNT(*) FROM Invoices i WHERE i.Status IN ('Fechada','Paga') AND
                (i.TotalAmount<>COALESCE((SELECT SUM(p.Amount) FROM CardPurchases p WHERE p.CreditCardId=i.CreditCardId AND p.InvoiceMonth=i.InvoiceMonth),0)
                 OR EXISTS(SELECT 1 FROM CardPurchases p WHERE p.CreditCardId=i.CreditCardId AND p.InvoiceMonth=i.InvoiceMonth AND p.Status<>CASE i.Status WHEN 'Paga' THEN 'Paga' ELSE 'Fechada' END)));
            """;
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) throw new InvalidOperationException("As alteracoes combinadas violam regras financeiras. Revise categorias, rateios, faturas e limites antes de sincronizar.");
    }

    private static Dictionary<string, object?> Parameters(params object?[] values)
    {
        var result = new Dictionary<string, object?>();
        for (var i = 0; i < values.Length; i += 2) result.Add((string)values[i]!, values[i + 1]);
        return result;
    }
}
