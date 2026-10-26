using System.Data;
using System.Globalization;
using FinanceiroApp.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Services;

public sealed partial class FinanceService
{
    public void SaveTransfer(TransferInput input)
    {
        RequirePositive(input.Amount, "Informe um valor positivo com ate duas casas decimais.");
        EnsureActiveAccount(input.FromAccountId);
        EnsureActiveAccount(input.ToAccountId);
        if (input.FromAccountId == input.ToAccountId) throw new InvalidOperationException("Origem e destino devem ser contas diferentes.");
        if (input.Date.Date > DateTime.Today) throw new InvalidOperationException("Transferencias representam movimentacoes realizadas; a data nao pode ser futura.");
        var values = P("$from", input.FromAccountId, "$to", input.ToAccountId, "$amount", input.Amount, "$date", input.Date.Date, "$notes", input.Notes.Trim(), "$id", input.Id);
        _repository.Execute(input.Id == 0
            ? "INSERT INTO Transfers(FromAccountId,ToAccountId,Amount,TransferDate,Notes) VALUES($from,$to,$amount,$date,$notes);"
            : "UPDATE Transfers SET FromAccountId=$from,ToAccountId=$to,Amount=$amount,TransferDate=$date,Notes=$notes WHERE Id=$id;", values);
        RecalculateAccountBalances();
    }

    public DataTable SearchTransfers(DateTime from, DateTime to) => _repository.Query("""
        SELECT t.Id, t.TransferDate AS Data, a.Name AS Origem, b.Name AS Destino, t.Amount AS Valor, t.Notes AS Observacoes
        FROM Transfers t JOIN Accounts a ON a.Id=t.FromAccountId JOIN Accounts b ON b.Id=t.ToAccountId
        WHERE t.TransferDate BETWEEN $from AND $to ORDER BY t.TransferDate DESC,t.Id DESC;
        """, P("$from", from.Date, "$to", to.Date));

    public void DeleteTransfer(int id)
    {
        _repository.Execute("DELETE FROM Transfers WHERE Id=$id;", P("$id", id));
        RecalculateAccountBalances();
    }

    public decimal GetAccountBalanceAt(int accountId, DateTime date)
    {
        RequireSelected(accountId, "Selecione uma conta.");
        if (Number("SELECT COUNT(*) FROM Accounts WHERE Id=$id;", P("$id", accountId)) == 0) throw new InvalidOperationException("Conta nao encontrada.");
        return Money("""
            SELECT InitialBalance
             + COALESCE((SELECT SUM(Amount) FROM Incomes WHERE AccountId=$id AND Status='Recebido' AND ReceivedDate <= $date),0)
             - COALESCE((SELECT SUM(Amount) FROM Expenses WHERE AccountId=$id AND Status='Pago' AND DueDate <= $date),0)
             - COALESCE((SELECT SUM(TotalAmount) FROM Invoices WHERE PaidAccountId=$id AND Status='Paga' AND PaidAt <= $date),0)
             + COALESCE((SELECT SUM(Amount) FROM Transfers WHERE ToAccountId=$id AND TransferDate <= $date),0)
             - COALESCE((SELECT SUM(Amount) FROM Transfers WHERE FromAccountId=$id AND TransferDate <= $date),0)
            FROM Accounts WHERE Id=$id;
            """, P("$id", accountId, "$date", date.Date));
    }

    public void ReconcileAccount(int accountId, DateTime date, decimal bankBalance, string notes)
    {
        if (date.Date > DateTime.Today) throw new InvalidOperationException("A conciliacao nao pode ter data futura.");
        var bookBalance = GetAccountBalanceAt(accountId, date);
        _repository.Execute("INSERT INTO Reconciliations(AccountId,BalanceDate,BankBalance,BookBalance,Notes) VALUES($account,$date,$bank,$book,$notes);",
            P("$account", accountId, "$date", date.Date, "$bank", bankBalance, "$book", bookBalance, "$notes", notes.Trim()));
    }

