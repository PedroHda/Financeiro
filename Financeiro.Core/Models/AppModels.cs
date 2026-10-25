namespace FinanceiroApp.Models;

public static class FinanceStatus
{
    public const string Received = "Recebido";
    public const string Pending = "Pendente";
    public const string Paid = "Pago";
    public const string Overdue = "Vencido";
    public const string Open = "Aberta";
    public const string Closed = "Fechada";
    public const string Finished = "Concluida";
    public const string Active = "Ativa";
}

public static class FinanceTypes
{
    public const string Income = "Receita";
    public const string Expense = "Despesa";
}

public sealed record LookupItem(int Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record DashboardSummary(
    decimal CurrentBalance,
    decimal MonthlyIncome,
    decimal MonthlyExpense,
    decimal MonthlyBalance,
    decimal CreditCardTotal,
    int DueSoonCount,
    int OverdueCount,
    bool ExpensesAboveIncome);

public sealed class AccountInput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal InitialBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = "";
}

public sealed class CategoryInput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = FinanceTypes.Expense;
    public string ColorHex { get; set; } = "#4F6BED";
    public string Icon { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class IncomeInput
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; } = DateTime.Today;
    public int CategoryId { get; set; }
    public int AccountId { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = FinanceStatus.Received;
}

public sealed class ExpenseInput
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today;
    public int CategoryId { get; set; }
    public int? AccountId { get; set; }
    public int? CreditCardId { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = FinanceStatus.Pending;
    public int? RecurringExpenseId { get; set; }
    public int? InstallmentGroupId { get; set; }
    public int InstallmentNumber { get; set; }
    public int InstallmentTotal { get; set; }
}

public sealed class RecurringExpenseInput
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public int CategoryId { get; set; }
    public int AccountId { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class InstallmentInput
{
    public string Description { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public DateTime FirstDueDate { get; set; } = DateTime.Today;
    public int CategoryId { get; set; }
    public int? AccountId { get; set; }
    public int? CreditCardId { get; set; }
    public string PaymentMethod { get; set; } = "Parcelado";
    public string Notes { get; set; } = "";
}

public sealed class CreditCardInput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Institution { get; set; } = "";
    public decimal TotalLimit { get; set; }
    public int ClosingDay { get; set; } = 1;
    public int DueDay { get; set; } = 10;
    public bool IsActive { get; set; } = true;
}

public sealed class CardPurchaseInput
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public int CreditCardId { get; set; }
    public int CategoryId { get; set; }
    public string Notes { get; set; } = "";
    public int InstallmentCount { get; set; } = 1;
}

public sealed class BudgetInput
{
    public int Id { get; set; }
    public string Month { get; set; } = DateTime.Today.ToString("yyyy-MM");
    public int CategoryId { get; set; }
    public decimal LimitAmount { get; set; }
}

public sealed class GoalInput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime TargetDate { get; set; } = DateTime.Today.AddMonths(6);
    public string Description { get; set; } = "";
    public string Status { get; set; } = FinanceStatus.Active;
}

public sealed class ReportFilter
{
    public DateTime StartDate { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public int? CategoryId { get; set; }
    public int? AccountId { get; set; }
    public int? CreditCardId { get; set; }
    public string? Status { get; set; }
    public string? TransactionType { get; set; }
}
