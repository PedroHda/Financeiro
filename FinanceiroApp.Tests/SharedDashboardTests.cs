using System.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Tests;

public sealed class SharedDashboardTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void DashboardMonthSelectorUsesRequestedPeriod()
    {
        var firstMonth = new DateTime(2026, 1, 1);
        _db.Service.SaveIncome(new IncomeInput { Description = "Janeiro", Amount = 100, ReceivedDate = firstMonth, AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId });
        _db.Service.SaveIncome(new IncomeInput { Description = "Fevereiro", Amount = 250, ReceivedDate = firstMonth.AddMonths(1), AccountId = _db.AccountId, CategoryId = _db.IncomeCategoryId });
        Assert.Equal(100m, _db.Service.GetDashboardSummary(firstMonth).MonthlyIncome);
        Assert.Equal(250m, _db.Service.GetDashboardSummary(firstMonth.AddMonths(1)).MonthlyIncome);
        Assert.Equal(1350m, _db.Service.GetDashboardSummary(firstMonth).CurrentBalance);
    }

    [Fact]
    public void SpendingCategoriesUseTheSameSelectedMonth()
    {
        var firstMonth = new DateTime(2026, 1, 1);
        _db.Service.SaveExpense(new ExpenseInput { Description = "Janeiro", Amount = 30, DueDate = firstMonth, AccountId = _db.AccountId, CategoryId = _db.ExpenseCategoryId });
        _db.Service.SaveExpense(new ExpenseInput { Description = "Fevereiro", Amount = 45, DueDate = firstMonth.AddMonths(1), AccountId = _db.AccountId, CategoryId = _db.ExpenseCategoryId });
        var january = _db.Service.GetDashboardCategoryExpenses(firstMonth);
        Assert.Equal(30m, january.Rows.Cast<DataRow>().Sum(row => Convert.ToDecimal(row["Total"])));
        var february = _db.Service.GetDashboardCategoryExpenses(firstMonth.AddMonths(1));
        Assert.Equal(45m, february.Rows.Cast<DataRow>().Sum(row => Convert.ToDecimal(row["Total"])));
    }

    public void Dispose() => _db.Dispose();
}
