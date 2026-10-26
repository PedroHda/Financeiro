namespace FinanceiroApp.Models;

public sealed class TransferInput
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Notes { get; set; } = "";
}

public sealed record ForecastMonth(DateTime Month, decimal OpeningBalance, decimal Income, decimal Outgoing, decimal ClosingBalance);
public sealed record ExpenseSplit(int CategoryId, decimal Amount);
public sealed record AttachmentInfo(int Id, string Name, int Size);
public sealed class ImportEntry
{
    public bool Selected { get; set; } = true;
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string SourceId { get; set; } = "";
    public int CategoryId { get; set; }
    public bool Duplicate { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Type => Amount >= 0 ? FinanceTypes.Income : FinanceTypes.Expense;
}
