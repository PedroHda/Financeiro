namespace Financeiro.Sync.Contracts;

public static class SyncProtocol
{
    public const int Version = 1;
    public const int PageSize = 100;
    public const int MaxChanges = 100;
    public const int MaxPayloadBytes = 8 * 1024 * 1024;
    public static readonly string[] Tables =
    [
        "Accounts", "Categories", "CreditCards", "RecurringExpenses", "InstallmentGroups", "Incomes", "Expenses",
        "CardPurchases", "Invoices", "Budgets", "Goals", "GoalContributions", "Transfers", "Reconciliations",
        "CategoryRules", "ImportedTransactions", "Attachments"
    ];
}

public sealed record SyncChange(Guid Id, string Table, long BaseRevision, string? Payload, bool Deleted);
public sealed record SyncRecord(Guid Id, string Table, long Revision, string? Payload, bool Deleted);
public sealed record SyncPushRequest(int Protocol, long WorkspaceRevision, IReadOnlyList<SyncChange> Changes);
public sealed record SyncPullPage(long Cursor, long WorkspaceRevision, bool HasMore, IReadOnlyList<SyncRecord> Records);
public sealed record SyncPushResult(long WorkspaceRevision, IReadOnlyList<SyncRecord> Records);
public sealed record SyncConflict(Guid Id, string Table, string? LocalPayload, SyncRecord Remote);
public sealed record SyncResolution(Guid Id, long RemoteRevision, bool KeepLocal);

public sealed class SyncConflictException(IReadOnlyList<SyncConflict> conflicts) : InvalidOperationException("Ha alteracoes concorrentes. Escolha quais dados manter antes de continuar.")
{
    public IReadOnlyList<SyncConflict> Conflicts { get; } = conflicts;
}
