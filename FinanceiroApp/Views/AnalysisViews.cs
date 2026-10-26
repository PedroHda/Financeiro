using System.Data;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class BudgetsView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _monthFilter = Ui.TextBox(DateTime.Today.ToString("yyyy-MM"));
    private readonly TextBox _month = Ui.TextBox(DateTime.Today.ToString("yyyy-MM"));
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly TextBox _limit = Ui.TextBox("0,00");
    private int _id;

    public BudgetsView(FinanceService service) : base(service, "Orcamento mensal")
    {
        _monthFilter.Text = _month.Text = DateTime.Today.ToString("yyyy-MM");
        Build();
    }

    public override void RefreshData()
    {
        var cat = Ui.SelectedId(_category);
        Ui.BindLookup(_category, Service.GetCategories(FinanceTypes.Expense));
        if (cat > 0) Ui.SelectLookup(_category, cat);
        Ui.BindGrid(_grid, Service.SearchBudgets(_monthFilter.Text));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Label("Mes"));
        toolbar.Controls.Add(_monthFilter);
        toolbar.Controls.Add(Ui.Button("Filtrar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Todos", (_, _) => { _monthFilter.Clear(); RefreshData(); }));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Mes", _month);
        Ui.AddField(form, "Categoria", _category);
        Ui.AddField(form, "Limite", _limit);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Limite por categoria", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void Save()
    {
        Service.SaveBudget(new BudgetInput
        {
            Id = _id,
            Month = _month.Text,
            CategoryId = Ui.SelectedId(_category),
            LimitAmount = Ui.ParseMoney(_limit, "limite")
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var month = Ui.TextBox(DateTime.Today.ToString("yyyy-MM"));
        month.Text = DateTime.Today.ToString("yyyy-MM");
        var category = Ui.ComboBox();
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Expense));
        var limit = Ui.TextBox("0,00");
        limit.Text = "0,00";

        var form = Ui.FormGrid();
        Ui.AddField(form, "Mes", month);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Limite", limit);

        ShowEntryDialog(
            "Novo orcamento",
            form,
            () => Service.SaveBudget(new BudgetInput
            {
                Month = month.Text,
                CategoryId = Ui.SelectedId(category),
                LimitAmount = Ui.ParseMoney(limit, "limite")
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione um orcamento.");
        }

        if (MessageBox.Show("Excluir este orcamento?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteBudget(_id);
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

        _month.Text = Ui.CellText(_grid, "Mes");
        Ui.SelectLookupByName(_category, Ui.CellText(_grid, "Categoria"));
        _limit.Text = Ui.CellMoney(_grid, "Limite").ToString("0.00");
    }

    private void ClearForm()
    {
        _id = 0;
        _month.Text = DateTime.Today.ToString("yyyy-MM");
        _limit.Text = "0,00";
        if (_category.Items.Count > 0) _category.SelectedIndex = 0;
    }
}

public sealed class GoalsView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly DataGridView _contributions = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar meta");
    private readonly TextBox _name = Ui.TextBox();
    private readonly TextBox _target = Ui.TextBox("0,00");
    private readonly TextBox _current = Ui.TextBox("0,00");
    private readonly DateTimePicker _targetDate = Ui.DatePicker();
    private readonly TextBox _description = Ui.TextBox();
    private readonly ComboBox _status = Ui.ComboBox();
    private readonly TextBox _contributionAmount = Ui.TextBox("0,00");
    private readonly DateTimePicker _contributionDate = Ui.DatePicker();
    private readonly TextBox _contributionNotes = Ui.TextBox();
    private int _id;

    public GoalsView(FinanceService service) : base(service, "Metas financeiras")
    {
        _status.Items.AddRange(new object[] { FinanceStatus.Active, FinanceStatus.Finished });
        _status.SelectedIndex = 0;
        Build();
    }

    public override void RefreshData()
    {
        Ui.BindGrid(_grid, Service.SearchGoals(_search.Text));
        LoadContributions();
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 330 };
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(Ui.Button("Pesquisar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => { _search.Clear(); RefreshData(); }));

        var goalsPanel = SurfacePanel();
        goalsPanel.Controls.Add(_grid);
        goalsPanel.Controls.Add(toolbar);
        left.Panel1.Controls.Add(goalsPanel);
        left.Panel2.Controls.Add(PanelWithGrid("Aportes da meta selecionada", _contributions));

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", _name);
        Ui.AddField(form, "Valor objetivo", _target);
        Ui.AddField(form, "Valor atual", _current);
        Ui.AddField(form, "Data alvo", _targetDate);
        Ui.AddField(form, "Descricao", _description);
        Ui.AddField(form, "Status", _status);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var contribution = Ui.FormGrid();
        Ui.AddField(contribution, "Aporte", _contributionAmount);
        Ui.AddField(contribution, "Data", _contributionDate);
        Ui.AddField(contribution, "Obs.", _contributionNotes);
        var contributionActions = Ui.Toolbar();
        contributionActions.Controls.Add(Ui.Button("Adicionar aporte", (_, _) => RunSave(AddContribution, RefreshData, "Aporte adicionado com sucesso."), true));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(contributionActions);
        formPanel.Controls.Add(contribution);
        formPanel.Controls.Add(new Label { Text = "Aportes", Dock = DockStyle.Top, Height = 30, Font = Ui.HeadingFont, ForeColor = Ui.Text });
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Cadastro", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(left);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => { FillForm(); LoadContributions(); };
    }

    private static Control PanelWithGrid(string title, DataGridView grid)
    {
        var panel = SurfacePanel();
        panel.Controls.Add(grid);
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 30, Font = Ui.HeadingFont, ForeColor = Ui.Text });
        return panel;
    }

    private void Save()
    {
        Service.SaveGoal(new GoalInput
        {
            Id = _id,
            Name = _name.Text,
            TargetAmount = Ui.ParseMoney(_target, "valor objetivo"),
            CurrentAmount = Ui.ParseMoney(_current, "valor atual"),
            TargetDate = _targetDate.Value.Date,
            Description = _description.Text,
            Status = Convert.ToString(_status.SelectedItem) ?? FinanceStatus.Active
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var name = Ui.TextBox();
        var target = Ui.TextBox("0,00");
        target.Text = "0,00";
        var current = Ui.TextBox("0,00");
        current.Text = "0,00";
        var targetDate = Ui.DatePicker();
        targetDate.Value = DateTime.Today.AddMonths(6);
        var description = Ui.TextBox();
        var status = Ui.ComboBox();
        status.Items.AddRange(new object[] { FinanceStatus.Active, FinanceStatus.Finished });
        status.SelectedItem = FinanceStatus.Active;

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", name);
        Ui.AddField(form, "Valor objetivo", target);
        Ui.AddField(form, "Valor atual", current);
        Ui.AddField(form, "Data alvo", targetDate);
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Status", status);

        ShowEntryDialog(
            "Nova meta financeira",
            form,
            () => Service.SaveGoal(new GoalInput
            {
                Name = name.Text,
                TargetAmount = Ui.ParseMoney(target, "valor objetivo"),
                CurrentAmount = Ui.ParseMoney(current, "valor atual"),
                TargetDate = targetDate.Value.Date,
                Description = description.Text,
                Status = Convert.ToString(status.SelectedItem) ?? FinanceStatus.Active
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma meta.");
        }

        if (MessageBox.Show("Excluir esta meta?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteGoal(_id);
            ClearForm();
        }
    }

    private void AddContribution()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma meta.");
        }

        Service.AddGoalContribution(_id, Ui.ParseMoney(_contributionAmount, "aporte"), _contributionDate.Value.Date, _contributionNotes.Text);
        _contributionAmount.Text = "0,00";
        _contributionNotes.Clear();
    }

    private void FillForm()
    {
        _id = Ui.SelectedGridId(_grid);
        if (_id == 0)
        {
            return;
        }

        _name.Text = Ui.CellText(_grid, "Nome");
        _target.Text = Ui.CellMoney(_grid, "ValorObjetivo").ToString("0.00");
        _current.Text = Ui.CellMoney(_grid, "ValorAtual").ToString("0.00");
        _targetDate.Value = Ui.CellDate(_grid, "DataAlvo");
        _description.Text = Ui.CellText(_grid, "Descricao");
        _status.SelectedItem = Ui.CellText(_grid, "Status");
    }

    private void LoadContributions()
    {
        if (Ui.SelectedGridId(_grid) > 0)
        {
            Ui.BindGrid(_contributions, Service.GetGoalContributions(Ui.SelectedGridId(_grid)));
        }
        else
        {
            Ui.BindGrid(_contributions, new DataTable());
        }
    }

    private void ClearForm()
    {
        _id = 0;
        _name.Clear();
        _target.Text = "0,00";
        _current.Text = "0,00";
        _targetDate.Value = DateTime.Today.AddMonths(6);
        _description.Clear();
        _status.SelectedItem = FinanceStatus.Active;
    }
}

public sealed class ReportsView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly ComboBox _type = Ui.ComboBox();
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly ComboBox _category = Ui.ComboBox();
    private readonly ComboBox _account = Ui.ComboBox();
    private readonly ComboBox _card = Ui.ComboBox();
    private readonly ComboBox _status = Ui.ComboBox();
    private readonly Label _summary = Ui.Label("Nenhum relatorio gerado.");

    public ReportsView(FinanceService service) : base(service, "Relatorios")
    {
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today;
        _type.Items.AddRange(new object[]
        {
            "Receitas por periodo",
            "Despesas por periodo",
            "Despesas por categoria",
            "Saldo mensal",
            "Contas pagas",
            "Contas pendentes",
            "Contas vencidas",
            "Gastos por cartao",
            "Evolucao mensal",
            "Receitas x despesas"
        });
        _type.SelectedIndex = 0;
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        Generate();
    }

    private void Build()
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_type);
        toolbar.Controls.Add(Ui.Label("De"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(Ui.Label("Ate"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(_category);
        toolbar.Controls.Add(_account);
        toolbar.Controls.Add(_card);
        toolbar.Controls.Add(_status);
        toolbar.Controls.Add(Ui.Button("Gerar", (_, _) => Run(Generate), true));
        toolbar.Controls.Add(Ui.Button("Exportar CSV", (_, _) => Run(Export)));

        var panel = SurfacePanel();
        _summary.Dock = DockStyle.Bottom;
        _summary.Height = 34;
        panel.Controls.Add(_grid);
        panel.Controls.Add(_summary);
        panel.Controls.Add(toolbar);
        Content.Controls.Add(panel);
    }

    private void LoadLookups()
    {
        var cat = Ui.SelectedId(_category);
        var acc = Ui.SelectedId(_account);
        var card = Ui.SelectedId(_card);
        Ui.BindLookup(_category, Service.GetCategories(""));
        Ui.BindLookup(_account, Service.GetAccounts());
        Ui.BindLookup(_card, Service.GetCreditCards());
        if (cat > 0) Ui.SelectLookup(_category, cat);
        if (acc > 0) Ui.SelectLookup(_account, acc);
        if (card > 0) Ui.SelectLookup(_card, card);
        var current = Convert.ToString(_status.SelectedItem) ?? "";
        _status.Items.Clear();
        _status.Items.AddRange(new object[] { "", FinanceStatus.Received, FinanceStatus.Pending, FinanceStatus.Paid, FinanceStatus.Overdue, FinanceStatus.Open, FinanceStatus.Closed, "Paga" });
        _status.SelectedItem = current;
        if (_status.SelectedIndex < 0) _status.SelectedIndex = 0;
    }

    private void Generate()
    {
        var filter = new ReportFilter
        {
            StartDate = _from.Value.Date,
            EndDate = _to.Value.Date,
            CategoryId = Ui.SelectedId(_category),
            AccountId = Ui.SelectedId(_account),
            CreditCardId = Ui.SelectedId(_card),
            Status = Convert.ToString(_status.SelectedItem)
        };
        var table = Service.GetReport(Convert.ToString(_type.SelectedItem) ?? "", filter);
        Ui.BindGrid(_grid, table);
        _summary.Text = $"{table.Rows.Count} registro(s) no relatorio.";
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "relatorio.csv" };
        if (dialog.ShowDialog() == DialogResult.OK && Ui.BoundTable(_grid) is { } table)
        {
            Service.ExportTableToCsv(table, dialog.FileName);
            Ui.ShowInfo("Arquivo exportado.");
        }
    }
}

public sealed class SettingsView : BaseView
{
    private readonly ComboBox _currency = Ui.ComboBox();
    private readonly TextBox _dateFormat = Ui.TextBox("dd/MM/yyyy");
    private readonly ComboBox _theme = Ui.ComboBox();
    private readonly Label _dbPath = Ui.Label("");

    public SettingsView(FinanceService service) : base(service, "Configuracoes")
    {
        _currency.Items.AddRange(new object[] { "BRL" });
        _theme.Items.AddRange(new object[] { "Claro", "Escuro" });
        Build();
    }

    public override void RefreshData()
    {
        _currency.SelectedItem = Service.GetSetting("Currency", "BRL");
        if (_currency.SelectedIndex < 0) _currency.SelectedIndex = 0;
        _dateFormat.Text = Service.GetSetting("DateFormat", "dd/MM/yyyy");
        _theme.SelectedItem = Service.GetSetting("Theme", "Claro");
        if (_theme.SelectedIndex < 0) _theme.SelectedIndex = 0;
        _dbPath.Text = Service.DatabasePath;
    }

    private void Build()
    {
        var panel = SurfacePanel();
        panel.Dock = DockStyle.Top;
        panel.Height = 430;

        var form = Ui.FormGrid();
        Ui.AddField(form, "Moeda padrao", _currency);
        Ui.AddField(form, "Formato de data", _dateFormat);
        Ui.AddField(form, "Tema", _theme);
        Ui.AddField(form, "Banco local", _dbPath);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Salvar", (_, _) => Run(Save), true));
        actions.Controls.Add(Ui.Button("Dados exemplo", (_, _) => RunSave(Seed, null, "Dados de exemplo criados.")));
        actions.Controls.Add(Ui.Button("Limpar dados", (_, _) => Run(Clear)));
        actions.Controls.Add(Ui.Button("Backup", (_, _) => Run(Backup)));
        actions.Controls.Add(Ui.Button("Restaurar", (_, _) => Run(Restore)));
        actions.Controls.Add(Ui.Button("Backup protegido", (_, _) => Run(EncryptedBackup)));
        actions.Controls.Add(Ui.Button("Restaurar protegido", (_, _) => Run(RestoreEncryptedBackup)));

        var note = new Label
        {
            Text = "O tema escuro fica salvo para evolucao visual futura; a interface atual usa o tema claro.",
            Dock = DockStyle.Top,
            Height = 42,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted
        };

        panel.Controls.Add(actions);
        panel.Controls.Add(note);
        panel.Controls.Add(form);
        panel.Controls.Add(new Label { Text = "Preferencias e manutencao", Dock = DockStyle.Top, Height = 34, Font = Ui.HeadingFont, ForeColor = Ui.Text });
        Content.Controls.Add(panel);
    }

    private void Save()
    {
        Service.SetSetting("Currency", Convert.ToString(_currency.SelectedItem) ?? "BRL");
        Service.SetSetting("DateFormat", _dateFormat.Text);
        Service.SetSetting("Theme", Convert.ToString(_theme.SelectedItem) ?? "Claro");
        Ui.ShowInfo("Configuracoes salvas.");
    }

    private void Seed()
    {
        Service.SeedDemoData();
    }

    private void Clear()
    {
        if (MessageBox.Show("Isto remove contas, lancamentos, cartoes, metas e orcamentos. Continuar?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            Service.ClearFinancialData();
            Ui.ShowInfo("Dados financeiros removidos.");
        }
    }

    private void Backup()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "SQLite DB (*.db)|*.db|Todos os arquivos (*.*)|*.*",
            FileName = $"financeiro-backup-{DateTime.Today:yyyyMMdd}.db"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            Service.BackupDatabase(dialog.FileName);
            Ui.ShowInfo("Backup gerado.");
        }
    }

    private void Restore()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "SQLite DB (*.db)|*.db|Todos os arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog() == DialogResult.OK &&
            MessageBox.Show("Restaurar este backup e substituir os dados atuais?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            Service.RestoreDatabase(dialog.FileName);
            RefreshData();
            Ui.ShowInfo("Backup restaurado.");
        }
    }

    private void EncryptedBackup()
    {
        using var dialog = new SaveFileDialog { Filter = "Backup protegido (*.finbackup)|*.finbackup", FileName = $"financeiro-{DateTime.Today:yyyyMMdd}.finbackup" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var password = PasswordPrompt.Ask(FindForm()!, "Senha do backup", true);
        if (password is null) return;
        new EncryptedBackupService(Service).Backup(dialog.FileName, password);
        Ui.ShowInfo("Backup protegido gerado.");
    }

    private void RestoreEncryptedBackup()
    {
        using var dialog = new OpenFileDialog { Filter = "Backup protegido (*.finbackup)|*.finbackup" };
        if (dialog.ShowDialog() != DialogResult.OK || MessageBox.Show("Substituir os dados atuais por este backup?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var password = PasswordPrompt.Ask(FindForm()!, "Senha do backup");
        if (password is null) return;
        new EncryptedBackupService(Service).Restore(dialog.FileName, password);
        RefreshData(); Ui.ShowInfo("Backup restaurado.");
    }
}
