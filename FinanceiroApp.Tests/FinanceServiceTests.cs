using System.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Tests;

public sealed class FinanceServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    private static ReportFilter AllDates() => new() { StartDate = new DateTime(2000, 1, 1), EndDate = new DateTime(2100, 12, 31) };
    private static decimal Amount(DataRow row, string column) => Convert.ToDecimal(row[column]);

    [Fact]
    public void IncomeStatusEditsAndDeletionRecalculateBalance()
    {
        var income = new IncomeInput
        {
            Description = "Salario teste", Amount = 123.45m, CategoryId = _db.IncomeCategoryId,
            AccountId = _db.AccountId, Status = FinanceStatus.Pending
        };
        _db.Service.SaveIncome(income);
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);

        income.Id = Convert.ToInt32(_db.Service.SearchIncomes(AllDates(), "", "Data").Rows[0]["Id"]);
        income.Status = FinanceStatus.Received;
        _db.Service.SaveIncome(income);
        Assert.Equal(1123.45m, _db.Service.GetDashboardSummary().CurrentBalance);
        _db.Service.SaveIncome(income);
        Assert.Equal(1123.45m, _db.Service.GetDashboardSummary().CurrentBalance);
        _db.Service.DeleteIncome(income.Id);
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void OverdueExpenseCanBeRescheduledAndPaid()
    {
        var expense = new ExpenseInput
        {
            Description = "Boleto", Amount = 50.25m, CategoryId = _db.ExpenseCategoryId,
            AccountId = _db.AccountId, DueDate = DateTime.Today.AddDays(-1)
        };
        _db.Service.SaveExpense(expense);
        var row = _db.Service.SearchExpenses(AllDates(), "", "Data").Rows[0];
        Assert.Equal(FinanceStatus.Overdue, row["Status"]);
        expense.Id = Convert.ToInt32(row["Id"]);
        expense.DueDate = DateTime.Today.AddDays(2);
        expense.Status = FinanceStatus.Overdue;
        _db.Service.SaveExpense(expense);
        Assert.Equal(FinanceStatus.Pending, _db.Service.SearchExpenses(AllDates(), "", "Data").Rows[0]["Status"]);
        expense.Status = FinanceStatus.Paid;
        _db.Service.SaveExpense(expense);
        Assert.Equal(949.75m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Theory]
    [InlineData("0.10", 6)]
    [InlineData("100", 3)]
    [InlineData("1200", 6)]
    public void InstallmentsHavePositiveValuesAndPreserveTotal(string total, int count)
    {
        var value = decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture);
        _db.Service.CreateInstallments(new InstallmentInput
        {
            Description = "Compra", TotalAmount = value, InstallmentCount = count,
            FirstDueDate = DateTime.Today, CategoryId = _db.ExpenseCategoryId, AccountId = _db.AccountId
        });
        var groupId = Convert.ToInt32(_db.Service.SearchInstallmentGroups().Rows[0]["Id"]);
        var rows = _db.Service.GetInstallmentDetails(groupId).Rows.Cast<DataRow>().ToArray();
        Assert.Equal(count, rows.Length);
        Assert.All(rows, row => Assert.True(Amount(row, "Valor") >= 0.01m));
        Assert.Equal(value, rows.Sum(row => Amount(row, "Valor")));
    }

    [Fact]
    public void CardInstallmentsUseConsecutiveInvoiceMonthsAndReserveFullLimit()
    {
        var cardId = _db.CreateCard();
        _db.Service.CreateInstallments(new InstallmentInput
        {
            Description = "Compra cartao", TotalAmount = 100, InstallmentCount = 3,
            FirstDueDate = new DateTime(2026, 1, 26), CategoryId = _db.ExpenseCategoryId, CreditCardId = cardId
        });
        var purchases = _db.Service.SearchCardPurchases(AllDates(), "");
        Assert.Equal(new[] { "2026-02", "2026-03", "2026-04" }, purchases.Rows.Cast<DataRow>().Select(row => (string)row["Fatura"]).Order().ToArray());
        Assert.Equal(900m, Amount(_db.Service.SearchCreditCards("").Rows[0], "LimiteDisponivel"));
    }

    [Fact]
    public void RecurrenceIncludesLastDayOfHorizonAndDoesNotDuplicate()
    {
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _db.Service.SaveRecurringExpense(new RecurringExpenseInput
        {
            Description = "Internet", Amount = 99.90m, DayOfMonth = 31, CategoryId = _db.ExpenseCategoryId,
            AccountId = _db.AccountId, StartDate = start
        });
        Assert.Equal(2, _db.Service.GenerateRecurringExpenses(1));
        Assert.Equal(0, _db.Service.GenerateRecurringExpenses(1));
        var nextMonth = start.AddMonths(1);
        var lastDay = nextMonth.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
        Assert.Contains(_db.Service.SearchExpenses(AllDates(), "", "Data").Rows.Cast<DataRow>(), row => (string)row["Vencimento"] == lastDay);
    }

    [Fact]
    public void InvoicePaymentDebitsAccountOnceAndReleasesLimit()
    {
        var cardId = _db.CreateCard();
        var purchase = Purchase(cardId, 250);
        _db.Service.SaveCardPurchase(purchase);
        var month = (string)_db.Service.SearchCardPurchases(AllDates(), "").Rows[0]["Fatura"];
        _db.Service.CloseInvoice(cardId, month);
        var id = Convert.ToInt32(_db.Service.SearchInvoices(month).Rows[0]["Id"]);
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveCardPurchase(purchase));
        _db.Service.PayInvoice(id, _db.AccountId);
        Assert.Equal(750m, _db.Service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(1000m, Amount(_db.Service.SearchCreditCards("").Rows[0], "LimiteDisponivel"));
        Assert.Throws<InvalidOperationException>(() => _db.Service.PayInvoice(id, _db.AccountId));
        Assert.Throws<InvalidOperationException>(() => _db.Service.CloseInvoice(cardId, month));
        Assert.Equal(750m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void MovingPurchaseToAnotherCardDoesNotReuseOldCardsLimit()
    {
        var firstCard = _db.CreateCard("Primeiro");
        var secondCard = _db.CreateCard("Segundo", 100);
        var purchase = Purchase(firstCard, 150);
        _db.Service.SaveCardPurchase(purchase);
        purchase.Id = Convert.ToInt32(_db.Service.SearchCardPurchases(AllDates(), "").Rows[0]["Id"]);
        purchase.CreditCardId = secondCard;
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveCardPurchase(purchase));
    }

    [Fact]
    public void LinkedAccountCannotBeDeleted()
    {
        _db.Service.SaveIncome(new IncomeInput
        {
            Description = "Receita", Amount = 100, CategoryId = _db.IncomeCategoryId, AccountId = _db.AccountId
        });
        Assert.Throws<InvalidOperationException>(() => _db.Service.DeleteAccount(_db.AccountId));
    }

    [Fact]
    public void WrongCategoryTypeIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveIncome(new IncomeInput
        {
            Description = "Receita", Amount = 100, CategoryId = _db.ExpenseCategoryId, AccountId = _db.AccountId
        }));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("10/2026")]
    [InlineData("")]
    public void InvalidBudgetMonthIsRejected(string month)
    {
        Assert.Throws<InvalidOperationException>(() => _db.Service.SaveBudget(new BudgetInput
        {
            Month = month, LimitAmount = 100, CategoryId = _db.ExpenseCategoryId
        }));
    }

    [Fact]
    public void BudgetAndGoalProgressAreCalculated()
    {
        _db.Service.SaveBudget(new BudgetInput { Month = DateTime.Today.ToString("yyyy-MM"), CategoryId = _db.ExpenseCategoryId, LimitAmount = 100 });
        _db.Service.SaveExpense(new ExpenseInput { Description = "Mercado", Amount = 90, CategoryId = _db.ExpenseCategoryId, AccountId = _db.AccountId });
        Assert.Equal("Alerta 90%", _db.Service.SearchBudgets("").Rows[0]["Alerta"]);
        _db.Service.SaveGoal(new GoalInput { Name = "Reserva", TargetAmount = 100 });
        var id = Convert.ToInt32(_db.Service.SearchGoals("").Rows[0]["Id"]);
        _db.Service.AddGoalContribution(id, 100, DateTime.Today, "Aporte");
        Assert.Equal(FinanceStatus.Finished, _db.Service.SearchGoals("").Rows[0]["Status"]);
    }

    private CardPurchaseInput Purchase(int cardId, decimal amount) => new()
    {
        Description = "Compra", Amount = amount, CreditCardId = cardId,
        CategoryId = _db.ExpenseCategoryId, PurchaseDate = new DateTime(2026, 2, 10)
    };

    public void Dispose() => _db.Dispose();
}
