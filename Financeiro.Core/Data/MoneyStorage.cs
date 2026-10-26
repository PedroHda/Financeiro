using System.Globalization;

namespace FinanceiroApp.Data;

internal static class MoneyStorage
{
    internal static readonly IReadOnlyDictionary<string, string[]> Columns = new Dictionary<string, string[]>
    {
        ["Accounts"] = ["InitialBalance", "CurrentBalance"],
        ["Incomes"] = ["Amount"],
        ["Expenses"] = ["Amount"],
        ["RecurringExpenses"] = ["Amount"],
        ["InstallmentGroups"] = ["TotalAmount"],
        ["CreditCards"] = ["TotalLimit", "AvailableLimit"],
        ["CardPurchases"] = ["Amount"],
        ["Invoices"] = ["TotalAmount"],
        ["Budgets"] = ["LimitAmount"],
        ["Goals"] = ["TargetAmount", "CurrentAmount"],
        ["GoalContributions"] = ["Amount"]
    };

    // The repository boundary exposes currency as decimal; SQL always operates on cents.
    internal static readonly HashSet<string> ResultColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Amount", "InitialBalance", "CurrentBalance", "TotalAmount", "TotalLimit", "AvailableLimit",
        "LimitAmount", "TargetAmount", "CurrentAmount", "Valor", "Total", "Saldo", "Receitas", "Despesas",
        "SaldoInicial", "SaldoAtual", "ValorTotal", "LimiteTotal", "LimiteDisponivel", "Limite", "Gasto",
        "ValorObjetivo", "ValorAtual", "Entradas", "Saidas", "SaldoPrevisto", "SaldoInformado", "Diferenca"
    };

    internal static long ToCents(decimal value)
    {
        if (decimal.Round(value, 2) != value)
            throw new InvalidOperationException("Valores monetarios devem ter no maximo duas casas decimais.");
        try { return checked((long)(value * 100m)); }
        catch (OverflowException ex) { throw new InvalidOperationException("Valor monetario fora do limite permitido.", ex); }
    }

    internal static decimal FromCents(object value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture) / 100m;
}
