using System.Data;
using System.Globalization;
using Financeiro.Mobile.Forms;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private void EditAccount(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var name = form.Text("Nome", EditorText(row, "Nome"));
        var type = form.Choice("Tipo", new[] { "Conta corrente", "Dinheiro fisico", "Poupanca", "Carteira digital", "Outros" }, EditorText(row, "Tipo", "Conta corrente"));
        var initial = form.Money("Saldo inicial (R$)", EditorMoney(row, "SaldoInicial"));
        var active = form.Check("Conta ativa", row is null || EditorText(row, "Status") == "Ativo");
        var notes = form.Text("Observações", EditorText(row, "Observacoes"), true);
        form.Show(id == 0 ? "Nova conta" : "Editar conta", () => _service.SaveAccount(new AccountInput
        {
            Id = id,
            Name = name.Text ?? "",
            Type = type.Value,
            InitialBalance = initial.Value,
            IsActive = active.Checked,
            Notes = notes.Text ?? ""
        }), id == 0 ? null : () => _service.DeleteAccount(id));
    }

    private void EditCategory(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var name = form.Text("Nome", EditorText(row, "Nome"));
        var type = form.Choice("Tipo", new[] { FinanceTypes.Income, FinanceTypes.Expense }, EditorText(row, "Tipo", FinanceTypes.Expense));
        var color = form.Text("Cor", EditorText(row, "Cor", "#087F5B"));
        var icon = form.Text("Ícone", EditorText(row, "Icone"));
        var active = form.Check("Categoria ativa", row is null || EditorText(row, "Status") == "Ativo");
        form.Show(id == 0 ? "Nova categoria" : "Editar categoria", () => _service.SaveCategory(new CategoryInput
        {
            Id = id,
            Name = name.Text ?? "",
            Type = type.Value,
            ColorHex = color.Text ?? "",
            Icon = icon.Text ?? "",
            IsActive = active.Checked
        }), id == 0 ? null : () => _service.DeleteCategory(id));
    }

    private void EditIncome(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var description = form.Text("Descrição", EditorText(row, "Descricao"));
        var amount = form.Money("Valor (R$)", EditorMoney(row, "Valor"));
        var date = form.Date("Data do recebimento", EditorDate(row, "Data", DateTime.Today));
        var category = form.Lookup("Categoria", EditorCategories(row, FinanceTypes.Income), EditorText(row, "Categoria"));
        var account = form.Lookup("Conta de destino", EditorAccounts(row), EditorText(row, "Conta"));
        var method = form.Text("Forma de recebimento", EditorText(row, "Forma"));
        var status = form.Choice("Status", _service.GetStatuses("income").Where(value => value.Length > 0), EditorText(row, "Status", FinanceStatus.Received));
        var notes = form.Text("Observações", EditorText(row, "Observacoes"), true);
        form.Show(id == 0 ? "Nova receita" : "Editar receita", () => _service.SaveIncome(new IncomeInput
        {
            Id = id,
            Description = description.Text ?? "",
            Amount = amount.Value,
            ReceivedDate = date.Value,
            CategoryId = category.Value.Id,
            AccountId = account.Value.Id,
            PaymentMethod = method.Text ?? "",
            Status = status.Value,
            Notes = notes.Text ?? ""
        }), id == 0 ? null : () => _service.DeleteIncome(id));
    }

    private void EditExpense(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var description = form.Text("Descrição", EditorText(row, "Descricao"));
        var amount = form.Money("Valor (R$)", EditorMoney(row, "Valor"));
        var date = form.Date("Data da despesa", EditorDate(row, "Data", DateTime.Today));
        var due = form.Date("Vencimento", EditorDate(row, "Vencimento", DateTime.Today));
        var category = form.Lookup("Categoria", EditorCategories(row, FinanceTypes.Expense), EditorText(row, "Categoria"));
        var account = form.Lookup("Conta de pagamento", EditorAccounts(row), EditorText(row, "Conta"));
        var method = form.Text("Forma de pagamento", EditorText(row, "Forma"));
        var status = form.Choice("Status", _service.GetStatuses("expense").Where(value => value.Length > 0), EditorText(row, "Status", FinanceStatus.Pending));
        var notes = form.Text("Observações", EditorText(row, "Observacoes"), true);
        if (!string.IsNullOrEmpty(EditorText(row, "Parcela"))) form.ReadOnly("Parcela", EditorText(row, "Parcela"));
        form.Show(id == 0 ? "Nova despesa" : "Editar despesa", () => _service.SaveExpense(new ExpenseInput
        {
            Id = id,
            Description = description.Text ?? "",
            Amount = amount.Value,
            ExpenseDate = date.Value,
            DueDate = due.Value,
            CategoryId = category.Value.Id,
            AccountId = EditorOptionalId(account.Value),
            CreditCardId = null,
            PaymentMethod = method.Text ?? "",
            Status = status.Value,
            Notes = notes.Text ?? ""
        }), id == 0 ? null : () => _service.DeleteExpense(id));
    }

    private void EditCard(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var name = form.Text("Nome", EditorText(row, "Nome"));
        var institution = form.Text("Instituição", EditorText(row, "Instituicao"));
        var limit = form.Money("Limite total (R$)", EditorMoney(row, "LimiteTotal"));
        var closing = form.Integer("Dia do fechamento", EditorInteger(row, "Fechamento", 1));
        var due = form.Integer("Dia do vencimento", EditorInteger(row, "Vencimento", 10));
        var active = form.Check("Cartão ativo", row is null || EditorText(row, "Status") == "Ativo");
        form.Show(id == 0 ? "Novo cartão" : "Editar cartão", () => _service.SaveCreditCard(new CreditCardInput
        {
            Id = id,
            Name = name.Text ?? "",
            Institution = institution.Text ?? "",
            TotalLimit = limit.Value,
            ClosingDay = closing.Value,
            DueDay = due.Value,
            IsActive = active.Checked
        }), id == 0 ? null : () => _service.DeleteCreditCard(id));
    }

    private void EditPurchase(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var description = form.Text("Descrição", EditorText(row, "Descricao"));
        var amount = form.Money(id == 0 ? "Valor total (R$)" : "Valor da compra / parcela (R$)", EditorMoney(row, "Valor"));
        var date = form.Date("Data da compra", EditorDate(row, "Data", DateTime.Today));
        var card = form.Lookup("Cartão", EditorCards(row), EditorText(row, "Cartao"));
        var category = form.Lookup("Categoria", EditorCategories(row, FinanceTypes.Expense), EditorText(row, "Categoria"));
        var count = id == 0 ? form.Integer("Número de parcelas", 1) : null;
        if (id != 0)
        {
            form.ReadOnly("Parcela", EditorText(row, "Parcela"));
            form.ReadOnly("Fatura", EditorMonthLabel(EditorText(row, "Fatura")));
            form.ReadOnly("Status", EditorText(row, "Status"));
        }
        var notes = form.Text("Observações", EditorText(row, "Observacoes"), true);
        form.Show(id == 0 ? "Nova compra no cartão" : "Editar compra no cartão", () => _service.SaveCardPurchase(new CardPurchaseInput
        {
            Id = id,
            Description = description.Text ?? "",
            Amount = amount.Value,
            PurchaseDate = date.Value,
            CreditCardId = card.Value.Id,
            CategoryId = category.Value.Id,
            // Editing one purchase must never generate a new installment group.
            InstallmentCount = id == 0 ? count!.Value : 1,
            Notes = notes.Text ?? ""
        }), id == 0 ? null : () => _service.DeleteCardPurchase(id));
    }

    private void EditBudget(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var month = form.Date("Mês", EditorDate(row, "Mes", _month), true);
        var category = form.Lookup("Categoria", EditorCategories(row, FinanceTypes.Expense), EditorText(row, "Categoria"));
        var limit = form.Money("Limite (R$)", EditorMoney(row, "Limite"));
        form.Show(id == 0 ? "Novo orçamento" : "Editar orçamento", () => _service.SaveBudget(new BudgetInput
        {
            Id = id,
            Month = month.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            CategoryId = category.Value.Id,
            LimitAmount = limit.Value
        }), id == 0 ? null : () => _service.DeleteBudget(id));
    }

    private void EditGoal(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var name = form.Text("Nome", EditorText(row, "Nome"));
        var target = form.Money("Valor objetivo (R$)", EditorMoney(row, "ValorObjetivo"));
        var current = form.Money("Valor atual (R$)", EditorMoney(row, "ValorAtual"));
        var date = form.Date("Data alvo", EditorDate(row, "DataAlvo", DateTime.Today.AddMonths(6)));
        var description = form.Text("Descrição", EditorText(row, "Descricao"), true);
        var status = form.Choice("Status", new[] { FinanceStatus.Active, FinanceStatus.Finished }, EditorText(row, "Status", FinanceStatus.Active));
        form.Show(id == 0 ? "Nova meta" : "Editar meta", () => _service.SaveGoal(new GoalInput
        {
            Id = id,
            Name = name.Text ?? "",
            TargetAmount = target.Value,
            CurrentAmount = current.Value,
            TargetDate = date.Value,
            Description = description.Text ?? "",
            Status = status.Value
        }), id == 0 ? null : () => _service.DeleteGoal(id));
    }

    private void EditRecurring(DataRow? row = null)
    {
        var id = EditorId(row);
        var form = new EntryForm(_ui, RefreshPage);
        var description = form.Text("Descrição", EditorText(row, "Descricao"));
        var amount = form.Money("Valor (R$)", EditorMoney(row, "Valor"));
        var day = form.Integer("Dia do vencimento", EditorInteger(row, "Dia", DateTime.Today.Day));
        var category = form.Lookup("Categoria", EditorCategories(row, FinanceTypes.Expense), EditorText(row, "Categoria"));
        var account = form.Lookup("Conta", EditorAccounts(row), EditorText(row, "Conta"));
        var method = form.Text("Forma de pagamento", EditorText(row, "Forma"));
        var start = form.Date("Início", EditorDate(row, "Inicio", DateTime.Today));
        var hasEnd = form.Check("Definir data final", !string.IsNullOrWhiteSpace(EditorText(row, "Fim")));
        var end = form.Date("Fim", EditorDate(row, "Fim", start.Value.AddYears(1)));
        end.Visible = hasEnd.Checked;
        hasEnd.CheckedChange += (_, _) => end.Visible = hasEnd.Checked;
        var active = form.Check("Recorrência ativa", row is null || EditorText(row, "Status") == "Ativa");
        var notes = form.Text("Observações", EditorText(row, "Observacoes"), true);
        form.Show(id == 0 ? "Nova recorrência" : "Editar recorrência", () => _service.SaveRecurringExpense(new RecurringExpenseInput
        {
            Id = id,
            Description = description.Text ?? "",
            Amount = amount.Value,
            DayOfMonth = day.Value,
            CategoryId = category.Value.Id,
            AccountId = account.Value.Id,
            PaymentMethod = method.Text ?? "",
            StartDate = start.Value,
            EndDate = hasEnd.Checked ? end.Value : null,
            IsActive = active.Checked,
            Notes = notes.Text ?? ""
        }), id == 0 ? null : () => _service.DeleteRecurringExpense(id));
    }

    private void NewInstallment()
    {
        var form = new EntryForm(_ui, RefreshPage);
        var description = form.Text("Descrição");
        var total = form.Money("Valor total (R$)");
        var count = form.Integer("Número de parcelas", 2);
        var first = form.Date("Primeiro vencimento", DateTime.Today);
        var category = form.Lookup("Categoria", _service.GetCategories(FinanceTypes.Expense));
        var account = form.Lookup("Conta", _service.GetAccounts());
        var card = form.Lookup("Cartão", _service.GetCreditCards());
        var method = form.Text("Forma de pagamento", "Parcelado");
        var notes = form.Text("Observações", multiline: true);
        form.Show("Novo parcelamento", () => _service.CreateInstallments(new InstallmentInput
        {
            Description = description.Text ?? "",
            TotalAmount = total.Value,
            InstallmentCount = count.Value,
            FirstDueDate = first.Value,
            CategoryId = category.Value.Id,
            AccountId = EditorOptionalId(account.Value),
            CreditCardId = EditorOptionalId(card.Value),
            PaymentMethod = method.Text ?? "",
            Notes = notes.Text ?? ""
        }));
    }

    private void AddContribution(int goalId)
    {
        var form = new EntryForm(_ui, RefreshPage);
        var goal = _service.SearchGoals("").Rows.Cast<DataRow>().FirstOrDefault(row => EditorId(row) == goalId);
        if (goal is not null) form.ReadOnly("Meta", EditorText(goal, "Nome"));
        var amount = form.Money("Valor do aporte (R$)");
        var date = form.Date("Data do aporte", DateTime.Today);
        var notes = form.Text("Observações", multiline: true);
        form.Show("Adicionar aporte", () => _service.AddGoalContribution(goalId, amount.Value, date.Value, notes.Text ?? ""));
    }

    private void CloseInvoice(int cardId)
    {
        var form = new EntryForm(_ui, RefreshPage);
        var card = _service.SearchCreditCards("").Rows.Cast<DataRow>().FirstOrDefault(row => EditorId(row) == cardId);
        if (card is not null) form.ReadOnly("Cartão", EditorText(card, "Nome"));
        var month = form.Date("Mês da fatura", _month, true);
        form.Show("Fechar fatura", () => _service.CloseInvoice(cardId, month.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
    }

    private void PayInvoice(DataRow row)
    {
        var form = new EntryForm(_ui, RefreshPage);
        var id = EditorId(row);
        form.ReadOnly("Cartão", EditorText(row, "Cartao"));
        form.ReadOnly("Mês", EditorMonthLabel(EditorText(row, "Mes")));
        form.ReadOnly("Valor", EditorMoney(row, "Valor").ToString("C2", FinancialFormatting.BrazilianCulture));
        form.ReadOnly("Vencimento", EditorDate(row, "Vencimento", DateTime.Today).ToString("dd/MM/yyyy", FinancialFormatting.BrazilianCulture));
        var account = form.Lookup("Conta de pagamento", EditorAccounts(row, "ContaPagamento"), EditorText(row, "ContaPagamento"));
        form.Show("Pagar fatura", () => _service.PayInvoice(id, account.Value.Id));
    }

    private static object? EditorValue(DataRow? row, string column) =>
        row is not null && row.Table.Columns.Contains(column) && !row.IsNull(column) ? row[column] : null;

    private static string EditorText(DataRow? row, string column, string fallback = "") =>
        EditorValue(row, column) is { } value ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private static decimal EditorMoney(DataRow? row, string column) =>
        EditorValue(row, column) is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : 0;

    private static int EditorInteger(DataRow? row, string column, int fallback = 0) =>
        EditorValue(row, column) is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;

    private static int EditorId(DataRow? row) => EditorInteger(row, "Id");
    private static int? EditorOptionalId(LookupItem item) => item.Id > 0 ? item.Id : null;

    private static DateTime EditorDate(DataRow? row, string column, DateTime fallback)
    {
        if (EditorValue(row, column) is DateTime date) return date.Date;
        var text = EditorText(row, column);
        if (DateTime.TryParseExact(text, new[] { "yyyy-MM-dd", "yyyy-MM", "yyyy-MM-dd HH:mm:ss", "O" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return date.Date;
        return fallback.Date;
    }

    private static string EditorMonthLabel(string month) =>
        DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("MM/yyyy", FinancialFormatting.BrazilianCulture)
            : month;

    private List<LookupItem> EditorAccounts(DataRow? row, string column = "Conta")
    {
        var items = _service.GetAccounts();
        var name = EditorText(row, column);
        if (name.Length > 0 && items.All(item => item.Name != name))
            EditorIncludeExisting(items, _service.SearchAccounts(""), name);
        return items;
    }

    private List<LookupItem> EditorCategories(DataRow? row, string type)
    {
        var items = _service.GetCategories(type);
        var name = EditorText(row, "Categoria");
        if (name.Length > 0 && items.All(item => item.Name != name))
            EditorIncludeExisting(items, _service.SearchCategories("", type), name);
        return items;
    }

    private List<LookupItem> EditorCards(DataRow? row)
    {
        var items = _service.GetCreditCards();
        var name = EditorText(row, "Cartao");
        if (name.Length > 0 && items.All(item => item.Name != name))
            EditorIncludeExisting(items, _service.SearchCreditCards(""), name);
        return items;
    }

    // Search aliases carry names, not foreign keys. Restore an inactive existing link without selecting another record.
    private static void EditorIncludeExisting(List<LookupItem> items, DataTable table, string name)
    {
        var existing = table.Rows.Cast<DataRow>().FirstOrDefault(row => EditorText(row, "Nome") == name);
        if (existing is not null) items.Add(new LookupItem(EditorId(existing), name));
    }
}
