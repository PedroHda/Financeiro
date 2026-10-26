using System.Data;
using FinanceiroApp.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Services;

public sealed partial class FinanceService
{
    public IReadOnlyList<ExpenseSplit> GetExpenseSplits(int expenseId) => _repository.Query("SELECT CategoryId,Amount FROM ExpenseSplits WHERE ExpenseId=$id ORDER BY Id;", P("$id", expenseId))
        .Rows.Cast<DataRow>().Select(r => new ExpenseSplit(Convert.ToInt32(r["CategoryId"]), ToDecimal(r["Amount"]))).ToArray();

    public void SaveExpenseSplits(int expenseId, IReadOnlyList<ExpenseSplit> splits)
    {
        if (Number("SELECT COUNT(*) FROM Expenses WHERE Id=$id;", P("$id", expenseId)) == 0) throw new InvalidOperationException("Despesa nao encontrada.");
        var amount = Money("SELECT Amount FROM Expenses WHERE Id=$id;", P("$id", expenseId));
        foreach (var split in splits)
        {
            RequirePositive(split.Amount, "Cada parte precisa ter um valor positivo com ate duas casas decimais.");
            EnsureCategoryType(split.CategoryId, FinanceTypes.Expense);
        }
        if (splits.Count > 0 && splits.Sum(s => s.Amount) != amount) throw new InvalidOperationException("A soma das partes deve ser igual ao valor da despesa.");
        _repository.Transaction((connection, transaction) =>
        {
            Repository.Execute(connection, transaction, "DELETE FROM ExpenseSplits WHERE ExpenseId=$id;", P("$id", expenseId));
            foreach (var split in splits)
                Repository.Execute(connection, transaction, "INSERT INTO ExpenseSplits(ExpenseId,CategoryId,Amount) VALUES($id,$category,$amount);", P("$id", expenseId, "$category", split.CategoryId, "$amount", split.Amount));
        });
    }

    public IReadOnlyList<AttachmentInfo> GetAttachments(string type, int entryId)
    {
        ValidateEntry(type, entryId);
        return _repository.Query("SELECT Id,Name,length(Data) AS Size FROM Attachments WHERE EntityType=$type AND EntityId=$id ORDER BY Id;", P("$type", type, "$id", entryId))
            .Rows.Cast<DataRow>().Select(r => new AttachmentInfo(Convert.ToInt32(r["Id"]), Convert.ToString(r["Name"])!, Convert.ToInt32(r["Size"]))).ToArray();
    }

    public void AddAttachment(string type, int entryId, string name, byte[] bytes)
    {
        ValidateEntry(type, entryId);
        name = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new InvalidOperationException("Nome de arquivo invalido.");
        if (bytes.Length is 0 or > 5 * 1024 * 1024) throw new InvalidOperationException("O comprovante deve ter entre 1 byte e 5 MB.");
        if (GetAttachments(type, entryId).Count >= 10) throw new InvalidOperationException("Use ate dez comprovantes por lancamento.");
        _repository.Execute("INSERT INTO Attachments(EntityType,EntityId,Name,Data) VALUES($type,$id,$name,$data);", P("$type", type, "$id", entryId, "$name", name, "$data", bytes));
    }

    public byte[] ReadAttachment(int id) => _repository.Scalar("SELECT Data FROM Attachments WHERE Id=$id;", P("$id", id)) as byte[] ?? throw new InvalidOperationException("Comprovante nao encontrado.");

    public void DeleteAttachment(int id) => _repository.Execute("DELETE FROM Attachments WHERE Id=$id;", P("$id", id));

    private void ValidateEntry(string type, int id)
    {
        var table = type switch { "income" => "Incomes", "expense" => "Expenses", _ => throw new InvalidOperationException("Tipo invalido.") };
        if (Number($"SELECT COUNT(*) FROM {table} WHERE Id=$id;", P("$id", id)) == 0) throw new InvalidOperationException("Selecione um lancamento existente.");
    }
}