    public DataTable GetReconciliations(int accountId = 0) => _repository.Query("""
        SELECT r.Id,a.Name AS Conta,r.BalanceDate AS Data,r.BankBalance AS SaldoInformado,
               r.BookBalance AS SaldoAtual,r.BankBalance-r.BookBalance AS Diferenca,r.Notes AS Observacoes
        FROM Reconciliations r JOIN Accounts a ON a.Id=r.AccountId WHERE $id=0 OR r.AccountId=$id ORDER BY r.BalanceDate DESC,r.Id DESC;
        """, P("$id", accountId));

    public IReadOnlyList<ForecastMonth> GetForecast(int months = 6, DateTime? today = null)
    {
        if (months is < 1 or > 24) throw new InvalidOperationException("Escolha entre 1 e 24 meses.");
        RefreshDerivedData();
        var reference = (today ?? DateTime.Today).Date;
        var start = new DateTime(reference.Year, reference.Month, 1);
        var balance = Money("SELECT COALESCE(SUM(CurrentBalance),0) FROM Accounts WHERE IsActive=1;");
        var recurring = _repository.Query("SELECT * FROM RecurringExpenses WHERE IsActive=1;");
        var cardBills = _repository.Query("""
            SELECT cp.InvoiceMonth,c.ClosingDay,c.DueDay,COALESCE(inv.DueDate,'') AS DueDate,SUM(cp.Amount) AS Total
            FROM CardPurchases cp JOIN CreditCards c ON c.Id=cp.CreditCardId
            LEFT JOIN Invoices inv ON inv.CreditCardId=cp.CreditCardId AND inv.InvoiceMonth=cp.InvoiceMonth
            WHERE cp.Status<>'Paga' GROUP BY cp.CreditCardId,cp.InvoiceMonth;
            """);
        var result = new List<ForecastMonth>();
        for (var i = 0; i < months; i++)
        {
            var month = start.AddMonths(i);
            var from = i == 0 ? reference : month;
            var end = month.AddMonths(1).AddDays(-1);
            var income = Money("SELECT COALESCE(SUM(i.Amount),0) FROM Incomes i JOIN Accounts a ON a.Id=i.AccountId WHERE a.IsActive=1 AND i.Status='Pendente' AND i.ReceivedDate BETWEEN $from AND $end;", P("$from", from, "$end", end));
            var obligationStart = i == 0 ? DateTime.MinValue : month;
            var outgoing = Money("SELECT COALESCE(SUM(e.Amount),0) FROM Expenses e JOIN Accounts a ON a.Id=e.AccountId WHERE a.IsActive=1 AND e.Status<>'Pago' AND e.DueDate BETWEEN $from AND $end;", P("$from", obligationStart, "$end", end));
            foreach (DataRow bill in cardBills.Rows)
            {
                var postedMonth = Convert.ToString(bill["InvoiceMonth"])!;
                var dueText = Convert.ToString(bill["DueDate"]);
                DateTime due;
                if (!string.IsNullOrEmpty(dueText)) due = DateTime.Parse(dueText, CultureInfo.InvariantCulture);
                else
                {
                    var closing = MonthDay(postedMonth, Convert.ToInt32(bill["ClosingDay"]));
                    due = MonthDay(postedMonth, Convert.ToInt32(bill["DueDay"]));
                    if (due < closing) due = MonthDay(ParseMonth(postedMonth).AddMonths(1).ToString("yyyy-MM"), Convert.ToInt32(bill["DueDay"]));
                }
                if (due >= obligationStart && due <= end) outgoing += ToDecimal(bill["Total"]);
            }
            foreach (DataRow row in recurring.Rows)
            {
                var due = new DateTime(month.Year, month.Month, Math.Min(Convert.ToInt32(row["DayOfMonth"]), DateTime.DaysInMonth(month.Year, month.Month)));
                var begin = DateTime.Parse(Convert.ToString(row["StartDate"])!, CultureInfo.InvariantCulture);
                var finish = row.IsNull("EndDate") ? DateTime.MaxValue : DateTime.Parse(Convert.ToString(row["EndDate"])!, CultureInfo.InvariantCulture);
                if (due < month || due > end || due < begin || due > finish) continue;
                if (Number("SELECT COUNT(*) FROM Expenses WHERE RecurringExpenseId=$id AND substr(DueDate,1,7)=$month;", P("$id", row["Id"], "$month", month.ToString("yyyy-MM"))) == 0
                    && Number("SELECT COUNT(*) FROM Accounts WHERE Id=$id AND IsActive=1;", P("$id", row["AccountId"])) > 0)
                    outgoing += ToDecimal(row["Amount"]);
            }
            result.Add(new ForecastMonth(month, balance, income, outgoing, balance + income - outgoing));
            balance += income - outgoing;
        }
        return result;
    }

