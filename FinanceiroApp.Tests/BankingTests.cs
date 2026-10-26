using System.Data;
using FinanceiroApp.Models;
using FinanceiroApp.Services;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Tests;

public sealed class BankingTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private static ReportFilter All => new() { StartDate = new DateTime(2000, 1, 1), EndDate = new DateTime(2100, 12, 31) };

    [Fact]
    public void TransfersPreserveTotalAndDoNotBecomeIncomeOrExpense()
    {
        _db.Service.SaveAccount(new AccountInput { Name = "Destino", Type = "Carteira" });
        var destination = _db.Service.GetAccounts(false).Single(a => a.Name == "Destino").Id;
        _db.Service.SaveTransfer(new TransferInput { FromAccountId = _db.AccountId, ToAccountId = destination, Amount = 123.45m });
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(876.55m, _db.Service.GetAccountBalanceAt(_db.AccountId, DateTime.Today));
        Assert.Equal(123.45m, _db.Service.GetAccountBalanceAt(destination, DateTime.Today));
        Assert.Empty(_db.Service.SearchIncomes(All, "", "Data").Rows.Cast<DataRow>());
        Assert.Empty(_db.Service.SearchExpenses(All, "", "Data").Rows.Cast<DataRow>());
        var id = Convert.ToInt32(_db.Service.SearchTransfers(All.StartDate, All.EndDate).Rows[0]["Id"]);
        _db.Service.DeleteTransfer(id);
        Assert.Equal(1000m, _db.Service.GetAccountBalanceAt(_db.AccountId, DateTime.Today));
    }

    [Fact]
    public void TransferValidationNeverChangesBalances()
    {
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveTransfer(new TransferInput { FromAccountId = _db.AccountId, ToAccountId = _db.AccountId, Amount = 20 }));
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveTransfer(new TransferInput { FromAccountId = _db.AccountId, ToAccountId = 999, Amount = 20 }));
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void ReconciliationRecordsDifferenceWithoutAdjustingBalance()
    {
        _db.Service.ReconcileAccount(_db.AccountId, DateTime.Today, 999.99m, "Conferido");
        Assert.Equal(-0.01m, _db.Service.GetReconciliations().Rows[0]["Diferenca"]);
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void CsvQuotedFieldsAndDuplicateImportAreHandled()
    {
        var csv = $"Data;Descricao;Valor;Id\n{DateTime.Today:dd/MM/yyyy};\"Mercado; bairro\";-10,01;1\n{DateTime.Today:dd/MM/yyyy};Salario;100,02;2";
        var entries = _db.Service.PrepareImport(_db.AccountId, StatementReader.ReadCsv(csv));
        Assert.Equal("Mercado; bairro", entries[0].Description);
        Assert.Equal(2, _db.Service.ImportStatement(_db.AccountId, entries, _db.IncomeCategoryId, _db.ExpenseCategoryId));
        Assert.Equal(1090.01m, _db.Service.GetDashboardSummary().CurrentBalance);
        var again = _db.Service.PrepareImport(_db.AccountId, StatementReader.ReadCsv(csv));
        Assert.All(again, e => Assert.True(e.Duplicate));
        Assert.Equal(0, _db.Service.ImportStatement(_db.AccountId, again, _db.IncomeCategoryId, _db.ExpenseCategoryId));
    }

    [Fact]
    public void ImportIsAtomicAndRechecksFingerprint()
    {
        var entries = _db.Service.PrepareImport(_db.AccountId, StatementReader.ReadCsv($"Data;Descricao;Valor;Id\n{DateTime.Today:dd/MM/yyyy};A;1,00;1\n{DateTime.Today:dd/MM/yyyy};B;-2,00;2"));
        Assert.Throws<InvalidOperationException>(() => _db.Service.ImportStatement(_db.AccountId, entries, _db.IncomeCategoryId, _db.IncomeCategoryId));
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(2, _db.Service.ImportStatement(_db.AccountId, entries, _db.IncomeCategoryId, _db.ExpenseCategoryId));
        Assert.Equal(0, _db.Service.ImportStatement(_db.AccountId, entries, _db.IncomeCategoryId, _db.ExpenseCategoryId));
    }

    [Fact]
    public void XmlOfxUsesPostedLocalDateAndDecimalMoney()
    {
        var entries = StatementReader.ReadOfx("<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL</CURDEF><BANKTRANLIST><STMTTRN><DTPOSTED>20261001120000[-3:BRT]</DTPOSTED><TRNAMT>-12.34</TRNAMT><FITID>ABC</FITID><MEMO>Compra</MEMO></STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>");
        Assert.Single(entries);
        Assert.Equal(-12.34m, entries[0].Amount);
        Assert.Equal(new DateTime(2026, 10, 1), entries[0].Date);
        Assert.Equal("ofx:ABC", entries[0].SourceId);
        Assert.Throws<InvalidOperationException>(() => StatementReader.ReadOfx("<!DOCTYPE OFX SYSTEM 'https://example.org/evil'><OFX/>"));
    }

    [Fact]
    public void SgmlOfxBankStatementIsParsedByOfxSharp()
    {
        var entries = StatementReader.ReadOfx("""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRSV1><SONRS><STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <DTSERVER>20261026120000[-3:BRT]<LANGUAGE>POR</SONRS></SIGNONMSGSRSV1>
            <BANKMSGSRSV1><STMTTRNRS><TRNUID>1<STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <STMTRS><CURDEF>BRL<BANKACCTFROM><BANKID>1<ACCTID>123<ACCTTYPE>CHECKING</BANKACCTFROM>
            <BANKTRANLIST><DTSTART>20261001000000[-3:BRT]<DTEND>20261026235959[-3:BRT]
            <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20261002120000[-3:BRT]<TRNAMT>-12.34<FITID>ABC123<NAME>Compra<MEMO>Mercado</STMTTRN>
            </BANKTRANLIST><LEDGERBAL><BALAMT>0.00<DTASOF>20261026120000[-3:BRT]</LEDGERBAL>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """);
        var entry = Assert.Single(entries);
        Assert.Equal(-12.34m, entry.Amount);
        Assert.Equal("ofx:ABC123", entry.SourceId);
        Assert.Equal(new DateTime(2026,10,2), entry.Date);
    }

    [Fact]
    public void RulesSuggestCategoriesAndCanBeRemoved()
    {
        _db.Service.SaveCategoryRule("mercado", FinanceTypes.Expense, _db.ExpenseCategoryId);
        var entries = _db.Service.PrepareImport(_db.AccountId, [new ImportEntry { Description = "Mercado Central", Amount = -10, Date = DateTime.Today }]);
        Assert.Equal(_db.ExpenseCategoryId, entries[0].CategoryId);
        _db.Service.DeleteCategoryRule(Convert.ToInt32(_db.Service.GetCategoryRules().Rows[0]["Id"]));
        Assert.Equal(0, _db.Service.GetCategoryRules().Rows.Count);
    }

    [Fact]
    public void ForecastIncludesPendingIncomeRecurrencesAndNoDuplicateGeneratedExpense()
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        _db.Service.SaveIncome(new IncomeInput { Description = "Prevista", Amount = 500, AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId, ReceivedDate = month, Status = FinanceStatus.Pending });
        _db.Service.SaveRecurringExpense(new RecurringExpenseInput { Description = "Internet", Amount = 100, DayOfMonth = 1, StartDate = month, AccountId = _db.AccountId, CategoryId = _db.ExpenseCategoryId });
        var before = _db.Service.GetForecast(2)[1];
        Assert.Equal(500m, before.Income);
        Assert.Equal(100m, before.Outgoing);
        Assert.Equal(1400m, before.ClosingBalance);
        _db.Service.GenerateRecurringExpenses(1);
        Assert.Equal(before, _db.Service.GetForecast(2)[1]);
    }

    [Fact]
    public void SplitsPreserveTotalsAndAffectCategoryReports()
    {
        var second = _db.Service.GetCategories(FinanceTypes.Expense, false).Last().Id;
        _db.Service.SaveExpense(new ExpenseInput { Description = "Mercado", Amount = 100, AccountId = _db.AccountId, CategoryId = _db.ExpenseCategoryId, Status = FinanceStatus.Paid });
        var id = Convert.ToInt32(_db.Service.SearchExpenses(All, "", "Data").Rows[0]["Id"]);
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveExpenseSplits(id, [new ExpenseSplit(second, 99.99m)]));
        _db.Service.SaveExpenseSplits(id, [new ExpenseSplit(_db.ExpenseCategoryId, 60), new ExpenseSplit(second, 40)]);
        Assert.Equal(100m, _db.Service.GetDashboardCategoryExpenses().Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["Total"])));
        Assert.Equal(2, _db.Service.GetReport("Despesas por categoria", All).Rows.Count);
        Assert.Equal(900m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void MoneyIsStoredAsIntegerCentsAndRejectsFractionalCents()
    {
        _db.Service.SaveIncome(new IncomeInput { Description = "Exato", Amount = 0.29m, AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId });
        using var connection = new SqliteConnection($"Data Source={_db.Service.DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Amount FROM Incomes;";
        Assert.Equal(29L, command.ExecuteScalar());
        command.CommandText = "SELECT typeof(Amount) FROM Incomes;";
        Assert.Equal("integer", command.ExecuteScalar());
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveAccount(new AccountInput { Name = "Invalida", Type = "Conta", InitialBalance = 1.001m }));
    }

    [Fact]
    public void ForecastIncludesOverdueObligationsInFirstMonth()
    {
        _db.Service.SaveExpense(new ExpenseInput { Description = "Vencida", Amount = 12.34m, AccountId = _db.AccountId, CategoryId = _db.ExpenseCategoryId, DueDate = DateTime.Today.AddMonths(-2) });
        Assert.Equal(12.34m, _db.Service.GetForecast()[0].Outgoing);
    }

    [Fact]
    public void LargeBalancesKeepExactCentsInReportsAndDashboard()
    {
        const decimal value = 90000000000000.29m;
        _db.Service.SaveAccount(new AccountInput { Id = _db.AccountId, Name = "Grande", Type = "Conta", InitialBalance = value });
        _db.Service.SaveIncome(new IncomeInput { Description = "Centavo", Amount = 0.01m, AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId });
        Assert.Equal(value + 0.01m, _db.Service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(value + 0.01m, _db.Service.SearchAccounts("").Rows[0]["SaldoAtual"]);
    }

    [Fact]
    public void AttachmentsAreBoundedAndDeletedWithEntry()
    {
        _db.Service.SaveIncome(new IncomeInput { Description = "A", Amount = 1, AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId });
        var id = Convert.ToInt32(_db.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        _db.Service.AddAttachment("income", id, "../../recibo.txt", [1, 2, 3]);
        var attachment = Assert.Single(_db.Service.GetAttachments("income", id));
        Assert.Equal("recibo.txt", attachment.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, _db.Service.ReadAttachment(attachment.Id));
        Assert.Throws<InvalidOperationException>(() => _db.Service.AddAttachment("income", id, "grande", new byte[5242881]));
        _db.Service.DeleteIncome(id);
        Assert.Throws<InvalidOperationException>(() => _db.Service.ReadAttachment(attachment.Id));
    }

    public void Dispose() => _db.Dispose();
}
