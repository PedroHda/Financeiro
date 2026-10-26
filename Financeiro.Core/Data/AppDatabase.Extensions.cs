using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Data;

public sealed partial class AppDatabase
{
    private static void InitializeExtensions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Transfers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FromAccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
                ToAccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
                Amount INTEGER NOT NULL CHECK(Amount > 0),
                TransferDate TEXT NOT NULL, Notes TEXT NOT NULL DEFAULT '',
                CHECK(FromAccountId <> ToAccountId)
            );
            CREATE TABLE IF NOT EXISTS Reconciliations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
                BalanceDate TEXT NOT NULL, BankBalance INTEGER NOT NULL, BookBalance INTEGER NOT NULL,
                Notes TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS ImportedTransactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
                Fingerprint TEXT NOT NULL, EntryKind TEXT NOT NULL, EntryId INTEGER NOT NULL,
                UNIQUE(AccountId, Fingerprint)
            );
            CREATE TABLE IF NOT EXISTS CategoryRules (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MatchText TEXT NOT NULL, Type TEXT NOT NULL CHECK(Type IN ('Receita','Despesa')),
                CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
                UNIQUE(MatchText, Type)
            );
            CREATE TABLE IF NOT EXISTS ExpenseSplits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExpenseId INTEGER NOT NULL REFERENCES Expenses(Id) ON DELETE CASCADE,
                CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
                Amount INTEGER NOT NULL CHECK(Amount > 0)
            );
            CREATE TABLE IF NOT EXISTS Attachments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EntityType TEXT NOT NULL CHECK(EntityType IN ('income','expense')),
                EntityId INTEGER NOT NULL, Name TEXT NOT NULL, Data BLOB NOT NULL,
                CHECK(length(Data) <= 5242880)
            );
            CREATE TABLE IF NOT EXISTS SyncKeys (
                TableName TEXT NOT NULL, LocalId INTEGER NOT NULL, SyncId TEXT NOT NULL UNIQUE,
                ServerRevision INTEGER NOT NULL DEFAULT 0, LastPayload TEXT NULL, Deleted INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY(TableName, LocalId)
            );
            CREATE TABLE IF NOT EXISTS SyncState (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_Transfers_Date ON Transfers(TransferDate);
            CREATE INDEX IF NOT EXISTS IX_Reconciliations_Account ON Reconciliations(AccountId, BalanceDate);
            CREATE INDEX IF NOT EXISTS IX_ExpenseSplits_Expense ON ExpenseSplits(ExpenseId);
            CREATE INDEX IF NOT EXISTS IX_Attachments_Entity ON Attachments(EntityType, EntityId);
            CREATE TRIGGER IF NOT EXISTS TR_Incomes_Attachments AFTER DELETE ON Incomes
              BEGIN DELETE FROM Attachments WHERE EntityType='income' AND EntityId=OLD.Id; END;
            CREATE TRIGGER IF NOT EXISTS TR_Expenses_Attachments AFTER DELETE ON Expenses
              BEGIN DELETE FROM Attachments WHERE EntityType='expense' AND EntityId=OLD.Id; END;
            CREATE VIEW IF NOT EXISTS ExpenseAllocations AS
              SELECT e.Id AS ExpenseId,e.Amount,e.CategoryId,e.DueDate,e.ExpenseDate,e.AccountId,e.CreditCardId,e.Status
                FROM Expenses e WHERE NOT EXISTS(SELECT 1 FROM ExpenseSplits s WHERE s.ExpenseId=e.Id)
              UNION ALL
              SELECT e.Id,s.Amount,s.CategoryId,e.DueDate,e.ExpenseDate,e.AccountId,e.CreditCardId,e.Status
                FROM Expenses e JOIN ExpenseSplits s ON s.ExpenseId=e.Id;
            """;
        command.ExecuteNonQuery();
    }
}
