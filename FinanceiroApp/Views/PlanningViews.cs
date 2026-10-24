using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class RecurringExpensesView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar recorrencia");
    private readonly TextBox _description = Ui.TextBox();
    private readonly TextBox _amount = Ui.TextBox("0,00");
    private readonly NumericUpDown _day = Ui.NumberBox(1, 31);
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly ComboBox _account = Ui.ComboBox();
    private readonly TextBox _method = Ui.TextBox("Boleto, debito...");
    private readonly DateTimePicker _start = Ui.DatePicker();
    private readonly DateTimePicker _end = Ui.DatePicker();
    private readonly CheckBox _hasEnd = new() { Text = "Tem data final", AutoSize = true, Font = Ui.BodyFont };
    private readonly CheckBox _active = new() { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };
    private readonly TextBox _notes = Ui.TextBox();
    private readonly NumericUpDown _monthsAhead = Ui.NumberBox(0, 24);
    private int _id;

    public RecurringExpensesView(FinanceService service) : base(service, "Despesas recorrentes")
    {
        _day.Value = 5;
        _monthsAhead.Value = 3;
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        Ui.BindGrid(_grid, Service.SearchRecurringExpenses(_search.Text));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(Ui.Button("Pesquisar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => { _search.Clear(); RefreshData(); }));
        toolbar.Controls.Add(Ui.Label("Gerar meses"));
        toolbar.Controls.Add(_monthsAhead);
        toolbar.Controls.Add(Ui.Button("Gerar", (_, _) => Run(Generate, RefreshData), true));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", _description);
        Ui.AddField(form, "Valor", _amount);
        Ui.AddField(form, "Dia do mes", _day);
        Ui.AddField(form, "Categoria", _category);
        Ui.AddField(form, "Conta", _account);
        Ui.AddField(form, "Forma", _method);
        Ui.AddField(form, "Inicio", _start);
        Ui.AddField(form, "Fim opcional", _end);
        Ui.AddField(form, "Usar fim", _hasEnd);
        Ui.AddField(form, "Status", _active);
        Ui.AddField(form, "Observacoes", _notes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Recorrencia mensal", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void LoadLookups()
    {
        var cat = Ui.SelectedId(_category);
        var acc = Ui.SelectedId(_account);
        Ui.BindLookup(_category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_account, Service.GetAccounts());
        if (cat > 0) Ui.SelectLookup(_category, cat);
        if (acc > 0) Ui.SelectLookup(_account, acc);
    }

    private void Save()
    {
        Service.SaveRecurringExpense(new RecurringExpenseInput
        {
            Id = _id,
            Description = _description.Text,
            Amount = Ui.ParseMoney(_amount, "valor"),
            DayOfMonth = (int)_day.Value,
            CategoryId = Ui.SelectedId(_category),
            AccountId = Ui.SelectedId(_account),
            PaymentMethod = _method.Text,
            StartDate = _start.Value.Date,
            EndDate = _hasEnd.Checked ? _end.Value.Date : null,
            IsActive = _active.Checked,
            Notes = _notes.Text
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var description = Ui.TextBox();
        var amount = Ui.TextBox("0,00");
        amount.Text = "0,00";
        var day = Ui.NumberBox(1, 31);
        day.Value = 5;
        var category = Ui.ComboBox();
        var account = Ui.ComboBox();
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(account, Service.GetAccounts());
        var method = Ui.TextBox("Boleto, debito...");
        var start = Ui.DatePicker();
        start.Value = DateTime.Today;
        var end = Ui.DatePicker();
        end.Value = DateTime.Today.AddYears(1);
        var hasEnd = new CheckBox { Text = "Tem data final", AutoSize = true, Font = Ui.BodyFont };
        var active = new CheckBox { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Valor", amount);
        Ui.AddField(form, "Dia do mes", day);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Conta", account);
        Ui.AddField(form, "Forma", method);
        Ui.AddField(form, "Inicio", start);
        Ui.AddField(form, "Fim opcional", end);
        Ui.AddField(form, "Usar fim", hasEnd);
        Ui.AddField(form, "Status", active);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Nova despesa recorrente",
            form,
            () => Service.SaveRecurringExpense(new RecurringExpenseInput
            {
                Description = description.Text,
                Amount = Ui.ParseMoney(amount, "valor"),
                DayOfMonth = (int)day.Value,
                CategoryId = Ui.SelectedId(category),
                AccountId = Ui.SelectedId(account),
                PaymentMethod = method.Text,
                StartDate = start.Value.Date,
                EndDate = hasEnd.Checked ? end.Value.Date : null,
                IsActive = active.Checked,
                Notes = notes.Text
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma recorrencia.");
        }

        if (MessageBox.Show("Excluir esta recorrencia? Lancamentos ja gerados serao preservados.", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteRecurringExpense(_id);
            ClearForm();
        }
    }

    private void Generate()
    {
        var count = Service.GenerateRecurringExpenses((int)_monthsAhead.Value);
        Ui.ShowInfo($"{count} lancamento(s) recorrente(s) gerado(s).");
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
        _day.Value = Math.Clamp(int.TryParse(Ui.CellText(_grid, "Dia"), out var day) ? day : 1, 1, 31);
        Ui.SelectLookupByName(_category, Ui.CellText(_grid, "Categoria"));
        Ui.SelectLookupByName(_account, Ui.CellText(_grid, "Conta"));
        _method.Text = Ui.CellText(_grid, "Forma");
        _start.Value = Ui.CellDate(_grid, "Inicio");
        var endText = Ui.CellText(_grid, "Fim");
        _hasEnd.Checked = DateTime.TryParse(endText, out var end);
        if (_hasEnd.Checked)
        {
            _end.Value = end;
        }
        _active.Checked = Ui.CellText(_grid, "Status") == "Ativa";
        _notes.Text = Ui.CellText(_grid, "Observacoes");
    }

    private void ClearForm()
    {
        _id = 0;
        _description.Clear();
        _amount.Text = "0,00";
        _day.Value = 5;
        _method.Clear();
        _start.Value = DateTime.Today;
        _end.Value = DateTime.Today.AddYears(1);
        _hasEnd.Checked = false;
        _active.Checked = true;
        _notes.Clear();
        if (_category.Items.Count > 0) _category.SelectedIndex = 0;
        if (_account.Items.Count > 0) _account.SelectedIndex = 0;
    }
}

public sealed class InstallmentsView : BaseView
{
    private readonly DataGridView _groups = Ui.Grid();
    private readonly DataGridView _details = Ui.Grid();
    private readonly TextBox _description = Ui.TextBox();
    private readonly TextBox _amount = Ui.TextBox("0,00");
    private readonly NumericUpDown _count = Ui.NumberBox(1, 120);
    private readonly DateTimePicker _firstDue = Ui.DatePicker();
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly ComboBox _account = Ui.ComboBox();
    private readonly ComboBox _card = Ui.ComboBox();
    private readonly TextBox _method = Ui.TextBox("Parcelado");
    private readonly TextBox _notes = Ui.TextBox();

    public InstallmentsView(FinanceService service) : base(service, "Parcelamentos")
    {
        _count.Value = 2;
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        Ui.BindGrid(_groups, Service.SearchInstallmentGroups());
        LoadDetails();
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };
        left.Panel1.Controls.Add(PanelWithGrid("Parcelamentos", _groups));
        left.Panel2.Controls.Add(PanelWithGrid("Parcelas vinculadas", _details));

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", _description);
        Ui.AddField(form, "Valor total", _amount);
        Ui.AddField(form, "Parcelas", _count);
        Ui.AddField(form, "Primeiro vencimento", _firstDue);
        Ui.AddField(form, "Categoria", _category);
        Ui.AddField(form, "Conta", _account);
        Ui.AddField(form, "Cartao", _card);
        Ui.AddField(form, "Forma", _method);
        Ui.AddField(form, "Observacoes", _notes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Gerar parcelas", (_, _) => RunSave(Create, RefreshData, "Parcelamento gerado com sucesso."), true));
        actions.Controls.Add(Ui.Button("Novo parcelamento", (_, _) => OpenNewDialog()));
        actions.Controls.Add(Ui.Button("Limpar", (_, _) => ClearForm()));

        var hint = new Label
        {
            Text = "Escolha conta para parcelas como despesas ou cartao para compras parceladas.",
            Dock = DockStyle.Top,
            Height = 48,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted
        };

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(hint);
        formPanel.Controls.Add(new Label { Text = "Novo parcelamento", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(left);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _groups.SelectionChanged += (_, _) => LoadDetails();
    }

    private static Control PanelWithGrid(string title, DataGridView grid)
    {
        var panel = SurfacePanel();
        var label = new Label { Text = title, Dock = DockStyle.Top, Height = 30, Font = Ui.HeadingFont, ForeColor = Ui.Text };
        panel.Controls.Add(grid);
        panel.Controls.Add(label);
        return panel;
    }

    private void LoadLookups()
    {
        var cat = Ui.SelectedId(_category);
        var acc = Ui.SelectedId(_account);
        var card = Ui.SelectedId(_card);
        Ui.BindLookup(_category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_account, Service.GetAccounts());
        Ui.BindLookup(_card, Service.GetCreditCards());
        if (cat > 0) Ui.SelectLookup(_category, cat);
        if (acc > 0) Ui.SelectLookup(_account, acc);
        if (card > 0) Ui.SelectLookup(_card, card);
    }

    private void Create()
    {
        var accountId = Ui.SelectedId(_account);
        var cardId = Ui.SelectedId(_card);
        Service.CreateInstallments(new InstallmentInput
        {
            Description = _description.Text,
            TotalAmount = Ui.ParseMoney(_amount, "valor total"),
            InstallmentCount = (int)_count.Value,
            FirstDueDate = _firstDue.Value.Date,
            CategoryId = Ui.SelectedId(_category),
            AccountId = cardId > 0 ? null : accountId,
            CreditCardId = cardId > 0 ? cardId : null,
            PaymentMethod = _method.Text,
            Notes = _notes.Text
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var description = Ui.TextBox();
        var amount = Ui.TextBox("0,00");
        amount.Text = "0,00";
        var count = Ui.NumberBox(1, 120);
        count.Value = 2;
        var firstDue = Ui.DatePicker();
        firstDue.Value = DateTime.Today;
        var category = Ui.ComboBox();
        var account = Ui.ComboBox();
        var card = Ui.ComboBox();
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(account, Service.GetAccounts());
        Ui.BindLookup(card, Service.GetCreditCards());
        var method = Ui.TextBox("Parcelado");
        method.Text = "Parcelado";
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Valor total", amount);
        Ui.AddField(form, "Parcelas", count);
        Ui.AddField(form, "Primeiro vencimento", firstDue);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Conta", account);
        Ui.AddField(form, "Cartao", card);
        Ui.AddField(form, "Forma", method);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Novo parcelamento",
            form,
            () =>
            {
                var accountId = Ui.SelectedId(account);
                var cardId = Ui.SelectedId(card);
                Service.CreateInstallments(new InstallmentInput
                {
                    Description = description.Text,
                    TotalAmount = Ui.ParseMoney(amount, "valor total"),
                    InstallmentCount = (int)count.Value,
                    FirstDueDate = firstDue.Value.Date,
                    CategoryId = Ui.SelectedId(category),
                    AccountId = cardId > 0 ? null : accountId,
                    CreditCardId = cardId > 0 ? cardId : null,
                    PaymentMethod = method.Text,
                    Notes = notes.Text
                });
            },
            () => { ClearForm(); RefreshData(); },
            "Parcelamento gerado com sucesso.");
    }

    private void LoadDetails()
    {
        var id = Ui.SelectedGridId(_groups);
        if (id > 0)
        {
            Ui.BindGrid(_details, Service.GetInstallmentDetails(id));
        }
    }

    private void ClearForm()
    {
        _description.Clear();
        _amount.Text = "0,00";
        _count.Value = 2;
        _firstDue.Value = DateTime.Today;
        _method.Text = "Parcelado";
        _notes.Clear();
        if (_category.Items.Count > 0) _category.SelectedIndex = 0;
        if (_account.Items.Count > 0) _account.SelectedIndex = 0;
        if (_card.Items.Count > 0) _card.SelectedIndex = 0;
    }
}
