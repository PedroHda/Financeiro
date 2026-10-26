using System.Data;
using System.Globalization;
using System.Text;
using FinanceiroApp.Data;
using FinanceiroApp.Models;

namespace FinanceiroApp.Services;

public sealed partial class FinanceService
{
    private readonly AppDatabase _database;
    private readonly Repository _repository;

    public FinanceService(string? databasePath = null)
    {
        _database = new AppDatabase(databasePath);
        _repository = new Repository(_database);
    }

    public string DatabasePath => _database.DatabasePath;

    public void Initialize()
    {
        _database.Initialize();
        RefreshDerivedData();
    }

    public void RefreshDerivedData()
    {
        RefreshOverdueExpenses();
        RecalculateAccountBalances();
        RecalculateCardLimits();
    }

    public DashboardSummary GetDashboardSummary(DateTime? referenceMonth = null)
    {
        RefreshDerivedData();
        var reference = referenceMonth ?? DateTime.Today;
        var start = new DateTime(reference.Year, reference.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var month = start.ToString("yyyy-MM");

        var currentBalance = Money("SELECT COALESCE(SUM(CurrentBalance), 0) FROM Accounts WHERE IsActive = 1;");
        var monthlyIncome = Money(
            "SELECT COALESCE(SUM(Amount), 0) FROM Incomes WHERE Status = 'Recebido' AND ReceivedDate BETWEEN $start AND $end;",
            P("$start", start, "$end", end));
        var monthlyExpense = Money(
            """
            SELECT COALESCE((SELECT SUM(Amount) FROM Expenses WHERE DueDate BETWEEN $start AND $end), 0)
                 + COALESCE((SELECT SUM(Amount) FROM CardPurchases WHERE InvoiceMonth = $month), 0);
            """,
            P("$start", start, "$end", end, "$month", month));
        var creditCardTotal = Money("SELECT COALESCE(SUM(TotalLimit - AvailableLimit), 0) FROM CreditCards WHERE IsActive = 1;");
        var dueSoonCount = Number(
            """
            SELECT
                (SELECT COUNT(*) FROM Expenses WHERE Status = 'Pendente' AND DueDate BETWEEN $today AND $soon)
              + (SELECT COUNT(*) FROM Invoices WHERE Status = 'Fechada' AND DueDate BETWEEN $today AND $soon);
            """,
            P("$today", DateTime.Today, "$soon", DateTime.Today.AddDays(7)));
        var overdueCount = Number(
            """
            SELECT
                (SELECT COUNT(*) FROM Expenses WHERE Status = 'Vencido')
              + (SELECT COUNT(*) FROM Invoices WHERE Status = 'Fechada' AND DueDate < $today);
            """,
            P("$today", DateTime.Today));

        return new DashboardSummary(
            currentBalance,
            monthlyIncome,
            monthlyExpense,
            monthlyIncome - monthlyExpense,
            creditCardTotal,
            dueSoonCount,
            overdueCount,
            monthlyExpense > monthlyIncome);
    }

    public DataTable GetDashboardCategoryExpenses(DateTime? referenceMonth = null)
    {
        var month = (referenceMonth ?? DateTime.Today).ToString("yyyy-MM");
        return _repository.Query(
            """
            SELECT Categoria, SUM(Total) AS Total
            FROM (
                SELECT c.Name AS Categoria, e.Amount AS Total
                FROM ExpenseAllocations e
                JOIN Categories c ON c.Id = e.CategoryId
                WHERE substr(e.DueDate, 1, 7) = $month
                UNION ALL
                SELECT c.Name AS Categoria, cp.Amount AS Total
                FROM CardPurchases cp
                JOIN Categories c ON c.Id = cp.CategoryId
                WHERE cp.InvoiceMonth = $month
            )
            GROUP BY Categoria
            ORDER BY Total DESC;
            """,
            P("$month", month));
    }

    public DataTable GetDashboardMonthlyEvolution()
    {
        return _repository.Query(
            """
            WITH months AS (
                SELECT substr(ReceivedDate, 1, 7) AS Month FROM Incomes
                UNION
                SELECT substr(DueDate, 1, 7) AS Month FROM Expenses
                UNION
                SELECT InvoiceMonth AS Month FROM CardPurchases
            )
            SELECT m.Month AS Mes,
                   COALESCE((SELECT SUM(Amount) FROM Incomes WHERE Status = 'Recebido' AND substr(ReceivedDate, 1, 7) = m.Month), 0) AS Receitas,
                   COALESCE((SELECT SUM(Amount) FROM Expenses WHERE substr(DueDate, 1, 7) = m.Month), 0)
                         + COALESCE((SELECT SUM(Amount) FROM CardPurchases WHERE InvoiceMonth = m.Month), 0) AS Despesas,
                   COALESCE((SELECT SUM(Amount) FROM Incomes WHERE Status = 'Recebido' AND substr(ReceivedDate, 1, 7) = m.Month), 0)
                         - COALESCE((SELECT SUM(Amount) FROM Expenses WHERE substr(DueDate, 1, 7) = m.Month), 0)
                         - COALESCE((SELECT SUM(Amount) FROM CardPurchases WHERE InvoiceMonth = m.Month), 0) AS Saldo
            FROM months m
            ORDER BY m.Month DESC
            LIMIT 12;
            """);
    }

    public DataTable GetDueSoon()
    {
        return _repository.Query(
            """
            SELECT 'Despesa' AS Tipo, Description AS Descricao, Amount AS Valor, DueDate AS Vencimento, Status
            FROM Expenses
            WHERE Status = 'Pendente' AND DueDate BETWEEN $today AND $soon
            UNION ALL
            SELECT 'Fatura' AS Tipo, cc.Name || ' - ' || i.InvoiceMonth AS Descricao, i.TotalAmount AS Valor, i.DueDate AS Vencimento, i.Status
            FROM Invoices i
            JOIN CreditCards cc ON cc.Id = i.CreditCardId
            WHERE i.Status = 'Fechada' AND i.DueDate BETWEEN $today AND $soon
            ORDER BY Vencimento;
            """,
            P("$today", DateTime.Today, "$soon", DateTime.Today.AddDays(7)));
    }

    public DataTable GetOverdue()
    {
        return _repository.Query(
            """
            SELECT 'Despesa' AS Tipo, Description AS Descricao, Amount AS Valor, DueDate AS Vencimento, Status
            FROM Expenses
            WHERE Status = 'Vencido'
            UNION ALL
            SELECT 'Fatura' AS Tipo, cc.Name || ' - ' || i.InvoiceMonth AS Descricao, i.TotalAmount AS Valor, i.DueDate AS Vencimento, i.Status
            FROM Invoices i
            JOIN CreditCards cc ON cc.Id = i.CreditCardId
            WHERE i.Status = 'Fechada' AND i.DueDate < $today
            ORDER BY Vencimento;
            """,
            P("$today", DateTime.Today));
    }

    public DataTable SearchAccounts(string search)
    {
        return _repository.Query(
            """
            SELECT Id, Name AS Nome, Type AS Tipo, InitialBalance AS SaldoInicial,
                   CurrentBalance AS SaldoAtual,
                   CASE IsActive WHEN 1 THEN 'Ativo' ELSE 'Inativo' END AS Status,
                   Notes AS Observacoes
            FROM Accounts
            WHERE $search = '' OR Name LIKE '%' || $search || '%' OR Type LIKE '%' || $search || '%'
            ORDER BY Name;
            """,
            P("$search", search.Trim()));
    }

    public void SaveAccount(AccountInput input)
    {
        Require(input.Name, "Informe o nome da conta.");
        Require(input.Type, "Informe o tipo da conta.");

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Accounts(Name, Type, InitialBalance, CurrentBalance, IsActive, Notes)
                VALUES($name, $type, $initial, $initial, $active, $notes);
                """,
                P("$name", input.Name.Trim(), "$type", input.Type.Trim(), "$initial", input.InitialBalance, "$active", input.IsActive, "$notes", input.Notes.Trim()));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE Accounts
                SET Name = $name, Type = $type, InitialBalance = $initial, IsActive = $active, Notes = $notes
                WHERE Id = $id;
                """,
                P("$name", input.Name.Trim(), "$type", input.Type.Trim(), "$initial", input.InitialBalance, "$active", input.IsActive, "$notes", input.Notes.Trim(), "$id", input.Id));
        }