    public void DuplicateEntry(string type, int id, DateTime newDate)
    {
        if (type == "income")
        {
            var row = _repository.Query("SELECT * FROM Incomes WHERE Id=$id;", P("$id", id)).Rows.Cast<DataRow>().SingleOrDefault() ?? throw new InvalidOperationException("Receita nao encontrada.");
            SaveIncome(new IncomeInput { Description = Convert.ToString(row["Description"])!, Amount = ToDecimal(row["Amount"]), CategoryId = Convert.ToInt32(row["CategoryId"]), AccountId = Convert.ToInt32(row["AccountId"]), ReceivedDate = newDate.Date, Status = FinanceStatus.Pending, PaymentMethod = Convert.ToString(row["PaymentMethod"])!, Notes = Convert.ToString(row["Notes"])! });
        }
        else if (type == "expense")
        {
            var row = _repository.Query("SELECT * FROM Expenses WHERE Id=$id;", P("$id", id)).Rows.Cast<DataRow>().SingleOrDefault() ?? throw new InvalidOperationException("Despesa nao encontrada.");
            SaveExpense(new ExpenseInput { Description = Convert.ToString(row["Description"])!, Amount = ToDecimal(row["Amount"]), CategoryId = Convert.ToInt32(row["CategoryId"]), AccountId = row.IsNull("AccountId") ? null : Convert.ToInt32(row["AccountId"]), CreditCardId = row.IsNull("CreditCardId") ? null : Convert.ToInt32(row["CreditCardId"]), ExpenseDate = newDate.Date, DueDate = newDate.Date, Status = FinanceStatus.Pending, PaymentMethod = Convert.ToString(row["PaymentMethod"])!, Notes = Convert.ToString(row["Notes"])! });
        }
        else throw new InvalidOperationException("Tipo de lancamento invalido.");
    }

    public void SetEntryStatus(string type, IReadOnlyCollection<int> ids, string status)
    {
        if (ids.Count == 0) throw new InvalidOperationException("Selecione ao menos um lancamento.");
        if (type != "income" && type != "expense") throw new InvalidOperationException("Tipo invalido.");
        if (!(type == "income" ? new[] { FinanceStatus.Received, FinanceStatus.Pending } : new[] { FinanceStatus.Paid, FinanceStatus.Pending }).Contains(status)) throw new InvalidOperationException("Status invalido.");
        _repository.Transaction((connection, transaction) =>
        {
            foreach (var id in ids.Distinct())
            {
                if (type == "expense" && status == FinanceStatus.Paid && Convert.ToInt32(Repository.Scalar(connection, transaction, "SELECT COUNT(*) FROM Expenses WHERE Id=$id AND AccountId IS NOT NULL;", P("$id", id))) == 0)
                    throw new InvalidOperationException("Todas as despesas pagas precisam de conta de pagamento.");
                Repository.Execute(connection, transaction, $"UPDATE {(type == "income" ? "Incomes" : "Expenses")} SET Status=$status WHERE Id=$id;", P("$status", status, "$id", id));
            }
        });
        RefreshDerivedData();
    }

    private void EnsureActiveAccount(int accountId)
    {
        if (Number("SELECT COUNT(*) FROM Accounts WHERE Id=$id AND IsActive=1;", P("$id", accountId)) == 0)
            throw new InvalidOperationException("Selecione uma conta ativa e existente.");
    }
}
