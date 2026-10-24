using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class IncomesView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar receita");
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly ComboBox _filterCategory = Ui.ComboBox();
    private readonly ComboBox _filterAccount = Ui.ComboBox();
    private readonly ComboBox _filterStatus = Ui.ComboBox();
    private readonly ComboBox _sort = Ui.ComboBox();

    private readonly TextBox _description = Ui.TextBox();
    private readonly TextBox _amount = Ui.TextBox("0,00");
    private readonly DateTimePicker _date = Ui.DatePicker();
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly ComboBox _account = Ui.ComboBox();
    private readonly TextBox _method = Ui.TextBox("PIX, dinheiro...");
    private readonly ComboBox _status = Ui.ComboBox();
    private readonly TextBox _notes = Ui.TextBox();
    private int _id;

    public IncomesView(FinanceService service) : base(service, "Receitas")
    {
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today;
        _sort.Items.AddRange(new object[] { "Data", "Valor", "Descricao" });
        _sort.SelectedIndex = 0;
        _status.Items.AddRange(new object[] { FinanceStatus.Received, FinanceStatus.Pending });
        _status.SelectedIndex = 0;
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        var filter = new ReportFilter
        {
            StartDate = _from.Value.Date,
            EndDate = _to.Value.Date,
            CategoryId = Ui.SelectedId(_filterCategory),
            AccountId = Ui.SelectedId(_filterAccount),
            Status = Convert.ToString(_filterStatus.SelectedItem)
        };
        Ui.BindGrid(_grid, Service.SearchIncomes(filter, _search.Text, Convert.ToString(_sort.SelectedItem) ?? "Data"));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(Ui.Label("De"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(Ui.Label("Ate"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(_filterCategory);
        toolbar.Controls.Add(_filterAccount);
        toolbar.Controls.Add(_filterStatus);
        toolbar.Controls.Add(_sort);
        toolbar.Controls.Add(Ui.Button("Filtrar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => ClearFilters()));
        toolbar.Controls.Add(Ui.Button("Exportar CSV", (_, _) => Run(Export)));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", _description);
        Ui.AddField(form, "Valor", _amount);
        Ui.AddField(form, "Data", _date);
        Ui.AddField(form, "Categoria", _category);
        Ui.AddField(form, "Conta", _account);
        Ui.AddField(form, "Forma", _method);
        Ui.AddField(form, "Status", _status);
        Ui.AddField(form, "Observacoes", _notes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Lancamento", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void LoadLookups()
    {
        var cat = Ui.SelectedId(_category);
        var acc = Ui.SelectedId(_account);
        var fcat = Ui.SelectedId(_filterCategory);
        var facc = Ui.SelectedId(_filterAccount);
        Ui.BindLookup(_category, Service.GetCategories(FinanceTypes.Income));
        Ui.BindLookup(_account, Service.GetAccounts());
        Ui.BindLookup(_filterCategory, Service.GetCategories(FinanceTypes.Income));
        Ui.BindLookup(_filterAccount, Service.GetAccounts());
        if (cat > 0) Ui.SelectLookup(_category, cat);
        if (acc > 0) Ui.SelectLookup(_account, acc);
        if (fcat > 0) Ui.SelectLookup(_filterCategory, fcat);
        if (facc > 0) Ui.SelectLookup(_filterAccount, facc);
        var currentStatus = Convert.ToString(_filterStatus.SelectedItem) ?? "";
        _filterStatus.Items.Clear();
        _filterStatus.Items.AddRange(Service.GetStatuses("income").Cast<object>().ToArray());
        _filterStatus.SelectedItem = currentStatus;
        if (_filterStatus.SelectedIndex < 0) _filterStatus.SelectedIndex = 0;
    }

    private void Save()
    {
        Service.SaveIncome(new IncomeInput
        {
            Id = _id,
            Description = _description.Text,
            Amount = Ui.ParseMoney(_amount, "valor"),
            ReceivedDate = _date.Value.Date,
            CategoryId = Ui.SelectedId(_category),
            AccountId = Ui.SelectedId(_account),
            PaymentMethod = _method.Text,
            Notes = _notes.Text,
            Status = Convert.ToString(_status.SelectedItem) ?? FinanceStatus.Received
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var description = Ui.TextBox();
        var amount = Ui.TextBox("0,00");
        amount.Text = "0,00";
        var date = Ui.DatePicker();
        date.Value = DateTime.Today;
        var category = Ui.ComboBox();
        var account = Ui.ComboBox();
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Income));
        Ui.BindLookup(account, Service.GetAccounts());
        var method = Ui.TextBox("PIX, dinheiro...");
        var status = Ui.ComboBox();
        status.Items.AddRange(new object[] { FinanceStatus.Received, FinanceStatus.Pending });
        status.SelectedItem = FinanceStatus.Received;
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Valor", amount);
        Ui.AddField(form, "Data", date);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Conta", account);
        Ui.AddField(form, "Forma", method);
        Ui.AddField(form, "Status", status);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Nova receita",
            form,
            () => Service.SaveIncome(new IncomeInput
            {
                Description = description.Text,
                Amount = Ui.ParseMoney(amount, "valor"),
                ReceivedDate = date.Value.Date,
                CategoryId = Ui.SelectedId(category),
                AccountId = Ui.SelectedId(account),
                PaymentMethod = method.Text,
                Notes = notes.Text,
                Status = Convert.ToString(status.SelectedItem) ?? FinanceStatus.Received
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma receita.");
        }

        if (MessageBox.Show("Excluir esta receita?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteIncome(_id);
            ClearForm();
        }
    }

    private void FillForm()
    {
        _id = Ui.SelectedGridId(_grid);
        if (_id == 0)
        {
            return;
        }

        _description.Text = Ui.CellText(_grid, "Descricao");
        _amount.Text = Ui.CellMoney(_grid, "Valor").ToString("0.00");
        _date.Value = Ui.CellDate(_grid, "Data");
        Ui.SelectLookupByName(_category, Ui.CellText(_grid, "Categoria"));
        Ui.SelectLookupByName(_account, Ui.CellText(_grid, "Conta"));
        _method.Text = Ui.CellText(_grid, "Forma");
        _status.SelectedItem = Ui.CellText(_grid, "Status");
        _notes.Text = Ui.CellText(_grid, "Observacoes");
    }

    private void ClearForm()
    {
        _id = 0;
        _description.Clear();
        _amount.Text = "0,00";
        _date.Value = DateTime.Today;
        _method.Clear();
        _notes.Clear();
        if (_category.Items.Count > 0) _category.SelectedIndex = 0;
        if (_account.Items.Count > 0) _account.SelectedIndex = 0;
        _status.SelectedItem = FinanceStatus.Received;
    }

    private void ClearFilters()
    {
        _search.Clear();
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today;
        if (_filterCategory.Items.Count > 0) _filterCategory.SelectedIndex = 0;
        if (_filterAccount.Items.Count > 0) _filterAccount.SelectedIndex = 0;
        if (_filterStatus.Items.Count > 0) _filterStatus.SelectedIndex = 0;
        _sort.SelectedIndex = 0;
        RefreshData();
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "receitas.csv" };
        if (dialog.ShowDialog() == DialogResult.OK && Ui.BoundTable(_grid) is { } table)
        {
            Service.ExportTableToCsv(table, dialog.FileName);
            Ui.ShowInfo("Arquivo exportado.");
        }
    }
}

public sealed class ExpensesView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar despesa");
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly ComboBox _filterCategory = Ui.ComboBox();
    private readonly ComboBox _filterAccount = Ui.ComboBox();
    private readonly ComboBox _filterStatus = Ui.ComboBox();
    private readonly ComboBox _sort = Ui.ComboBox();

    private readonly TextBox _description = Ui.TextBox();
    private readonly TextBox _amount = Ui.TextBox("0,00");
    private readonly DateTimePicker _expenseDate = Ui.DatePicker();
    private readonly DateTimePicker _dueDate = Ui.DatePicker();
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly ComboBox _account = Ui.ComboBox();
    private readonly TextBox _method = Ui.TextBox("Debito, boleto...");
    private readonly ComboBox _status = Ui.ComboBox();
    private readonly TextBox _notes = Ui.TextBox();
    private int _id;

    public ExpensesView(FinanceService service) : base(service, "Despesas")
    {
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today.AddMonths(1);
        _sort.Items.AddRange(new object[] { "Data", "Valor", "Descricao" });
        _sort.SelectedIndex = 0;
        _status.Items.AddRange(new object[] { FinanceStatus.Pending, FinanceStatus.Paid, FinanceStatus.Overdue });
        _status.SelectedIndex = 0;
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        var filter = new ReportFilter
        {
            StartDate = _from.Value.Date,
            EndDate = _to.Value.Date,
            CategoryId = Ui.SelectedId(_filterCategory),
            AccountId = Ui.SelectedId(_filterAccount),
            Status = Convert.ToString(_filterStatus.SelectedItem)
        };
        Ui.BindGrid(_grid, Service.SearchExpenses(filter, _search.Text, Convert.ToString(_sort.SelectedItem) ?? "Data"));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(Ui.Label("De"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(Ui.Label("Ate"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(_filterCategory);
        toolbar.Controls.Add(_filterAccount);
        toolbar.Controls.Add(_filterStatus);
        toolbar.Controls.Add(_sort);
        toolbar.Controls.Add(Ui.Button("Filtrar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => ClearFilters()));
        toolbar.Controls.Add(Ui.Button("Exportar CSV", (_, _) => Run(Export)));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", _description);
        Ui.AddField(form, "Valor", _amount);
        Ui.AddField(form, "Data", _expenseDate);
        Ui.AddField(form, "Vencimento", _dueDate);
        Ui.AddField(form, "Categoria", _category);
        Ui.AddField(form, "Conta", _account);
        Ui.AddField(form, "Forma", _method);
        Ui.AddField(form, "Status", _status);
        Ui.AddField(form, "Observacoes", _notes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var hint = new Label
        {
            Text = "Para compras parceladas use a tela Parcelamentos.",
            Dock = DockStyle.Top,
            Height = 42,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted
        };

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(hint);
        formPanel.Controls.Add(new Label { Text = "Lancamento", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void LoadLookups()
    {
        var cat = Ui.SelectedId(_category);
        var acc = Ui.SelectedId(_account);
        var fcat = Ui.SelectedId(_filterCategory);
        var facc = Ui.SelectedId(_filterAccount);
        Ui.BindLookup(_category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_account, Service.GetAccounts());
        Ui.BindLookup(_filterCategory, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_filterAccount, Service.GetAccounts());
        if (cat > 0) Ui.SelectLookup(_category, cat);
        if (acc > 0) Ui.SelectLookup(_account, acc);
        if (fcat > 0) Ui.SelectLookup(_filterCategory, fcat);
        if (facc > 0) Ui.SelectLookup(_filterAccount, facc);
        var currentStatus = Convert.ToString(_filterStatus.SelectedItem) ?? "";
        _filterStatus.Items.Clear();
        _filterStatus.Items.AddRange(Service.GetStatuses("expense").Cast<object>().ToArray());
        _filterStatus.SelectedItem = currentStatus;
        if (_filterStatus.SelectedIndex < 0) _filterStatus.SelectedIndex = 0;
    }

    private void Save()
    {
        Service.SaveExpense(new ExpenseInput
        {
            Id = _id,
            Description = _description.Text,
            Amount = Ui.ParseMoney(_amount, "valor"),
            ExpenseDate = _expenseDate.Value.Date,
            DueDate = _dueDate.Value.Date,
            CategoryId = Ui.SelectedId(_category),
            AccountId = Ui.SelectedId(_account),
            PaymentMethod = _method.Text,
            Notes = _notes.Text,
            Status = Convert.ToString(_status.SelectedItem) ?? FinanceStatus.Pending
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var description = Ui.TextBox();
        var amount = Ui.TextBox("0,00");
        amount.Text = "0,00";
        var expenseDate = Ui.DatePicker();
        expenseDate.Value = DateTime.Today;
        var dueDate = Ui.DatePicker();
        dueDate.Value = DateTime.Today;
        var category = Ui.ComboBox();
        var account = Ui.ComboBox();
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(account, Service.GetAccounts());
        var method = Ui.TextBox("Debito, boleto...");
        var status = Ui.ComboBox();
        status.Items.AddRange(new object[] { FinanceStatus.Pending, FinanceStatus.Paid, FinanceStatus.Overdue });
        status.SelectedItem = FinanceStatus.Pending;
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Valor", amount);
        Ui.AddField(form, "Data", expenseDate);
        Ui.AddField(form, "Vencimento", dueDate);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Conta", account);
        Ui.AddField(form, "Forma", method);
        Ui.AddField(form, "Status", status);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Nova despesa",
            form,
            () => Service.SaveExpense(new ExpenseInput
            {
                Description = description.Text,
                Amount = Ui.ParseMoney(amount, "valor"),
                ExpenseDate = expenseDate.Value.Date,
                DueDate = dueDate.Value.Date,
                CategoryId = Ui.SelectedId(category),
                AccountId = Ui.SelectedId(account),
                PaymentMethod = method.Text,
                Notes = notes.Text,
                Status = Convert.ToString(status.SelectedItem) ?? FinanceStatus.Pending
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma despesa.");
        }

        if (MessageBox.Show("Excluir esta despesa?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteExpense(_id);
            ClearForm();
        }
    }

    private void FillForm()
    {
        _id = Ui.SelectedGridId(_grid);
        if (_id == 0)
        {
            return;
        }

        _description.Text = Ui.CellText(_grid, "Descricao");
        _amount.Text = Ui.CellMoney(_grid, "Valor").ToString("0.00");
        _expenseDate.Value = Ui.CellDate(_grid, "Data");
        _dueDate.Value = Ui.CellDate(_grid, "Vencimento");
        Ui.SelectLookupByName(_category, Ui.CellText(_grid, "Categoria"));
        Ui.SelectLookupByName(_account, Ui.CellText(_grid, "Conta"));
        _method.Text = Ui.CellText(_grid, "Forma");
        _status.SelectedItem = Ui.CellText(_grid, "Status");
        _notes.Text = Ui.CellText(_grid, "Observacoes");
    }

    private void ClearForm()
    {
        _id = 0;
        _description.Clear();
        _amount.Text = "0,00";
        _expenseDate.Value = DateTime.Today;
        _dueDate.Value = DateTime.Today;
        _method.Clear();
        _notes.Clear();
        if (_category.Items.Count > 0) _category.SelectedIndex = 0;
        if (_account.Items.Count > 0) _account.SelectedIndex = 0;
        _status.SelectedItem = FinanceStatus.Pending;
    }

    private void ClearFilters()
    {
        _search.Clear();
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today.AddMonths(1);
        if (_filterCategory.Items.Count > 0) _filterCategory.SelectedIndex = 0;
        if (_filterAccount.Items.Count > 0) _filterAccount.SelectedIndex = 0;
        if (_filterStatus.Items.Count > 0) _filterStatus.SelectedIndex = 0;
        _sort.SelectedIndex = 0;
        RefreshData();
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "despesas.csv" };
        if (dialog.ShowDialog() == DialogResult.OK && Ui.BoundTable(_grid) is { } table)
        {
            Service.ExportTableToCsv(table, dialog.FileName);
            Ui.ShowInfo("Arquivo exportado.");
        }
    }
}
