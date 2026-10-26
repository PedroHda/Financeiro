using System.ComponentModel;
using System.Data;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class BankingView : BaseView
{
    private readonly DataGridView _transfers = Ui.Grid();
    private readonly DataGridView _reconciliations = Ui.Grid();
    private readonly DataGridView _forecast = Ui.Grid();
    private readonly DataGridView _rules = Ui.Grid();
    private readonly DataGridView _preview = Ui.Grid();
    private readonly ComboBox _importAccount = Ui.ComboBox();
    private readonly ComboBox _incomeCategory = Ui.ComboBox();
    private readonly ComboBox _expenseCategory = Ui.ComboBox();
    private readonly Label _importSummary = Ui.Label("");
    private readonly NumericUpDown _months = new() { Minimum = 1, Maximum = 24, Value = 6, Width = 65 };
    private BindingList<ImportEntry> _entries = new();
    private int _previewAccount;

    public BankingView(FinanceService service) : base(service, "Operacoes e planejamento")
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.BodyFont };
        tabs.TabPages.Add(Page("Transferencias", _transfers, Ui.Button("Novo registro", (_, _) => EditTransfer()),
            Ui.Button("Editar", (_, _) => EditTransfer(true)), Ui.Button("Excluir", (_, _) => Run(() =>
            {
                var id = Ui.SelectedGridId(_transfers);
                if (id == 0) throw new InvalidOperationException("Selecione uma transferencia.");
                if (MessageBox.Show("Excluir esta transferencia?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Service.DeleteTransfer(id);
            }, RefreshData))));
        tabs.TabPages.Add(Page("Conciliacao", _reconciliations, Ui.Button("Conferir saldo", (_, _) => Reconcile())));
        tabs.TabPages.Add(BuildImport());
        tabs.TabPages.Add(Page("Previsao de saldo", _forecast, Ui.Label("Meses"), _months,
            Ui.Button("Atualizar", (_, _) => Run(LoadForecast))));
        tabs.TabPages.Add(Page("Regras de categorias", _rules, Ui.Button("Nova regra", (_, _) => EditRule()),
            Ui.Button("Excluir", (_, _) => Run(() =>
            {
                var id = Ui.SelectedGridId(_rules);
                if (id == 0) throw new InvalidOperationException("Selecione uma regra.");
                if (MessageBox.Show("Excluir esta regra?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Service.DeleteCategoryRule(id);
            }, RefreshData))));
        Content.Controls.Add(tabs);
    }

    public override void RefreshData()
    {
        Ui.BindGrid(_transfers, Service.SearchTransfers(DateTime.MinValue, DateTime.MaxValue));
        Ui.BindGrid(_reconciliations, Service.GetReconciliations());
        Ui.BindGrid(_rules, Service.GetCategoryRules());
        var selected = Ui.SelectedId(_importAccount);
        Ui.BindLookup(_importAccount, Service.GetAccounts());
        if (selected > 0) Ui.SelectLookup(_importAccount, selected);
        Ui.BindLookup(_incomeCategory, Service.GetCategories(FinanceTypes.Income));
        Ui.BindLookup(_expenseCategory, Service.GetCategories(FinanceTypes.Expense));
        LoadForecast();
    }

    private static TabPage Page(string name, Control grid, params Control[] actions)
    {
        var page = new TabPage(name) { Padding = new Padding(10), BackColor = Ui.Surface };
        var toolbar = Ui.Toolbar();
        toolbar.Controls.AddRange(actions);
        page.Controls.Add(grid);
        page.Controls.Add(toolbar);
        return page;
    }

    private void EditTransfer(bool editing = false)
    {
        var id = editing ? Ui.SelectedGridId(_transfers) : 0;
        if (editing && id == 0) { Ui.ShowError(new InvalidOperationException("Selecione uma transferencia.")); return; }
        var from = Ui.ComboBox();
        var to = Ui.ComboBox();
        Ui.BindLookup(from, Service.GetAccounts());
        Ui.BindLookup(to, Service.GetAccounts());
        var amount = Ui.TextBox();
        amount.Text = editing ? Ui.CellMoney(_transfers, "Valor").ToString("0.00") : "0,00";
        var date = Ui.DatePicker();
        date.Value = editing ? Ui.CellDate(_transfers, "Data") : DateTime.Today;
        var notes = Ui.TextBox();
        if (editing)
        {
            Ui.SelectLookupByName(from, Ui.CellText(_transfers, "Origem"));
            Ui.SelectLookupByName(to, Ui.CellText(_transfers, "Destino"));
            notes.Text = Ui.CellText(_transfers, "Observacoes");
        }
        var form = Ui.FormGrid();
        Ui.AddField(form, "Origem", from); Ui.AddField(form, "Destino", to);
        Ui.AddField(form, "Valor", amount); Ui.AddField(form, "Data", date); Ui.AddField(form, "Observacoes", notes);
        ShowEntryDialog(editing ? "Editar transferencia" : "Nova transferencia", form,
            () => Service.SaveTransfer(new TransferInput { Id = id, FromAccountId = Ui.SelectedId(from), ToAccountId = Ui.SelectedId(to), Amount = Ui.ParseMoney(amount, "valor"), Date = date.Value.Date, Notes = notes.Text }), RefreshData);
    }

    private void Reconcile()
    {
        var account = Ui.ComboBox(); Ui.BindLookup(account, Service.GetAccounts());
        var date = Ui.DatePicker();
        var amount = Ui.TextBox(); amount.Text = "0,00";
        var expected = Ui.Label("");
        var notes = Ui.TextBox();
        void UpdateBalance() => Run(() => expected.Text = Ui.SelectedId(account) > 0 ? Service.GetAccountBalanceAt(Ui.SelectedId(account), date.Value.Date).ToString("C2") : "");
        account.SelectedIndexChanged += (_, _) => UpdateBalance();
        date.ValueChanged += (_, _) => UpdateBalance();
        var form = Ui.FormGrid();
        Ui.AddField(form, "Conta", account); Ui.AddField(form, "Data do saldo", date);
        Ui.AddField(form, "Saldo no aplicativo", expected); Ui.AddField(form, "Saldo no banco", amount); Ui.AddField(form, "Observacoes", notes);
        ShowEntryDialog("Conferir saldo", form, () => Service.ReconcileAccount(Ui.SelectedId(account), date.Value.Date, Ui.ParseMoney(amount, "saldo"), notes.Text), RefreshData);
    }

    private TabPage BuildImport()
    {
        _preview.ReadOnly = false;
        _preview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _preview.AutoGenerateColumns = false;
        _preview.Columns.Clear();
        _preview.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "Importar", Width = 65 });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "Data", ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "Descricao", ReadOnly = true, FillWeight = 180 });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "Valor", ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
        _preview.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Duplicate", HeaderText = "Possivel duplicado", ReadOnly = true });
        _preview.Columns.Add(new DataGridViewComboBoxColumn { Name = "Categoria", DataPropertyName = "CategoryId", HeaderText = "Categoria", DisplayMember = "Name", ValueMember = "Id", FlatStyle = FlatStyle.Flat });
        _preview.DataError += (_, e) => { e.ThrowException = false; };
        _importAccount.SelectedIndexChanged += (_, _) =>
        {
            if (_previewAccount != 0 && Ui.SelectedId(_importAccount) != _previewAccount)
            { _entries = new(); _preview.DataSource = _entries; _importSummary.Text = ""; _previewAccount = 0; }
        };
        static Control Field(string label, Control input)
        {
            var group = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(4) };
            var caption = Ui.Label(label); caption.Margin = new Padding(0, 0, 0, 4);
            input.Width = 200; input.Margin = Padding.Empty;
            group.Controls.Add(caption); group.Controls.Add(input);
            return group;
        }
        var page = Page("Importar extrato", _preview,
            Field("Conta", _importAccount), Field("Receita padrao", _incomeCategory), Field("Despesa padrao", _expenseCategory),
            Ui.Button("Abrir OFX / CSV", async (_, _) => await ReadStatement()), Ui.Button("Confirmar importacao", async (_, _) => await Import()));
        _importSummary.Dock = DockStyle.Bottom;
        _importSummary.Height = 30;
        page.Controls.Add(_importSummary);
        return page;
    }

    private async Task ReadStatement()
    {
        try
        {
            var account = Ui.SelectedId(_importAccount);
            if (account <= 0) throw new InvalidOperationException("Selecione a conta antes de abrir o extrato.");
            using var dialog = new OpenFileDialog { Filter = "Extratos (*.ofx;*.csv)|*.ofx;*.csv" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            Enabled = false;
            var entries = await Task.Run(() => Service.PrepareImport(account, StatementReader.Read(dialog.FileName)));
            _previewAccount = account;
            _entries = new BindingList<ImportEntry>(entries);
            var categories = Service.GetCategories("");
            categories[0] = new LookupItem(0, "Categoria padrao");
            ((DataGridViewComboBoxColumn)_preview.Columns["Categoria"]!).DataSource = categories;
            _preview.DataSource = _entries;
            _importSummary.Text = $"{entries.Count} movimento(s), {entries.Count(e => e.Duplicate)} possivel(is) duplicado(s).";
        }
        catch (Exception ex) { Ui.ShowError(ex); }
        finally { Enabled = true; }
    }

    private async Task Import()
    {
        try
        {
            _preview.EndEdit();
            if (_previewAccount == 0 || _entries.Count == 0) throw new InvalidOperationException("Abra um extrato primeiro.");
            var income = Ui.SelectedId(_incomeCategory); var expense = Ui.SelectedId(_expenseCategory);
            if (MessageBox.Show($"Importar {_entries.Count(e => e.Selected)} movimento(s)?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Enabled = false;
            var count = await Task.Run(() => Service.ImportStatement(_previewAccount, _entries.ToList(), income, expense));
            _entries = new(); _preview.DataSource = _entries; _previewAccount = 0;
            _importSummary.Text = $"{count} movimento(s) importado(s).";
            RefreshData();
            Ui.ShowInfo(_importSummary.Text);
        }
        catch (Exception ex) { Ui.ShowError(ex); }
        finally { Enabled = true; }
    }

    private void LoadForecast()
    {
        var table = new DataTable();
        table.Columns.Add("Mes", typeof(DateTime));
        foreach (var name in new[] { "SaldoInicial", "Receitas", "Despesas", "SaldoPrevisto" }) table.Columns.Add(name, typeof(decimal));
        foreach (var month in Service.GetForecast((int)_months.Value)) table.Rows.Add(month.Month, month.OpeningBalance, month.Income, month.Outgoing, month.ClosingBalance);
        Ui.BindGrid(_forecast, table);
        _forecast.Columns["Mes"]!.DefaultCellStyle.Format = "MM/yyyy";
    }

    private void EditRule()
    {
        var text = Ui.TextBox();
        var type = Ui.ComboBox(); type.Items.AddRange(new object[] { FinanceTypes.Expense, FinanceTypes.Income });
        var category = Ui.ComboBox();
        type.SelectedIndexChanged += (_, _) => Ui.BindLookup(category, Service.GetCategories(Convert.ToString(type.SelectedItem)!));
        type.SelectedIndex = 0;
        var form = Ui.FormGrid();
        Ui.AddField(form, "Texto da descricao", text); Ui.AddField(form, "Tipo", type); Ui.AddField(form, "Categoria", category);
        ShowEntryDialog("Nova regra de categoria", form, () => Service.SaveCategoryRule(text.Text, Convert.ToString(type.SelectedItem)!, Ui.SelectedId(category)), RefreshData);
    }
}
