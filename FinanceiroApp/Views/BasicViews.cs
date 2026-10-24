using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class AccountsView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar conta");
    private readonly TextBox _name = Ui.TextBox();
    private readonly ComboBox _type = Ui.ComboBox();
    private readonly TextBox _initial = Ui.TextBox("0,00");
    private readonly CheckBox _active = new() { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };
    private readonly TextBox _notes = Ui.TextBox();
    private int _id;

    public AccountsView(FinanceService service) : base(service, "Contas e carteiras")
    {
        _type.Items.AddRange(new object[] { "Conta corrente", "Dinheiro fisico", "Poupanca", "Carteira digital", "Outros" });
        _type.SelectedIndex = 0;
        Build();
    }

    public override void RefreshData()
    {
        Ui.BindGrid(_grid, Service.SearchAccounts(_search.Text));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();

        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(Ui.Button("Pesquisar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => { _search.Clear(); RefreshData(); }));
        toolbar.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", _name);
        Ui.AddField(form, "Tipo", _type);
        Ui.AddField(form, "Saldo inicial", _initial);
        Ui.AddField(form, "Status", _active);
        Ui.AddField(form, "Observacoes", _notes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));

        var formPanel = SurfacePanel();
        formPanel.Dock = DockStyle.Fill;
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Cadastro", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void Save()
    {
        Service.SaveAccount(new AccountInput
        {
            Id = _id,
            Name = _name.Text,
            Type = Convert.ToString(_type.SelectedItem) ?? "",
            InitialBalance = Ui.ParseMoney(_initial, "saldo inicial"),
            IsActive = _active.Checked,
            Notes = _notes.Text
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var name = Ui.TextBox();
        var type = Ui.ComboBox();
        type.Items.AddRange(new object[] { "Conta corrente", "Dinheiro fisico", "Poupanca", "Carteira digital", "Outros" });
        type.SelectedIndex = 0;
        var initial = Ui.TextBox("0,00");
        initial.Text = "0,00";
        var active = new CheckBox { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", name);
        Ui.AddField(form, "Tipo", type);
        Ui.AddField(form, "Saldo inicial", initial);
        Ui.AddField(form, "Status", active);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Nova conta ou carteira",
            form,
            () => Service.SaveAccount(new AccountInput
            {
                Name = name.Text,
                Type = Convert.ToString(type.SelectedItem) ?? "",
                InitialBalance = Ui.ParseMoney(initial, "saldo inicial"),
                IsActive = active.Checked,
                Notes = notes.Text
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma conta.");
        }

        if (MessageBox.Show("Excluir esta conta?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteAccount(_id);
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

        _name.Text = Ui.CellText(_grid, "Nome");
        _type.SelectedItem = Ui.CellText(_grid, "Tipo");
        _initial.Text = Ui.CellMoney(_grid, "SaldoInicial").ToString("0.00");
        _active.Checked = Ui.CellText(_grid, "Status") == "Ativo";
        _notes.Text = Ui.CellText(_grid, "Observacoes");
    }

    private void ClearForm()
    {
        _id = 0;
        _name.Clear();
        _initial.Text = "0,00";
        _active.Checked = true;
        _notes.Clear();
        if (_type.Items.Count > 0)
        {
            _type.SelectedIndex = 0;
        }
    }
}

public sealed class CategoriesView : BaseView
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.TextBox("Pesquisar categoria");
    private readonly ComboBox _filterType = Ui.ComboBox();
    private readonly TextBox _name = Ui.TextBox();
    private readonly ComboBox _type = Ui.ComboBox();
    private readonly TextBox _color = Ui.TextBox("#4F6BED");
    private readonly TextBox _icon = Ui.TextBox("icone");
    private readonly CheckBox _active = new() { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };
    private int _id;

    public CategoriesView(FinanceService service) : base(service, "Categorias")
    {
        _filterType.Items.AddRange(new object[] { "", FinanceTypes.Income, FinanceTypes.Expense });
        _filterType.SelectedIndex = 0;
        _type.Items.AddRange(new object[] { FinanceTypes.Income, FinanceTypes.Expense });
        _type.SelectedItem = FinanceTypes.Expense;
        Build();
    }

    public override void RefreshData()
    {
        Ui.BindGrid(_grid, Service.SearchCategories(_search.Text, Convert.ToString(_filterType.SelectedItem) ?? ""));
    }

    private void Build()
    {
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_filterType);
        toolbar.Controls.Add(Ui.Button("Pesquisar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Limpar", (_, _) => { _search.Clear(); _filterType.SelectedIndex = 0; RefreshData(); }));
        toolbar.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewDialog()));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_grid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", _name);
        Ui.AddField(form, "Tipo", _type);
        Ui.AddField(form, "Cor", _color);
        Ui.AddField(form, "Icone", _icon);
        Ui.AddField(form, "Status", _active);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(Save, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(Delete, RefreshData)));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Cadastro", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        Content.Controls.Add(main);
        _grid.SelectionChanged += (_, _) => FillForm();
    }

    private void Save()
    {
        Service.SaveCategory(new CategoryInput
        {
            Id = _id,
            Name = _name.Text,
            Type = Convert.ToString(_type.SelectedItem) ?? FinanceTypes.Expense,
            ColorHex = _color.Text,
            Icon = _icon.Text,
            IsActive = _active.Checked
        });
        ClearForm();
    }

    private void OpenNewDialog()
    {
        var name = Ui.TextBox();
        var type = Ui.ComboBox();
        type.Items.AddRange(new object[] { FinanceTypes.Income, FinanceTypes.Expense });
        type.SelectedItem = FinanceTypes.Expense;
        var color = Ui.TextBox("#4F6BED");
        color.Text = "#4F6BED";
        var icon = Ui.TextBox("icone");
        var active = new CheckBox { Text = "Ativa", Checked = true, AutoSize = true, Font = Ui.BodyFont };

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", name);
        Ui.AddField(form, "Tipo", type);
        Ui.AddField(form, "Cor", color);
        Ui.AddField(form, "Icone", icon);
        Ui.AddField(form, "Status", active);

        ShowEntryDialog(
            "Nova categoria",
            form,
            () => Service.SaveCategory(new CategoryInput
            {
                Name = name.Text,
                Type = Convert.ToString(type.SelectedItem) ?? FinanceTypes.Expense,
                ColorHex = color.Text,
                Icon = icon.Text,
                IsActive = active.Checked
            }),
            () => { ClearForm(); RefreshData(); });
    }

    private void Delete()
    {
        if (_id <= 0)
        {
            throw new InvalidOperationException("Selecione uma categoria.");
        }

        if (MessageBox.Show("Excluir esta categoria?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteCategory(_id);
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

        _name.Text = Ui.CellText(_grid, "Nome");
        _type.SelectedItem = Ui.CellText(_grid, "Tipo");
        _color.Text = Ui.CellText(_grid, "Cor");
        _icon.Text = Ui.CellText(_grid, "Icone");
        _active.Checked = Ui.CellText(_grid, "Status") == "Ativo";
    }

    private void ClearForm()
    {
        _id = 0;
        _name.Clear();
        _type.SelectedItem = FinanceTypes.Expense;
        _color.Text = "#4F6BED";
        _icon.Clear();
        _active.Checked = true;
    }
}
