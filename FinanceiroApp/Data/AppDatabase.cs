using System.Data;
using Microsoft.Data.Sqlite;

namespace FinanceiroApp.Data;

public sealed class AppDatabase
{
    public string DatabasePath { get; }

    private string ConnectionString
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                ForeignKeys = true,
                Pooling = false
            };
            return builder.ToString();
        }
    }

    public AppDatabase(string? databasePath = null)
    {
        DatabasePath = Path.GetFullPath(databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanceiroApp", "financeiro.db"));
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    public void Initialize()
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();

        EnsureDefaults(connection);
    }

    internal static void ValidateBackup(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O backup esta corrompido.");
        }

        using var expected = new SqliteConnection("Data Source=:memory:");
        expected.Open();
        using var schema = expected.CreateCommand();
        schema.CommandText = SchemaSql;
        schema.ExecuteNonQuery();
        schema.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        using var tables = schema.ExecuteReader();
        while (tables.Read())
        {
            var table = tables.GetString(0);
            using var columns = expected.CreateCommand();
            columns.CommandText = $"PRAGMA table_info([{table}]);";
            using var reader = columns.ExecuteReader();
            var names = new List<string>();
            while (reader.Read()) names.Add($"[{reader.GetString(1)}]");

            // Compile every required column before allowing a restore.
            command.CommandText = $"SELECT {string.Join(", ", names)} FROM [{table}] LIMIT 0;";
            using var actual = command.ExecuteReader();
        }

        command.CommandText = "PRAGMA foreign_key_check;";
        using var violations = command.ExecuteReader();
        if (violations.Read())
        {
            throw new InvalidOperationException("O backup possui registros vinculados invalidos.");
        }
    }

    private static void EnsureDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Settings;";
        var settingsCount = Convert.ToInt32(command.ExecuteScalar());
        if (settingsCount == 0)
        {
            Execute(connection, "INSERT INTO Settings(Key, Value) VALUES('Currency', 'BRL'), ('DateFormat', 'dd/MM/yyyy'), ('Theme', 'Claro');");
        }

        command.CommandText = "SELECT COUNT(*) FROM Categories;";
        var categoryCount = Convert.ToInt32(command.ExecuteScalar());
        if (categoryCount > 0)
        {
            return;
        }

        var categories = new[]
        {
            ("Salario", "Receita", "#2F855A", "cash"),
            ("Freelance", "Receita", "#3182CE", "briefcase"),
            ("Investimentos", "Receita", "#805AD5", "chart"),
            ("Alimentacao", "Despesa", "#E53E3E", "meal"),
            ("Transporte", "Despesa", "#DD6B20", "car"),
            ("Moradia", "Despesa", "#718096", "home"),
            ("Saude", "Despesa", "#D53F8C", "heart"),
            ("Educacao", "Despesa", "#2B6CB0", "book"),
            ("Lazer", "Despesa", "#38A169", "smile"),
            ("Cartao de credito", "Despesa", "#6B46C1", "card"),
            ("Assinaturas", "Despesa", "#319795", "repeat"),
            ("Outros", "Despesa", "#4A5568", "more")
        };

        foreach (var (name, type, color, icon) in categories)
        {
            Execute(
                connection,
                "INSERT INTO Categories(Name, Type, ColorHex, Icon, IsActive) VALUES($name, $type, $color, $icon, 1);",
                new Dictionary<string, object?>
                {
                    ["$name"] = name,
                    ["$type"] = type,
                    ["$color"] = color,
                    ["$icon"] = icon
                });
        }
    }

    private static void Execute(SqliteConnection connection, string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Repository.AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS Accounts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Type TEXT NOT NULL,
            InitialBalance REAL NOT NULL DEFAULT 0,
            CurrentBalance REAL NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1,
            Notes TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Categories (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Type TEXT NOT NULL CHECK(Type IN ('Receita', 'Despesa')),
            ColorHex TEXT NOT NULL DEFAULT '#4F6BED',
            Icon TEXT NOT NULL DEFAULT '',
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Incomes (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Description TEXT NOT NULL,
            Amount REAL NOT NULL,
            ReceivedDate TEXT NOT NULL,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            AccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
            PaymentMethod TEXT NOT NULL DEFAULT '',
            Notes TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL CHECK(Status IN ('Recebido', 'Pendente')),
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Expenses (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Description TEXT NOT NULL,
            Amount REAL NOT NULL,
            ExpenseDate TEXT NOT NULL,
            DueDate TEXT NOT NULL,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            AccountId INTEGER NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
            CreditCardId INTEGER NULL REFERENCES CreditCards(Id) ON DELETE RESTRICT,
            PaymentMethod TEXT NOT NULL DEFAULT '',
            Notes TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL CHECK(Status IN ('Pago', 'Pendente', 'Vencido')),
            RecurringExpenseId INTEGER NULL REFERENCES RecurringExpenses(Id) ON DELETE SET NULL,
            InstallmentGroupId INTEGER NULL REFERENCES InstallmentGroups(Id) ON DELETE SET NULL,
            InstallmentNumber INTEGER NOT NULL DEFAULT 0,
            InstallmentTotal INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS RecurringExpenses (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Description TEXT NOT NULL,
            Amount REAL NOT NULL,
            DayOfMonth INTEGER NOT NULL,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            AccountId INTEGER NOT NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
            PaymentMethod TEXT NOT NULL DEFAULT '',
            Notes TEXT NOT NULL DEFAULT '',
            StartDate TEXT NOT NULL,
            EndDate TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS InstallmentGroups (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Description TEXT NOT NULL,
            TotalAmount REAL NOT NULL,
            InstallmentCount INTEGER NOT NULL,
            FirstDueDate TEXT NOT NULL,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            AccountId INTEGER NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
            CreditCardId INTEGER NULL REFERENCES CreditCards(Id) ON DELETE RESTRICT,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS CreditCards (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Institution TEXT NOT NULL DEFAULT '',
            TotalLimit REAL NOT NULL,
            AvailableLimit REAL NOT NULL,
            ClosingDay INTEGER NOT NULL,
            DueDay INTEGER NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS CardPurchases (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Description TEXT NOT NULL,
            Amount REAL NOT NULL,
            PurchaseDate TEXT NOT NULL,
            CreditCardId INTEGER NOT NULL REFERENCES CreditCards(Id) ON DELETE RESTRICT,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            InstallmentGroupId INTEGER NULL REFERENCES InstallmentGroups(Id) ON DELETE SET NULL,
            InstallmentNumber INTEGER NOT NULL DEFAULT 1,
            InstallmentTotal INTEGER NOT NULL DEFAULT 1,
            InvoiceMonth TEXT NOT NULL,
            Notes TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL CHECK(Status IN ('Aberta', 'Fechada', 'Paga')),
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Invoices (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CreditCardId INTEGER NOT NULL REFERENCES CreditCards(Id) ON DELETE RESTRICT,
            InvoiceMonth TEXT NOT NULL,
            ClosingDate TEXT NOT NULL,
            DueDate TEXT NOT NULL,
            Status TEXT NOT NULL CHECK(Status IN ('Aberta', 'Fechada', 'Paga')),
            TotalAmount REAL NOT NULL DEFAULT 0,
            PaidAccountId INTEGER NULL REFERENCES Accounts(Id) ON DELETE RESTRICT,
            PaidAt TEXT NULL,
            UNIQUE(CreditCardId, InvoiceMonth)
        );

        CREATE TABLE IF NOT EXISTS Budgets (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Month TEXT NOT NULL,
            CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            LimitAmount REAL NOT NULL,
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE(Month, CategoryId)
        );

        CREATE TABLE IF NOT EXISTS Goals (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            TargetAmount REAL NOT NULL,
            CurrentAmount REAL NOT NULL DEFAULT 0,
            TargetDate TEXT NOT NULL,
            Description TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL DEFAULT 'Ativa',
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS GoalContributions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            GoalId INTEGER NOT NULL REFERENCES Goals(Id) ON DELETE CASCADE,
            Amount REAL NOT NULL,
            ContributionDate TEXT NOT NULL,
            Notes TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Incomes_Date ON Incomes(ReceivedDate);
        CREATE INDEX IF NOT EXISTS IX_Expenses_DueDate ON Expenses(DueDate);
        CREATE INDEX IF NOT EXISTS IX_Expenses_Status ON Expenses(Status);
        CREATE INDEX IF NOT EXISTS IX_CardPurchases_Month ON CardPurchases(InvoiceMonth);
        CREATE INDEX IF NOT EXISTS IX_Budgets_Month ON Budgets(Month);
        """;
}

public sealed class Repository
{
    private readonly AppDatabase _database;

    public Repository(AppDatabase database)
    {
        _database = database;
    }

    public DataTable Query(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);

        using var reader = command.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public object? Scalar(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return command.ExecuteScalar();
    }

    public int Execute(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return command.ExecuteNonQuery();
    }

    public long Insert(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"{sql}; SELECT last_insert_rowid();";
        AddParameters(command, parameters);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public void Transaction(Action<SqliteConnection, SqliteTransaction> work)
    {
        using var connection = _database.CreateConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            work(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static int Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return command.ExecuteNonQuery();
    }

    public static long Insert(SqliteConnection connection, SqliteTransaction transaction, string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"{sql}; SELECT last_insert_rowid();";
        AddParameters(command, parameters);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public static object? Scalar(SqliteConnection connection, SqliteTransaction transaction, string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return command.ExecuteScalar();
    }

    internal static void AddParameters(SqliteCommand command, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var pair in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = pair.Key;
            parameter.Value = ToDbValue(pair.Value);
            command.Parameters.Add(parameter);
        }
    }

    private static object ToDbValue(object? value)
    {
        return value switch
        {
            null => DBNull.Value,
            DateTime date => date.ToString("yyyy-MM-dd"),
            bool flag => flag ? 1 : 0,
            decimal money => (double)money,
            _ => value
        };
    }
}
