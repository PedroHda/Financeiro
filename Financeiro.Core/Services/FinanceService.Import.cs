using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceiroApp.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Services;

public sealed partial class FinanceService
{
    public List<ImportEntry> PrepareImport(int accountId, IEnumerable<ImportEntry> source)
    {
        EnsureActiveAccount(accountId);
        var entries = source.ToList();
        if (entries.Count > 10000) throw new InvalidOperationException("Importe ate 10.000 registros por vez.");
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var rules = _repository.Query("SELECT MatchText,Type,CategoryId FROM CategoryRules ORDER BY length(MatchText) DESC,Id;");
        foreach (var entry in entries)
        {
            Require(entry.Description, "Movimento sem descricao.");
            if (entry.Amount == 0) throw new InvalidOperationException("Movimento sem valor.");
            _ = MoneyStorage.ToCents(entry.Amount);
            var basis = string.IsNullOrWhiteSpace(entry.SourceId)
                ? $"{entry.Date:yyyy-MM-dd}|{entry.Amount.ToString("0.00", CultureInfo.InvariantCulture)}|{entry.Description.Trim().ToUpperInvariant()}"
                : entry.SourceId;
            if (entry.SourceId.Length == 0)
            {
                occurrences.TryGetValue(basis, out var occurrence);
                occurrences[basis] = occurrence + 1;
                basis += "|" + occurrence;
            }
            entry.Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)));
            var known = Number("SELECT COUNT(*) FROM ImportedTransactions WHERE AccountId=$account AND Fingerprint=$fingerprint;", P("$account", accountId, "$fingerprint", entry.Fingerprint)) > 0;
            var sameInFile = !identities.Add(entry.Fingerprint);
            var table = entry.Amount > 0 ? "Incomes" : "Expenses";
            var dateColumn = entry.Amount > 0 ? "ReceivedDate" : "ExpenseDate";
            var possible = Number($"SELECT COUNT(*) FROM {table} WHERE AccountId=$account AND {dateColumn}=$date AND Amount=$amount AND lower(trim(Description))=lower($description);",
                P("$account", accountId, "$date", entry.Date.Date, "$amount", Math.Abs(entry.Amount), "$description", entry.Description.Trim())) > 0;
            entry.Duplicate = known || sameInFile || possible;
            entry.Selected = !entry.Duplicate;
            foreach (DataRow rule in rules.Rows)
            {
                if (Convert.ToString(rule["Type"]) == entry.Type && entry.Description.Contains(Convert.ToString(rule["MatchText"])!, StringComparison.OrdinalIgnoreCase))
                { entry.CategoryId = Convert.ToInt32(rule["CategoryId"]); break; }
            }
        }
        return entries;
    }

    public int ImportStatement(int accountId, IReadOnlyList<ImportEntry> entries, int incomeCategoryId, int expenseCategoryId)
    {
        EnsureActiveAccount(accountId);
        if (entries.Count > 10000) throw new InvalidOperationException("Importe ate 10.000 movimentos por vez.");
        var count = 0;
        // Recompute identities without changing the user's selections or categories.
        var prepared = PrepareImport(accountId, entries.Select(e => new ImportEntry { Date = e.Date, Description = e.Description, Amount = e.Amount, SourceId = e.SourceId }));
        _repository.Transaction((connection, transaction) =>
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.Selected) continue;
                if (entry.Date.Date > DateTime.Today) throw new InvalidOperationException("O extrato contem movimento futuro. Revise a data antes de importar.");
                if (Convert.ToInt32(Repository.Scalar(connection, transaction, "SELECT COUNT(*) FROM ImportedTransactions WHERE AccountId=$account AND Fingerprint=$fingerprint;", P("$account", accountId, "$fingerprint", prepared[i].Fingerprint))) > 0) continue;
                var income = entry.Amount > 0;
                var category = entry.CategoryId > 0 ? entry.CategoryId : income ? incomeCategoryId : expenseCategoryId;
                var type = income ? FinanceTypes.Income : FinanceTypes.Expense;
                if (Convert.ToInt32(Repository.Scalar(connection, transaction, "SELECT COUNT(*) FROM Categories WHERE Id=$id AND Type=$type AND IsActive=1;", P("$id", category, "$type", type))) == 0)
                    throw new InvalidOperationException($"Selecione uma categoria ativa de {type.ToLowerInvariant()} para {entry.Description}.");
                var entryId = Repository.Insert(connection, transaction, income
                    ? "INSERT INTO Incomes(Description,Amount,ReceivedDate,CategoryId,AccountId,PaymentMethod,Notes,Status) VALUES($description,$amount,$date,$category,$account,'Extrato',$notes,'Recebido')"
                    : "INSERT INTO Expenses(Description,Amount,ExpenseDate,DueDate,CategoryId,AccountId,PaymentMethod,Notes,Status) VALUES($description,$amount,$date,$date,$category,$account,'Extrato',$notes,'Pago')",
                    P("$description", entry.Description.Trim(), "$amount", Math.Abs(entry.Amount), "$date", entry.Date.Date, "$category", category, "$account", accountId, "$notes", entry.SourceId));
                Repository.Execute(connection, transaction, "INSERT INTO ImportedTransactions(AccountId,Fingerprint,EntryKind,EntryId) VALUES($account,$fingerprint,$kind,$id);",
                    P("$account", accountId, "$fingerprint", prepared[i].Fingerprint, "$kind", income ? "income" : "expense", "$id", entryId));
                count++;
            }
        });
        RefreshDerivedData();
        return count;
    }

    public DataTable GetCategoryRules() => _repository.Query("SELECT r.Id,r.MatchText AS Texto,r.Type AS Tipo,c.Name AS Categoria FROM CategoryRules r JOIN Categories c ON c.Id=r.CategoryId ORDER BY r.Type,r.MatchText;");

    public void SaveCategoryRule(string matchText, string type, int categoryId)
    {
        Require(matchText, "Informe o texto da regra.");
        if (matchText.Trim().Length < 3) throw new InvalidOperationException("Use ao menos tres caracteres na regra.");
        EnsureCategoryType(categoryId, type);
        _repository.Execute("INSERT INTO CategoryRules(MatchText,Type,CategoryId) VALUES($text,$type,$category) ON CONFLICT(MatchText,Type) DO UPDATE SET CategoryId=excluded.CategoryId;",
            P("$text", matchText.Trim().ToUpperInvariant(), "$type", type, "$category", categoryId));
    }

    public void DeleteCategoryRule(int id) => _repository.Execute("DELETE FROM CategoryRules WHERE Id=$id;", P("$id", id));
}
