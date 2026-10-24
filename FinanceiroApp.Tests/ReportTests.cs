using System.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void MonthlyReportRespectsExactPeriodAndAccount()
    {
        var day = new DateTime(2026, 10, 15);
        _db.Service.SaveIncome(new IncomeInput { Description = "Dentro", Amount = 100, ReceivedDate = day, CategoryId = _db.IncomeCategoryId, AccountId = _db.AccountId });
        _db.Service.SaveIncome(new IncomeInput { Description = "Fora", Amount = 300, ReceivedDate = day.AddDays(-1), CategoryId = _db.IncomeCategoryId, AccountId = _db.AccountId });
        _db.Service.SaveAccount(new AccountInput { Name = "Segunda", Type = "Outros" });
        var secondAccount = _db.Service.GetAccounts(false).Single(item => item.Name == "Segunda").Id;
        _db.Service.SaveIncome(new IncomeInput { Description = "Outra conta", Amount = 500, ReceivedDate = day, CategoryId = _db.IncomeCategoryId, AccountId = secondAccount });
        var report = _db.Service.GetReport("Saldo mensal", new ReportFilter { StartDate = day, EndDate = day, AccountId = _db.AccountId });
        Assert.Single(report.Rows.Cast<DataRow>());
        Assert.Equal(100m, Convert.ToDecimal(report.Rows[0]["Receitas"]));
    }

    [Fact]
    public void CategoryReportIncludesCardPurchasesAndRespectsCardFilter()
    {
        var cardId = _db.CreateCard();
        var day = DateTime.Today;
        _db.Service.SaveCardPurchase(new CardPurchaseInput { Description = "Cartao", Amount = 40, PurchaseDate = day, CategoryId = _db.ExpenseCategoryId, CreditCardId = cardId });
        _db.Service.SaveExpense(new ExpenseInput { Description = "Debito", Amount = 60, DueDate = day, CategoryId = _db.ExpenseCategoryId, AccountId = _db.AccountId });
        var filter = new ReportFilter { StartDate = day, EndDate = day };
        Assert.Equal(100m, Convert.ToDecimal(_db.Service.GetReport("Despesas por categoria", filter).Rows[0]["Total"]));
        filter.CreditCardId = cardId;
        Assert.Equal(40m, Convert.ToDecimal(_db.Service.GetReport("Despesas por categoria", filter).Rows[0]["Total"]));
    }

    [Fact]
    public void InvalidReportPeriodIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => _db.Service.GetReport("Saldo mensal", new ReportFilter { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(-1) }));
    }

    public void Dispose() => _db.Dispose();
}