        RecalculateAccountBalances();
    }

    public void DeleteAccount(int id)
    {
        EnsureNoLinks("Incomes", "AccountId", id, "A conta possui receitas vinculadas.");
        EnsureNoLinks("Expenses", "AccountId", id, "A conta possui despesas vinculadas.");
        EnsureNoLinks("RecurringExpenses", "AccountId", id, "A conta possui recorrencias vinculadas.");
        EnsureNoLinks("Invoices", "PaidAccountId", id, "A conta possui faturas pagas vinculadas.");
        EnsureNoLinks("Transfers", "FromAccountId", id, "A conta possui transferencias vinculadas.");
        EnsureNoLinks("Transfers", "ToAccountId", id, "A conta possui transferencias vinculadas.");
        EnsureNoLinks("Reconciliations", "AccountId", id, "A conta possui conciliacoes vinculadas.");
        _repository.Execute("DELETE FROM Accounts WHERE Id = $id;", P("$id", id));
    }

    public DataTable SearchCategories(string search, string type = "")
    {
        return _repository.Query(
            """
            SELECT Id, Name AS Nome, Type AS Tipo, ColorHex AS Cor, Icon AS Icone,
                   CASE IsActive WHEN 1 THEN 'Ativo' ELSE 'Inativo' END AS Status
            FROM Categories
            WHERE ($search = '' OR Name LIKE '%' || $search || '%')
              AND ($type = '' OR Type = $type)
            ORDER BY Type, Name;
            """,
            P("$search", search.Trim(), "$type", type));
    }

    public void SaveCategory(CategoryInput input)
    {
        Require(input.Name, "Informe o nome da categoria.");
        Require(input.Type, "Informe o tipo da categoria.");
        Require(input.ColorHex, "Informe uma cor para a categoria.");

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Categories(Name, Type, ColorHex, Icon, IsActive)
                VALUES($name, $type, $color, $icon, $active);
                """,
                P("$name", input.Name.Trim(), "$type", input.Type, "$color", input.ColorHex.Trim(), "$icon", input.Icon.Trim(), "$active", input.IsActive));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE Categories
                SET Name = $name, Type = $type, ColorHex = $color, Icon = $icon, IsActive = $active
                WHERE Id = $id;
                """,
                P("$name", input.Name.Trim(), "$type", input.Type, "$color", input.ColorHex.Trim(), "$icon", input.Icon.Trim(), "$active", input.IsActive, "$id", input.Id));
        }
    }

    public void DeleteCategory(int id)
    {
        EnsureNoLinks("Incomes", "CategoryId", id, "A categoria possui receitas vinculadas.");
        EnsureNoLinks("Expenses", "CategoryId", id, "A categoria possui despesas vinculadas.");
        EnsureNoLinks("RecurringExpenses", "CategoryId", id, "A categoria possui recorrencias vinculadas.");
        EnsureNoLinks("CardPurchases", "CategoryId", id, "A categoria possui compras no cartao vinculadas.");
        EnsureNoLinks("Budgets", "CategoryId", id, "A categoria possui orcamentos vinculados.");
        EnsureNoLinks("ExpenseSplits", "CategoryId", id, "A categoria possui rateios vinculados.");
        EnsureNoLinks("CategoryRules", "CategoryId", id, "A categoria possui regras de importacao vinculadas.");
        _repository.Execute("DELETE FROM Categories WHERE Id = $id;", P("$id", id));
    }

    public DataTable SearchIncomes(ReportFilter filter, string search, string sortBy)
    {
        var orderBy = sortBy switch
        {
            "Valor" => "i.Amount DESC",
            "Descricao" => "i.Description",
            _ => "i.ReceivedDate DESC"
        };

        return _repository.Query(
            $"""
            SELECT i.Id, i.Description AS Descricao, i.Amount AS Valor, i.ReceivedDate AS Data,
                   c.Name AS Categoria, a.Name AS Conta, i.PaymentMethod AS Forma, i.Status, i.Notes AS Observacoes
            FROM Incomes i
            JOIN Categories c ON c.Id = i.CategoryId
            JOIN Accounts a ON a.Id = i.AccountId
            WHERE i.ReceivedDate BETWEEN $start AND $end
              AND ($search = '' OR i.Description LIKE '%' || $search || '%' OR i.Notes LIKE '%' || $search || '%')
              AND ($category = 0 OR i.CategoryId = $category)
              AND ($account = 0 OR i.AccountId = $account)
              AND ($status = '' OR i.Status = $status)
            ORDER BY {orderBy};
            """,
            P("$start", filter.StartDate, "$end", filter.EndDate, "$search", search.Trim(),
              "$category", filter.CategoryId ?? 0, "$account", filter.AccountId ?? 0, "$status", filter.Status ?? ""));
    }

    public void SaveIncome(IncomeInput input)
    {
        Require(input.Description, "Informe a descricao da receita.");
        RequirePositive(input.Amount, "Informe um valor valido para a receita.");
        RequireSelected(input.CategoryId, "Selecione uma categoria de receita.");
        RequireSelected(input.AccountId, "Selecione a conta de destino.");
        EnsureCategoryType(input.CategoryId, FinanceTypes.Income);

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Incomes(Description, Amount, ReceivedDate, CategoryId, AccountId, PaymentMethod, Notes, Status)
                VALUES($description, $amount, $date, $category, $account, $method, $notes, $status);
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$date", input.ReceivedDate,
                  "$category", input.CategoryId, "$account", input.AccountId, "$method", input.PaymentMethod.Trim(),
                  "$notes", input.Notes.Trim(), "$status", input.Status));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE Incomes
                SET Description = $description, Amount = $amount, ReceivedDate = $date, CategoryId = $category,
                    AccountId = $account, PaymentMethod = $method, Notes = $notes, Status = $status
                WHERE Id = $id;
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$date", input.ReceivedDate,
                  "$category", input.CategoryId, "$account", input.AccountId, "$method", input.PaymentMethod.Trim(),
                  "$notes", input.Notes.Trim(), "$status", input.Status, "$id", input.Id));
        }

        RecalculateAccountBalances();
    }

    public void DeleteIncome(int id)
    {
        _repository.Execute("DELETE FROM Incomes WHERE Id = $id;", P("$id", id));
        RecalculateAccountBalances();
    }

    public DataTable SearchExpenses(ReportFilter filter, string search, string sortBy)
    {
        RefreshOverdueExpenses();
        var orderBy = sortBy switch
        {
            "Valor" => "e.Amount DESC",
            "Descricao" => "e.Description",
            _ => "e.DueDate DESC"
        };

        return _repository.Query(
            $"""
            SELECT e.Id, e.Description AS Descricao, e.Amount AS Valor, e.ExpenseDate AS Data,
                   e.DueDate AS Vencimento, c.Name AS Categoria, COALESCE(a.Name, '') AS Conta,
                   COALESCE(cc.Name, '') AS Cartao, e.PaymentMethod AS Forma, e.Status,
                   CASE WHEN e.InstallmentTotal > 1 THEN e.InstallmentNumber || '/' || e.InstallmentTotal ELSE '' END AS Parcela,
                   e.Notes AS Observacoes
            FROM Expenses e
            JOIN Categories c ON c.Id = e.CategoryId
            LEFT JOIN Accounts a ON a.Id = e.AccountId
            LEFT JOIN CreditCards cc ON cc.Id = e.CreditCardId
            WHERE e.DueDate BETWEEN $start AND $end
              AND ($search = '' OR e.Description LIKE '%' || $search || '%' OR e.Notes LIKE '%' || $search || '%')
              AND ($category = 0 OR e.CategoryId = $category)
              AND ($account = 0 OR e.AccountId = $account)
              AND ($card = 0 OR e.CreditCardId = $card)
              AND ($status = '' OR e.Status = $status)
            ORDER BY {orderBy};
            """,
            P("$start", filter.StartDate, "$end", filter.EndDate, "$search", search.Trim(),
              "$category", filter.CategoryId ?? 0, "$account", filter.AccountId ?? 0,
              "$card", filter.CreditCardId ?? 0, "$status", filter.Status ?? ""));
    }

    public void SaveExpense(ExpenseInput input)
    {
        Require(input.Description, "Informe a descricao da despesa.");
        RequirePositive(input.Amount, "Informe um valor valido para a despesa.");
        RequireSelected(input.CategoryId, "Selecione uma categoria de despesa.");
        EnsureCategoryType(input.CategoryId, FinanceTypes.Expense);

        if (input.Id > 0 && Number("SELECT COUNT(*) FROM ExpenseSplits WHERE ExpenseId=$id;", P("$id", input.Id)) > 0
            && Money("SELECT COALESCE(SUM(Amount),0) FROM ExpenseSplits WHERE ExpenseId=$id;", P("$id", input.Id)) != input.Amount)
            throw new InvalidOperationException("Remova o rateio antes de alterar o valor total da despesa.");

        if (input.Status == FinanceStatus.Paid && input.AccountId is null or <= 0)
        {
            throw new InvalidOperationException("Despesa paga precisa ter uma conta de pagamento.");
        }

        if ((input.AccountId is null or <= 0) && (input.CreditCardId is null or <= 0))
        {
            throw new InvalidOperationException("Selecione uma conta ou cartao para a despesa.");
        }

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Expenses(Description, Amount, ExpenseDate, DueDate, CategoryId, AccountId, CreditCardId,
                                     PaymentMethod, Notes, Status, RecurringExpenseId, InstallmentGroupId, InstallmentNumber, InstallmentTotal)
                VALUES($description, $amount, $expenseDate, $dueDate, $category, $account, $card,
                       $method, $notes, $status, $recurring, $group, $number, $total);
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$expenseDate", input.ExpenseDate,
                  "$dueDate", input.DueDate, "$category", input.CategoryId, "$account", input.AccountId, "$card", input.CreditCardId,
                  "$method", input.PaymentMethod.Trim(), "$notes", input.Notes.Trim(), "$status", input.Status,
                  "$recurring", input.RecurringExpenseId, "$group", input.InstallmentGroupId,
                  "$number", input.InstallmentNumber, "$total", input.InstallmentTotal));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE Expenses
                SET Description = $description, Amount = $amount, ExpenseDate = $expenseDate, DueDate = $dueDate,
                    CategoryId = $category, AccountId = $account, CreditCardId = $card, PaymentMethod = $method,
                    Notes = $notes, Status = $status
                WHERE Id = $id;
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$expenseDate", input.ExpenseDate,
                  "$dueDate", input.DueDate, "$category", input.CategoryId, "$account", input.AccountId, "$card", input.CreditCardId,
                  "$method", input.PaymentMethod.Trim(), "$notes", input.Notes.Trim(), "$status", input.Status, "$id", input.Id));
        }

        RefreshDerivedData();
    }

    public void DeleteExpense(int id)
    {
        _repository.Execute("DELETE FROM Expenses WHERE Id = $id;", P("$id", id));
        RefreshDerivedData();
    }

    public DataTable SearchRecurringExpenses(string search)
    {
        return _repository.Query(
            """
            SELECT r.Id, r.Description AS Descricao, r.Amount AS Valor, r.DayOfMonth AS Dia,
                   c.Name AS Categoria, a.Name AS Conta, r.PaymentMethod AS Forma, r.StartDate AS Inicio,
                   COALESCE(r.EndDate, '') AS Fim, CASE r.IsActive WHEN 1 THEN 'Ativa' ELSE 'Inativa' END AS Status,
                   r.Notes AS Observacoes
            FROM RecurringExpenses r
            JOIN Categories c ON c.Id = r.CategoryId
            JOIN Accounts a ON a.Id = r.AccountId
            WHERE $search = '' OR r.Description LIKE '%' || $search || '%'
            ORDER BY r.IsActive DESC, r.Description;
            """,
            P("$search", search.Trim()));
    }

    public void SaveRecurringExpense(RecurringExpenseInput input)
    {
        Require(input.Description, "Informe a descricao da recorrencia.");
        RequirePositive(input.Amount, "Informe um valor valido.");
        RequireSelected(input.CategoryId, "Selecione a categoria.");
        RequireSelected(input.AccountId, "Selecione a conta.");
        EnsureCategoryType(input.CategoryId, FinanceTypes.Expense);
        if (input.EndDate.HasValue && input.EndDate.Value.Date < input.StartDate.Date)
        {
            throw new InvalidOperationException("A data final deve ser igual ou posterior ao inicio.");
        }
        if (input.DayOfMonth is < 1 or > 31)
        {
            throw new InvalidOperationException("O dia de vencimento deve ficar entre 1 e 31.");
        }

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO RecurringExpenses(Description, Amount, DayOfMonth, CategoryId, AccountId, PaymentMethod, Notes, StartDate, EndDate, IsActive)
                VALUES($description, $amount, $day, $category, $account, $method, $notes, $start, $end, $active);
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$day", input.DayOfMonth,
                  "$category", input.CategoryId, "$account", input.AccountId, "$method", input.PaymentMethod.Trim(),
                  "$notes", input.Notes.Trim(), "$start", input.StartDate, "$end", input.EndDate, "$active", input.IsActive));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE RecurringExpenses
                SET Description = $description, Amount = $amount, DayOfMonth = $day, CategoryId = $category, AccountId = $account,
                    PaymentMethod = $method, Notes = $notes, StartDate = $start, EndDate = $end, IsActive = $active
                WHERE Id = $id;
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$day", input.DayOfMonth,
                  "$category", input.CategoryId, "$account", input.AccountId, "$method", input.PaymentMethod.Trim(),
                  "$notes", input.Notes.Trim(), "$start", input.StartDate, "$end", input.EndDate, "$active", input.IsActive, "$id", input.Id));
        }
    }

    public int GenerateRecurringExpenses(int monthsAhead)
    {
        if (monthsAhead is < 0 or > 24)
        {
            throw new InvalidOperationException("Escolha entre 0 e 24 meses futuros.");
        }
        var rows = _repository.Query(
            """
            SELECT Id, Description, Amount, DayOfMonth, CategoryId, AccountId, PaymentMethod, Notes, StartDate, EndDate
            FROM RecurringExpenses
            WHERE IsActive = 1;
            """);
        var generated = 0;
        var until = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(monthsAhead + 1).AddDays(-1);

        foreach (DataRow row in rows.Rows)
        {
            var start = DateTime.Parse(Convert.ToString(row["StartDate"])!, CultureInfo.InvariantCulture);
            var endText = Convert.ToString(row["EndDate"]);
            var end = string.IsNullOrWhiteSpace(endText) ? until : DateTime.Parse(endText, CultureInfo.InvariantCulture);
            if (end > until)
            {
                end = until;
            }

            var cursor = new DateTime(start.Year, start.Month, 1);
            while (cursor <= end)
            {
                var day = Math.Min(Convert.ToInt32(row["DayOfMonth"]), DateTime.DaysInMonth(cursor.Year, cursor.Month));
                var dueDate = new DateTime(cursor.Year, cursor.Month, day);
                if (dueDate >= start && dueDate <= end)
                {
                    var exists = Number(
                        "SELECT COUNT(*) FROM Expenses WHERE RecurringExpenseId = $id AND substr(DueDate, 1, 7) = $month;",
                        P("$id", row["Id"], "$month", dueDate.ToString("yyyy-MM")));
                    if (exists == 0)
                    {
                        SaveExpense(new ExpenseInput
                        {
                            Description = Convert.ToString(row["Description"])!,
                            Amount = ToDecimal(row["Amount"]),
                            ExpenseDate = dueDate,
                            DueDate = dueDate,
                            CategoryId = Convert.ToInt32(row["CategoryId"]),
                            AccountId = Convert.ToInt32(row["AccountId"]),
                            PaymentMethod = Convert.ToString(row["PaymentMethod"])!,
                            Notes = Convert.ToString(row["Notes"])!,
                            Status = FinanceStatus.Pending,
                            RecurringExpenseId = Convert.ToInt32(row["Id"])
                        });
                        generated++;
                    }
                }

                cursor = cursor.AddMonths(1);
            }
        }

        RefreshDerivedData();
        return generated;
    }

    public void DeleteRecurringExpense(int id)
    {
        _repository.Execute("DELETE FROM RecurringExpenses WHERE Id = $id;", P("$id", id));
    }

    public DataTable SearchInstallmentGroups()
    {
        return _repository.Query(
            """
            SELECT g.Id, g.Description AS Descricao, g.TotalAmount AS ValorTotal,
                   g.InstallmentCount AS Parcelas, g.FirstDueDate AS PrimeiroVencimento,
                   c.Name AS Categoria, COALESCE(a.Name, '') AS Conta, COALESCE(cc.Name, '') AS Cartao
            FROM InstallmentGroups g
            JOIN Categories c ON c.Id = g.CategoryId
            LEFT JOIN Accounts a ON a.Id = g.AccountId
            LEFT JOIN CreditCards cc ON cc.Id = g.CreditCardId
            ORDER BY g.CreatedAt DESC;
            """);
    }

    public DataTable GetInstallmentDetails(int groupId)
    {
        return _repository.Query(
            """
            SELECT 'Despesa' AS Tipo, Description AS Descricao, Amount AS Valor, DueDate AS Vencimento,
                   Status, InstallmentNumber || '/' || InstallmentTotal AS Parcela
            FROM Expenses
            WHERE InstallmentGroupId = $id
            UNION ALL
            SELECT 'Cartao' AS Tipo, Description AS Descricao, Amount AS Valor, InvoiceMonth AS Vencimento,
                   Status, InstallmentNumber || '/' || InstallmentTotal AS Parcela
            FROM CardPurchases
            WHERE InstallmentGroupId = $id
            ORDER BY Parcela;
            """,
            P("$id", groupId));
    }

    public void CreateInstallments(InstallmentInput input)
    {
        Require(input.Description, "Informe a descricao do parcelamento.");
        RequirePositive(input.TotalAmount, "Informe o valor total.");
        RequireSelected(input.CategoryId, "Selecione uma categoria.");
        EnsureCategoryType(input.CategoryId, FinanceTypes.Expense);
        if (input.InstallmentCount <= 0)
        {
            throw new InvalidOperationException("Informe o numero de parcelas.");
        }

        var hasAccount = input.AccountId is > 0;
        var hasCard = input.CreditCardId is > 0;
        if (!hasAccount && !hasCard)
        {
            throw new InvalidOperationException("Selecione uma conta ou cartao.");
        }

        if (hasAccount && hasCard)
        {
            throw new InvalidOperationException("Escolha somente uma conta ou um cartao.");
        }
        if (input.TotalAmount * 100 < input.InstallmentCount)
        {
            throw new InvalidOperationException("Cada parcela deve ter pelo menos R$ 0,01.");
        }

        DateTime? firstInvoiceMonth = null;
        if (hasCard)
        {
            EnsureCardLimit(input.CreditCardId!.Value, input.TotalAmount, 0);
            firstInvoiceMonth = ParseMonth(CalculateInvoiceMonth(input.CreditCardId.Value, input.FirstDueDate));
            for (var i = 0; i < input.InstallmentCount; i++)
            {
                EnsureInvoiceOpen(input.CreditCardId.Value, firstInvoiceMonth.Value.AddMonths(i).ToString("yyyy-MM"));
            }
        }

        _repository.Transaction((connection, transaction) =>
        {
            var groupId = (int)Repository.Insert(
                connection,
                transaction,
                """
                INSERT INTO InstallmentGroups(Description, TotalAmount, InstallmentCount, FirstDueDate, CategoryId, AccountId, CreditCardId)
                VALUES($description, $total, $count, $first, $category, $account, $card)
                """,
                P("$description", input.Description.Trim(), "$total", input.TotalAmount, "$count", input.InstallmentCount,
                  "$first", input.FirstDueDate, "$category", input.CategoryId, "$account", input.AccountId, "$card", input.CreditCardId));

            var baseAmount = Math.Floor(input.TotalAmount * 100 / input.InstallmentCount) / 100;
            var extraCents = (int)(input.TotalAmount * 100 - baseAmount * 100 * input.InstallmentCount);
            for (var i = 1; i <= input.InstallmentCount; i++)
            {
                var amount = baseAmount + (i <= extraCents ? 0.01m : 0m);
                var dueDate = input.FirstDueDate.AddMonths(i - 1);
                var description = $"{input.Description.Trim()} ({i}/{input.InstallmentCount})";

                if (hasCard)
                {
                    var invoiceMonth = firstInvoiceMonth!.Value.AddMonths(i - 1).ToString("yyyy-MM");
                    Repository.Execute(
                        connection,
                        transaction,
                        """
                        INSERT INTO CardPurchases(Description, Amount, PurchaseDate, CreditCardId, CategoryId, InstallmentGroupId,
                                                  InstallmentNumber, InstallmentTotal, InvoiceMonth, Notes, Status)
                        VALUES($description, $amount, $purchaseDate, $card, $category, $group, $number, $total, $invoiceMonth, $notes, 'Aberta');
                        """,
                        P("$description", description, "$amount", amount, "$purchaseDate", dueDate, "$card", input.CreditCardId,
                          "$category", input.CategoryId, "$group", groupId, "$number", i, "$total", input.InstallmentCount,
                          "$invoiceMonth", invoiceMonth, "$notes", input.Notes.Trim()));
                }
                else
                {
                    Repository.Execute(
                        connection,
                        transaction,
                        """
                        INSERT INTO Expenses(Description, Amount, ExpenseDate, DueDate, CategoryId, AccountId, PaymentMethod, Notes,
                                             Status, InstallmentGroupId, InstallmentNumber, InstallmentTotal)
                        VALUES($description, $amount, $expenseDate, $dueDate, $category, $account, $method, $notes,
                               'Pendente', $group, $number, $total);
                        """,
                        P("$description", description, "$amount", amount, "$expenseDate", dueDate, "$dueDate", dueDate,
                          "$category", input.CategoryId, "$account", input.AccountId, "$method", input.PaymentMethod.Trim(),
                          "$notes", input.Notes.Trim(), "$group", groupId, "$number", i, "$total", input.InstallmentCount));
                }
            }
        });

        RefreshDerivedData();
    }

    public DataTable SearchCreditCards(string search)
    {
        return _repository.Query(
            """
            SELECT Id, Name AS Nome, Institution AS Instituicao, TotalLimit AS LimiteTotal,
                   AvailableLimit AS LimiteDisponivel, ClosingDay AS Fechamento,
                   DueDay AS Vencimento, CASE IsActive WHEN 1 THEN 'Ativo' ELSE 'Inativo' END AS Status
            FROM CreditCards
            WHERE $search = '' OR Name LIKE '%' || $search || '%' OR Institution LIKE '%' || $search || '%'
            ORDER BY Name;
            """,
            P("$search", search.Trim()));
    }

    public void SaveCreditCard(CreditCardInput input)
    {
        Require(input.Name, "Informe o nome do cartao.");
        RequirePositive(input.TotalLimit, "Informe um limite valido.");
        if (input.Id > 0)
        {
            var committed = Money("SELECT COALESCE(SUM(Amount), 0) FROM CardPurchases WHERE CreditCardId = $id AND Status <> 'Paga';", P("$id", input.Id));
            if (input.TotalLimit < committed)
            {
                throw new InvalidOperationException("O limite total nao pode ser menor que as compras ainda nao pagas.");
            }
        }
        if (input.ClosingDay is < 1 or > 31 || input.DueDay is < 1 or > 31)
        {
            throw new InvalidOperationException("Fechamento e vencimento devem ficar entre 1 e 31.");
        }

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO CreditCards(Name, Institution, TotalLimit, AvailableLimit, ClosingDay, DueDay, IsActive)
                VALUES($name, $institution, $total, $total, $closing, $due, $active);
                """,
                P("$name", input.Name.Trim(), "$institution", input.Institution.Trim(), "$total", input.TotalLimit,
                  "$closing", input.ClosingDay, "$due", input.DueDay, "$active", input.IsActive));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE CreditCards
                SET Name = $name, Institution = $institution, TotalLimit = $total, ClosingDay = $closing,
                    DueDay = $due, IsActive = $active
                WHERE Id = $id;
                """,
                P("$name", input.Name.Trim(), "$institution", input.Institution.Trim(), "$total", input.TotalLimit,
                  "$closing", input.ClosingDay, "$due", input.DueDay, "$active", input.IsActive, "$id", input.Id));
        }

        RecalculateCardLimits();
    }

    public void DeleteCreditCard(int id)
    {
        EnsureNoLinks("CardPurchases", "CreditCardId", id, "O cartao possui compras vinculadas.");
        EnsureNoLinks("Invoices", "CreditCardId", id, "O cartao possui faturas vinculadas.");
        EnsureNoLinks("InstallmentGroups", "CreditCardId", id, "O cartao possui parcelamentos vinculados.");
        _repository.Execute("DELETE FROM CreditCards WHERE Id = $id;", P("$id", id));
    }

    public DataTable SearchCardPurchases(ReportFilter filter, string search)
    {
        return _repository.Query(
            """
            SELECT cp.Id, cp.Description AS Descricao, cp.Amount AS Valor, cp.PurchaseDate AS Data,
                   cc.Name AS Cartao, c.Name AS Categoria, cp.InvoiceMonth AS Fatura, cp.Status,
                   cp.InstallmentNumber || '/' || cp.InstallmentTotal AS Parcela, cp.Notes AS Observacoes
            FROM CardPurchases cp
            JOIN CreditCards cc ON cc.Id = cp.CreditCardId
            JOIN Categories c ON c.Id = cp.CategoryId
            WHERE cp.PurchaseDate BETWEEN $start AND $end
              AND ($search = '' OR cp.Description LIKE '%' || $search || '%')
              AND ($category = 0 OR cp.CategoryId = $category)
              AND ($card = 0 OR cp.CreditCardId = $card)
              AND ($status = '' OR cp.Status = $status)
            ORDER BY cp.PurchaseDate DESC;
            """,
            P("$start", filter.StartDate, "$end", filter.EndDate, "$search", search.Trim(),
              "$category", filter.CategoryId ?? 0, "$card", filter.CreditCardId ?? 0, "$status", filter.Status ?? ""));
    }

    public void SaveCardPurchase(CardPurchaseInput input)
    {
        Require(input.Description, "Informe a descricao da compra.");
        RequirePositive(input.Amount, "Informe o valor da compra.");
        RequireSelected(input.CreditCardId, "Selecione um cartao.");
        RequireSelected(input.CategoryId, "Selecione uma categoria.");
        EnsureCategoryType(input.CategoryId, FinanceTypes.Expense);

        if (input.Id > 0)
        {
            var status = Convert.ToString(_repository.Scalar("SELECT Status FROM CardPurchases WHERE Id = $id;", P("$id", input.Id)));
            if (status != FinanceStatus.Open)
            {
                throw new InvalidOperationException("Somente compras em fatura aberta podem ser editadas.");
            }
            if (input.InstallmentCount > 1)
            {
                throw new InvalidOperationException("Crie um novo parcelamento para gerar novas parcelas.");
            }
        }

        if (input.InstallmentCount > 1)
        {
            CreateInstallments(new InstallmentInput
            {
                Description = input.Description,
                TotalAmount = input.Amount,
                InstallmentCount = input.InstallmentCount,
                FirstDueDate = input.PurchaseDate,
                CategoryId = input.CategoryId,
                CreditCardId = input.CreditCardId,
                Notes = input.Notes,
                PaymentMethod = "Cartao"
            });
            return;
        }

        var previousAmount = input.Id == 0
            ? 0m
            : Money("SELECT COALESCE(Amount, 0) FROM CardPurchases WHERE Id = $id AND CreditCardId = $card AND Status = 'Aberta';", P("$id", input.Id, "$card", input.CreditCardId));
        EnsureCardLimit(input.CreditCardId, input.Amount, previousAmount);
        var invoiceMonth = CalculateInvoiceMonth(input.CreditCardId, input.PurchaseDate);
        EnsureInvoiceOpen(input.CreditCardId, invoiceMonth);

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO CardPurchases(Description, Amount, PurchaseDate, CreditCardId, CategoryId, InvoiceMonth, Notes, Status)
                VALUES($description, $amount, $date, $card, $category, $invoiceMonth, $notes, 'Aberta');
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$date", input.PurchaseDate,
                  "$card", input.CreditCardId, "$category", input.CategoryId, "$invoiceMonth", invoiceMonth, "$notes", input.Notes.Trim()));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE CardPurchases
                SET Description = $description, Amount = $amount, PurchaseDate = $date, CreditCardId = $card,
                    CategoryId = $category, InvoiceMonth = $invoiceMonth, Notes = $notes
                WHERE Id = $id AND Status = 'Aberta';
                """,
                P("$description", input.Description.Trim(), "$amount", input.Amount, "$date", input.PurchaseDate,
                  "$card", input.CreditCardId, "$category", input.CategoryId, "$invoiceMonth", invoiceMonth,
                  "$notes", input.Notes.Trim(), "$id", input.Id));
        }

        RecalculateCardLimits();
    }

    public void DeleteCardPurchase(int id)
    {
        var status = Convert.ToString(_repository.Scalar("SELECT Status FROM CardPurchases WHERE Id = $id;", P("$id", id))) ?? "";
        if (status != FinanceStatus.Open)
        {
            throw new InvalidOperationException("Somente compras em fatura aberta podem ser excluidas.");
        }

        _repository.Execute("DELETE FROM CardPurchases WHERE Id = $id;", P("$id", id));
        RecalculateCardLimits();
    }

    public DataTable SearchInvoices(string month = "")
    {
        return _repository.Query(
            """
            SELECT i.Id, cc.Name AS Cartao, i.InvoiceMonth AS Mes, i.TotalAmount AS Valor,
                   i.ClosingDate AS Fechamento, i.DueDate AS Vencimento, i.Status,
                   COALESCE(a.Name, '') AS ContaPagamento, COALESCE(i.PaidAt, '') AS PagoEm
            FROM Invoices i
            JOIN CreditCards cc ON cc.Id = i.CreditCardId
            LEFT JOIN Accounts a ON a.Id = i.PaidAccountId
            WHERE $month = '' OR i.InvoiceMonth = $month
            ORDER BY i.InvoiceMonth DESC, cc.Name;
            """,
            P("$month", month.Trim()));
    }

    public void CloseInvoice(int cardId, string invoiceMonth)
    {
        RequireSelected(cardId, "Selecione um cartao.");
        Require(invoiceMonth, "Informe o mes da fatura no formato AAAA-MM.");
        _ = ParseMonth(invoiceMonth);
        EnsureInvoiceOpen(cardId, invoiceMonth);

        var card = _repository.Query("SELECT ClosingDay, DueDay FROM CreditCards WHERE Id = $id;", P("$id", cardId));
        if (card.Rows.Count == 0)
        {
            throw new InvalidOperationException("Cartao nao encontrado.");
        }

        var openCount = Number(
            "SELECT COUNT(*) FROM CardPurchases WHERE CreditCardId = $card AND InvoiceMonth = $month AND Status = 'Aberta';",
            P("$card", cardId, "$month", invoiceMonth));
        if (openCount == 0)
        {
            throw new InvalidOperationException("Nao existem compras abertas para esta fatura.");
        }

        var total = Money(
            "SELECT COALESCE(SUM(Amount), 0) FROM CardPurchases WHERE CreditCardId = $card AND InvoiceMonth = $month AND Status <> 'Paga';",
            P("$card", cardId, "$month", invoiceMonth));
        if (total <= 0)
        {
            throw new InvalidOperationException("Nao existem compras abertas para esta fatura.");
        }

        var closingDate = MonthDay(invoiceMonth, Convert.ToInt32(card.Rows[0]["ClosingDay"]));
        var dueDate = MonthDay(invoiceMonth, Convert.ToInt32(card.Rows[0]["DueDay"]));
        if (dueDate < closingDate)
        {
            dueDate = MonthDay(ParseMonth(invoiceMonth).AddMonths(1).ToString("yyyy-MM"), Convert.ToInt32(card.Rows[0]["DueDay"]));
        }

        _repository.Transaction((connection, transaction) =>
        {
            Repository.Execute(
                connection,
                transaction,
                """
                INSERT INTO Invoices(CreditCardId, InvoiceMonth, ClosingDate, DueDate, Status, TotalAmount)
                VALUES($card, $month, $closing, $due, 'Fechada', $total)
                ON CONFLICT(CreditCardId, InvoiceMonth) DO UPDATE SET
                    ClosingDate = excluded.ClosingDate,
                    DueDate = excluded.DueDate,
                    Status = 'Fechada',
                    TotalAmount = excluded.TotalAmount;
                """,
                P("$card", cardId, "$month", invoiceMonth, "$closing", closingDate, "$due", dueDate, "$total", total));

            Repository.Execute(
                connection,
                transaction,
                "UPDATE CardPurchases SET Status = 'Fechada' WHERE CreditCardId = $card AND InvoiceMonth = $month AND Status = 'Aberta';",
                P("$card", cardId, "$month", invoiceMonth));
        });

        RecalculateCardLimits();
    }

    public void PayInvoice(int invoiceId, int accountId)
    {
        RequireSelected(accountId, "Selecione a conta de pagamento.");
        var invoice = _repository.Query("SELECT CreditCardId, InvoiceMonth, Status FROM Invoices WHERE Id = $id;", P("$id", invoiceId));
        if (invoice.Rows.Count == 0)
        {
            throw new InvalidOperationException("Fatura nao encontrada.");
        }

        if (Convert.ToString(invoice.Rows[0]["Status"]) == "Paga")
        {
            throw new InvalidOperationException("Esta fatura ja foi paga.");
        }

        _repository.Transaction((connection, transaction) =>
        {
            Repository.Execute(
                connection,
                transaction,
                "UPDATE Invoices SET Status = 'Paga', PaidAccountId = $account, PaidAt = $today WHERE Id = $id;",
                P("$account", accountId, "$today", DateTime.Today, "$id", invoiceId));

            Repository.Execute(
                connection,
                transaction,
                "UPDATE CardPurchases SET Status = 'Paga' WHERE CreditCardId = $card AND InvoiceMonth = $month;",
                P("$card", invoice.Rows[0]["CreditCardId"], "$month", invoice.Rows[0]["InvoiceMonth"]));
        });

        RefreshDerivedData();
    }

    public DataTable SearchBudgets(string month)
    {
        return _repository.Query(
            """
            WITH budget_rows AS (
                SELECT b.Id, b.Month, c.Name AS Categoria, b.LimitAmount,
                       COALESCE((SELECT SUM(e.Amount) FROM ExpenseAllocations e WHERE e.CategoryId = b.CategoryId AND substr(e.DueDate, 1, 7) = b.Month), 0)
                     + COALESCE((SELECT SUM(cp.Amount) FROM CardPurchases cp WHERE cp.CategoryId = b.CategoryId AND cp.InvoiceMonth = b.Month), 0) AS Gasto
                FROM Budgets b
                JOIN Categories c ON c.Id = b.CategoryId
                WHERE $month = '' OR b.Month = $month
            )
            SELECT Id, Month AS Mes, Categoria, LimitAmount AS Limite, Gasto,
                   ROUND(CASE WHEN LimitAmount <= 0 THEN 0 ELSE (Gasto * 100.0 / LimitAmount) END, 2) AS Percentual,
                   CASE
                       WHEN LimitAmount > 0 AND Gasto >= LimitAmount THEN 'Limite ultrapassado'
                       WHEN LimitAmount > 0 AND Gasto >= LimitAmount * 0.90 THEN 'Alerta 90%'
                       WHEN LimitAmount > 0 AND Gasto >= LimitAmount * 0.80 THEN 'Alerta 80%'
                       ELSE 'Dentro do limite'
                   END AS Alerta
            FROM budget_rows
            ORDER BY Month DESC, Categoria;
            """,
            P("$month", month.Trim()));
    }

    public void SaveBudget(BudgetInput input)
    {
        Require(input.Month, "Informe o mes no formato AAAA-MM.");
        RequireSelected(input.CategoryId, "Selecione a categoria.");
        RequirePositive(input.LimitAmount, "Informe um limite valido.");
        _ = ParseMonth(input.Month);
        EnsureCategoryType(input.CategoryId, FinanceTypes.Expense);

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Budgets(Month, CategoryId, LimitAmount)
                VALUES($month, $category, $limit)
                ON CONFLICT(Month, CategoryId) DO UPDATE SET LimitAmount = excluded.LimitAmount;
                """,
                P("$month", input.Month.Trim(), "$category", input.CategoryId, "$limit", input.LimitAmount));
        }
        else
        {
            _repository.Execute(
                "UPDATE Budgets SET Month = $month, CategoryId = $category, LimitAmount = $limit WHERE Id = $id;",
                P("$month", input.Month.Trim(), "$category", input.CategoryId, "$limit", input.LimitAmount, "$id", input.Id));
        }
    }

    public void DeleteBudget(int id)
    {
        _repository.Execute("DELETE FROM Budgets WHERE Id = $id;", P("$id", id));
    }

    public DataTable SearchGoals(string search)
    {
        return _repository.Query(
            """
            SELECT Id, Name AS Nome, TargetAmount AS ValorObjetivo, CurrentAmount AS ValorAtual,
                   TargetDate AS DataAlvo, ROUND(CASE WHEN TargetAmount <= 0 THEN 0 ELSE CurrentAmount * 100.0 / TargetAmount END, 2) AS Percentual,
                   Description AS Descricao, Status
            FROM Goals
            WHERE $search = '' OR Name LIKE '%' || $search || '%'
            ORDER BY Status, TargetDate;
            """,
            P("$search", search.Trim()));
    }

    public void SaveGoal(GoalInput input)
    {
        Require(input.Name, "Informe o nome da meta.");
        RequirePositive(input.TargetAmount, "Informe o valor objetivo.");

        if (input.Id == 0)
        {
            _repository.Execute(
                """
                INSERT INTO Goals(Name, TargetAmount, CurrentAmount, TargetDate, Description, Status)
                VALUES($name, $target, $current, $date, $description, $status);
                """,
                P("$name", input.Name.Trim(), "$target", input.TargetAmount, "$current", input.CurrentAmount,
                  "$date", input.TargetDate, "$description", input.Description.Trim(), "$status", input.Status));
        }
        else
        {
            _repository.Execute(
                """
                UPDATE Goals
                SET Name = $name, TargetAmount = $target, CurrentAmount = $current, TargetDate = $date,
                    Description = $description, Status = $status
                WHERE Id = $id;
                """,
                P("$name", input.Name.Trim(), "$target", input.TargetAmount, "$current", input.CurrentAmount,
                  "$date", input.TargetDate, "$description", input.Description.Trim(), "$status", input.Status, "$id", input.Id));
        }
    }

    public void AddGoalContribution(int goalId, decimal amount, DateTime date, string notes)
    {
        RequireSelected(goalId, "Selecione uma meta.");
        RequirePositive(amount, "Informe um valor valido para o aporte.");
        _repository.Transaction((connection, transaction) =>
        {
            Repository.Execute(
                connection,
                transaction,
                "INSERT INTO GoalContributions(GoalId, Amount, ContributionDate, Notes) VALUES($goal, $amount, $date, $notes);",
                P("$goal", goalId, "$amount", amount, "$date", date, "$notes", notes.Trim()));
            Repository.Execute(
                connection,
                transaction,
                "UPDATE Goals SET CurrentAmount = CurrentAmount + $amount WHERE Id = $goal;",
                P("$amount", amount, "$goal", goalId));
            Repository.Execute(
                connection,
                transaction,
                "UPDATE Goals SET Status = 'Concluida' WHERE Id = $goal AND CurrentAmount >= TargetAmount;",
                P("$goal", goalId));
        });
    }

    public void DeleteGoal(int id)
    {
        _repository.Execute("DELETE FROM Goals WHERE Id = $id;", P("$id", id));
    }

    public DataTable GetGoalContributions(int goalId)
    {
        return _repository.Query(
            """
            SELECT ContributionDate AS Data, Amount AS Valor, Notes AS Observacoes
            FROM GoalContributions
            WHERE GoalId = $goal
            ORDER BY ContributionDate DESC;
            """,
            P("$goal", goalId));
    }

    public DataTable GetReport(string reportType, ReportFilter filter)
    {
        if (filter.EndDate.Date < filter.StartDate.Date)
        {
            throw new InvalidOperationException("A data final deve ser igual ou posterior a data inicial.");
        }
        RefreshOverdueExpenses();
        return reportType switch
        {
            "Receitas por periodo" => SearchIncomes(filter, "", "Data"),
            "Despesas por periodo" => SearchExpenses(filter, "", "Data"),
            "Despesas por categoria" => ReportExpensesByCategory(filter),
            "Saldo mensal" => ReportMonthlyBalance(filter),
            "Contas pagas" => ReportExpensesByStatus(filter, FinanceStatus.Paid),
            "Contas pendentes" => ReportExpensesByStatus(filter, FinanceStatus.Pending),
            "Contas vencidas" => ReportExpensesByStatus(filter, FinanceStatus.Overdue),
            "Gastos por cartao" => ReportCardSpending(filter),
            "Evolucao mensal" => ReportMonthlyBalance(filter),
            "Receitas x despesas" => ReportMonthlyBalance(filter),
            _ => ReportMonthlyBalance(filter)
        };
    }

    public void ExportTableToCsv(DataTable table, string filePath)
    {
        if (table.Rows.Count == 0)
        {
            throw new InvalidOperationException("Nao ha dados para exportar.");
        }

        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName))));
        foreach (DataRow row in table.Rows)
        {
            writer.WriteLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(Convert.ToString(row[c]) ?? ""))));
        }
    }

    public void BackupDatabase(string destinationPath)
    {
        new DatabaseBackupService(_database).Backup(destinationPath);
    }

    public void RestoreDatabase(string sourcePath)
    {
        new DatabaseBackupService(_database).Restore(sourcePath);
        Initialize();
    }

    public string GetSetting(string key, string fallback = "")
    {
        return Convert.ToString(_repository.Scalar("SELECT Value FROM Settings WHERE Key = $key;", P("$key", key))) ?? fallback;
    }

    public void SetSetting(string key, string value)
    {
        _repository.Execute(
            "INSERT INTO Settings(Key, Value) VALUES($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;",
            P("$key", key, "$value", value));
    }

    public void SeedDemoData()
    {
        _repository.Transaction((connection, transaction) =>
        {
            var checkingId = Repository.Insert(connection, transaction,
                "INSERT INTO Accounts(Name, Type, InitialBalance, CurrentBalance, IsActive, Notes) VALUES('Conta Corrente Demo', 'Conta corrente', 250000, 250000, 1, 'Conta de exemplo')", null);
            var walletId = Repository.Insert(connection, transaction,
                "INSERT INTO Accounts(Name, Type, InitialBalance, CurrentBalance, IsActive, Notes) VALUES('Carteira Demo', 'Dinheiro fisico', 35000, 35000, 1, 'Carteira de exemplo')", null);
            var salaryCategory = CategoryId(connection, transaction, "Salario");
            var foodCategory = CategoryId(connection, transaction, "Alimentacao");
            var homeCategory = CategoryId(connection, transaction, "Moradia");
            var leisureCategory = CategoryId(connection, transaction, "Lazer");
            var cardCategory = CategoryId(connection, transaction, "Cartao de credito");
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            Repository.Execute(connection, transaction,
                "INSERT INTO Incomes(Description, Amount, ReceivedDate, CategoryId, AccountId, PaymentMethod, Notes, Status) VALUES('Salario Demo', 520000, $date, $category, $account, 'PIX', 'Receita de exemplo', 'Recebido');",
                P("$date", monthStart.AddDays(4), "$category", salaryCategory, "$account", checkingId));
            Repository.Execute(connection, transaction,
                "INSERT INTO Expenses(Description, Amount, ExpenseDate, DueDate, CategoryId, AccountId, PaymentMethod, Notes, Status) VALUES('Mercado Demo', 64000, $date, $due, $category, $account, 'Debito', 'Despesa de exemplo', 'Pago');",
                P("$date", today.AddDays(-6), "$due", today.AddDays(-6), "$category", foodCategory, "$account", checkingId));
            Repository.Execute(connection, transaction,
                "INSERT INTO Expenses(Description, Amount, ExpenseDate, DueDate, CategoryId, AccountId, PaymentMethod, Notes, Status) VALUES('Aluguel Demo', 150000, $date, $due, $category, $account, 'Transferencia', 'Despesa pendente de exemplo', 'Pendente');",
                P("$date", today, "$due", today.AddDays(4), "$category", homeCategory, "$account", checkingId));
            Repository.Execute(connection, transaction,
                "INSERT INTO RecurringExpenses(Description, Amount, DayOfMonth, CategoryId, AccountId, PaymentMethod, Notes, StartDate, IsActive) VALUES('Internet Demo', 11990, 12, $category, $account, 'Debito automatico', 'Recorrencia de exemplo', $start, 1);",
                P("$category", homeCategory, "$account", checkingId, "$start", monthStart));

            var cardId = Repository.Insert(connection, transaction,
                "INSERT INTO CreditCards(Name, Institution, TotalLimit, AvailableLimit, ClosingDay, DueDay, IsActive) VALUES('Cartao Demo', 'Banco Demo', 400000, 400000, 25, 8, 1)", null);
            var demoPurchaseDate = today.AddDays(-2);
            var demoInvoiceMonth = (demoPurchaseDate.Day > 25 ? demoPurchaseDate.AddMonths(1) : demoPurchaseDate).ToString("yyyy-MM");
            Repository.Execute(connection, transaction,
                "INSERT INTO CardPurchases(Description, Amount, PurchaseDate, CreditCardId, CategoryId, InvoiceMonth, Notes, Status) VALUES('Cinema Demo', 9000, $date, $card, $category, $month, 'Compra de exemplo', 'Aberta');",
                P("$date", demoPurchaseDate, "$card", cardId, "$category", leisureCategory, "$month", demoInvoiceMonth));
            Repository.Execute(connection, transaction,
                "INSERT INTO Budgets(Month, CategoryId, LimitAmount) VALUES($month, $category, 90000) ON CONFLICT(Month, CategoryId) DO UPDATE SET LimitAmount = excluded.LimitAmount;",
                P("$month", monthStart.ToString("yyyy-MM"), "$category", foodCategory));
            Repository.Execute(connection, transaction,
                "INSERT INTO Budgets(Month, CategoryId, LimitAmount) VALUES($month, $category, 50000) ON CONFLICT(Month, CategoryId) DO UPDATE SET LimitAmount = excluded.LimitAmount;",
                P("$month", monthStart.ToString("yyyy-MM"), "$category", leisureCategory));
            var goalId = Repository.Insert(connection, transaction,
                "INSERT INTO Goals(Name, TargetAmount, CurrentAmount, TargetDate, Description, Status) VALUES('Reserva Demo', 1000000, 120000, $date, 'Meta de exemplo', 'Ativa')",
                P("$date", today.AddMonths(8)));
            Repository.Execute(connection, transaction,
                "INSERT INTO GoalContributions(GoalId, Amount, ContributionDate, Notes) VALUES($goal, 120000, $date, 'Aporte inicial demo');",
                P("$goal", goalId, "$date", today.AddDays(-10)));
            _ = cardCategory;
            _ = walletId;
        });

        RefreshDerivedData();
    }

    public void ClearFinancialData()
    {
        _repository.Transaction((connection, transaction) =>
        {
            foreach (var table in new[]
            {
                "Attachments", "ExpenseSplits", "ImportedTransactions", "Reconciliations", "Transfers",
                "GoalContributions", "Goals", "Budgets", "Invoices", "CardPurchases",
                "Expenses", "RecurringExpenses", "Incomes", "InstallmentGroups", "CreditCards", "Accounts"
            })
            {
                Repository.Execute(connection, transaction, $"DELETE FROM {table};");
            }
        });
    }

    public List<LookupItem> GetAccounts(bool includeEmpty = true)
    {
        return Lookup("SELECT Id, Name FROM Accounts WHERE IsActive = 1 ORDER BY Name;", includeEmpty);
    }

    public List<LookupItem> GetCategories(string type, bool includeEmpty = true)
    {
        return Lookup("SELECT Id, Name FROM Categories WHERE IsActive = 1 AND ($type = '' OR Type = $type) ORDER BY Name;", includeEmpty, P("$type", type));
    }

    public List<LookupItem> GetCreditCards(bool includeEmpty = true)
    {
        return Lookup("SELECT Id, Name FROM CreditCards WHERE IsActive = 1 ORDER BY Name;", includeEmpty);
    }

    public List<string> GetStatuses(string kind)
    {
        return kind switch
        {
            "income" => new List<string> { "", FinanceStatus.Received, FinanceStatus.Pending },
            "expense" => new List<string> { "", FinanceStatus.Paid, FinanceStatus.Pending, FinanceStatus.Overdue },
            "card" => new List<string> { "", FinanceStatus.Open, FinanceStatus.Closed, "Paga" },
            _ => new List<string> { "" }
        };
    }

    private DataTable ReportExpensesByCategory(ReportFilter filter)
    {
        return _repository.Query(
            $"""
            WITH entries AS ({ReportEntriesSql})
            SELECT c.Name AS Categoria, SUM(e.Amount) AS Total, COUNT(*) AS Lancamentos
            FROM entries e
            JOIN Categories c ON c.Id = e.CategoryId
            WHERE e.Type = 'Despesa' AND {ReportFilterSql}
            GROUP BY c.Id, c.Name
            ORDER BY Total DESC;
            """,
            ReportParameters(filter));
    }

    private DataTable ReportExpensesByStatus(ReportFilter filter, string status)
    {
        filter.Status = status;
        return SearchExpenses(filter, "", "Data");
    }

    private DataTable ReportCardSpending(ReportFilter filter)
    {
        return _repository.Query(
            """
            SELECT cc.Name AS Cartao, cp.InvoiceMonth AS Mes, c.Name AS Categoria,
                   SUM(cp.Amount) AS Total, COUNT(*) AS Compras
            FROM CardPurchases cp
            JOIN CreditCards cc ON cc.Id = cp.CreditCardId
            JOIN Categories c ON c.Id = cp.CategoryId
            WHERE cp.PurchaseDate BETWEEN $start AND $end
              AND ($card = 0 OR cp.CreditCardId = $card)
              AND ($category = 0 OR cp.CategoryId = $category)
              AND ($status = '' OR cp.Status = $status)
            GROUP BY cc.Name, cp.InvoiceMonth, c.Name
            ORDER BY cp.InvoiceMonth DESC, Total DESC;
            """,
            P("$start", filter.StartDate, "$end", filter.EndDate, "$card", filter.CreditCardId ?? 0,
              "$category", filter.CategoryId ?? 0, "$status", filter.Status ?? ""));
    }

    private DataTable ReportMonthlyBalance(ReportFilter filter)
    {
        return _repository.Query(
            $"""
            WITH entries AS ({ReportEntriesSql})
            SELECT e.Month AS Mes,
                   SUM(CASE WHEN e.Type = 'Receita' THEN e.Amount ELSE 0 END) AS Receitas,
                   SUM(CASE WHEN e.Type = 'Despesa' THEN e.Amount ELSE 0 END) AS Despesas,
                   SUM(CASE WHEN e.Type = 'Receita' THEN e.Amount ELSE -e.Amount END) AS Saldo
            FROM entries e
            WHERE {ReportFilterSql}
              AND ($status <> '' OR e.Type <> 'Receita' OR e.Status = 'Recebido')
            GROUP BY e.Month
            ORDER BY e.Month DESC;
            """,
            ReportParameters(filter));
    }

    private const string ReportEntriesSql = """
        SELECT 'Receita' AS Type, Amount, ReceivedDate AS Date, substr(ReceivedDate, 1, 7) AS Month,
               CategoryId, AccountId, NULL AS CreditCardId, Status FROM Incomes
        UNION ALL
        SELECT 'Despesa', Amount, DueDate, substr(DueDate, 1, 7), CategoryId, AccountId, CreditCardId, Status FROM ExpenseAllocations
        UNION ALL
        SELECT 'Despesa', Amount, PurchaseDate, InvoiceMonth, CategoryId, NULL, CreditCardId, Status FROM CardPurchases
        """;

    private const string ReportFilterSql = """
        e.Date BETWEEN $start AND $end
        AND ($category = 0 OR e.CategoryId = $category)
        AND ($account = 0 OR e.AccountId = $account)
        AND ($card = 0 OR e.CreditCardId = $card)
        AND ($status = '' OR e.Status = $status)
        AND ($type = '' OR e.Type = $type)
        """;

    private static Dictionary<string, object?> ReportParameters(ReportFilter filter) =>
        P("$start", filter.StartDate, "$end", filter.EndDate, "$category", filter.CategoryId ?? 0,
          "$account", filter.AccountId ?? 0, "$card", filter.CreditCardId ?? 0,
          "$status", filter.Status ?? "", "$type", filter.TransactionType ?? "");

    private List<LookupItem> Lookup(string sql, bool includeEmpty, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var table = _repository.Query(sql, parameters);
        var items = includeEmpty ? new List<LookupItem> { new(0, "Selecione") } : new List<LookupItem>();
        items.AddRange(table.Rows.Cast<DataRow>().Select(row => new LookupItem(Convert.ToInt32(row["Id"]), Convert.ToString(row["Name"])!)));
        return items;
    }

    private void RefreshOverdueExpenses()
    {
        _repository.Execute(
            "UPDATE Expenses SET Status = CASE WHEN DueDate < $today THEN 'Vencido' ELSE 'Pendente' END WHERE Status <> 'Pago';",
            P("$today", DateTime.Today));
    }

    private void RecalculateAccountBalances()
    {
        _repository.Execute(
            """
            UPDATE Accounts
            SET CurrentBalance = InitialBalance
                + COALESCE((SELECT SUM(i.Amount) FROM Incomes i WHERE i.AccountId = Accounts.Id AND i.Status = 'Recebido'), 0)
                - COALESCE((SELECT SUM(e.Amount) FROM Expenses e WHERE e.AccountId = Accounts.Id AND e.Status = 'Pago'), 0)
                - COALESCE((SELECT SUM(inv.TotalAmount) FROM Invoices inv WHERE inv.PaidAccountId = Accounts.Id AND inv.Status = 'Paga'), 0)
                + COALESCE((SELECT SUM(t.Amount) FROM Transfers t WHERE t.ToAccountId = Accounts.Id), 0)
                - COALESCE((SELECT SUM(t.Amount) FROM Transfers t WHERE t.FromAccountId = Accounts.Id), 0);
            """);
    }

    private void RecalculateCardLimits()
    {
        _repository.Execute(
            """
            UPDATE CreditCards
            SET AvailableLimit = TotalLimit
                - COALESCE((SELECT SUM(cp.Amount) FROM CardPurchases cp WHERE cp.CreditCardId = CreditCards.Id AND cp.Status <> 'Paga'), 0);
            """);
    }

    private void EnsureCardLimit(int cardId, decimal newAmount, decimal previousAmount)
    {
        if (Number("SELECT COUNT(*) FROM CreditCards WHERE Id = $id AND IsActive = 1;", P("$id", cardId)) == 0)
        {
            throw new InvalidOperationException("Selecione um cartao ativo e existente.");
        }
        RecalculateCardLimits();
        var available = Money("SELECT AvailableLimit FROM CreditCards WHERE Id = $id;", P("$id", cardId)) + previousAmount;
        if (newAmount > available)
        {
            throw new InvalidOperationException($"Cartao sem limite disponivel. Limite atual: {available:C2}.");
        }
    }

    private string CalculateInvoiceMonth(int cardId, DateTime purchaseDate)
    {
        var closingDay = Number("SELECT ClosingDay FROM CreditCards WHERE Id = $id;", P("$id", cardId));
        var invoiceDate = purchaseDate.Day > closingDay ? purchaseDate.AddMonths(1) : purchaseDate;
        return invoiceDate.ToString("yyyy-MM");
    }

    private void EnsureNoLinks(string table, string column, int id, string message)
    {
        var count = Number($"SELECT COUNT(*) FROM {table} WHERE {column} = $id;", P("$id", id));
        if (count > 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private decimal Money(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        return MoneyStorage.FromCents(_repository.Scalar(sql, parameters) ?? 0);
    }

    private int Number(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        return Convert.ToInt32(_repository.Scalar(sql, parameters) ?? 0);
    }

    private static Dictionary<string, object?> P(params object?[] values)
    {
        var parameters = new Dictionary<string, object?>();
        for (var i = 0; i < values.Length; i += 2)
        {
            parameters[Convert.ToString(values[i])!] = values[i + 1];
        }

        return parameters;
    }

    private static void Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireSelected(int id, string message)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequirePositive(decimal value, string message)
    {
        if (value <= 0 || decimal.Round(value, 2) != value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static DateTime MonthDay(string yearMonth, int day)
    {
        var date = ParseMonth(yearMonth);
        var year = date.Year;
        var month = date.Month;
        var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, safeDay);
    }

    private static DateTime ParseMonth(string yearMonth)
    {
        if (!DateTime.TryParseExact(yearMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            throw new InvalidOperationException("Informe um mes valido no formato AAAA-MM, por exemplo 2026-10.");
        }
        return month;
    }

    private void EnsureCategoryType(int categoryId, string type)
    {
        if (Number("SELECT COUNT(*) FROM Categories WHERE Id = $id AND Type = $type;", P("$id", categoryId, "$type", type)) == 0)
        {
            throw new InvalidOperationException($"Selecione uma categoria de {type.ToLowerInvariant()} existente.");
        }
    }

    private void EnsureInvoiceOpen(int cardId, string month)
    {
        if (Number("SELECT COUNT(*) FROM Invoices WHERE CreditCardId = $card AND InvoiceMonth = $month AND Status <> 'Aberta';", P("$card", cardId, "$month", month)) > 0)
        {
            throw new InvalidOperationException("Esta fatura ja foi fechada ou paga e nao aceita alteracoes.");
        }
    }

    private static int CategoryId(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, string name)
    {
        return Convert.ToInt32(Repository.Scalar(connection, transaction, "SELECT Id FROM Categories WHERE Name = $name LIMIT 1;", P("$name", name)) ?? 0);
    }

    private static decimal ToDecimal(object value)
    {
        if (value is decimal d)
        {
            return d;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        var needsEscape = value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsEscape ? $"\"{escaped}\"" : escaped;
    }
}
