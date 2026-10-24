using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class CreditCardsView : BaseView
{
    private readonly DataGridView _cardsGrid = Ui.Grid();
    private readonly DataGridView _purchasesGrid = Ui.Grid();
    private readonly DataGridView _invoicesGrid = Ui.Grid();
    private readonly TextBox _cardSearch = Ui.TextBox("Pesquisar cartao");
    private readonly TextBox _cardName = Ui.TextBox();
    private readonly TextBox _institution = Ui.TextBox();
    private readonly TextBox _limit = Ui.TextBox("0,00");
    private readonly NumericUpDown _closingDay = Ui.NumberBox(1, 31);
    private readonly NumericUpDown _dueDay = Ui.NumberBox(1, 31);
    private readonly CheckBox _cardActive = new() { Text = "Ativo", Checked = true, AutoSize = true, Font = Ui.BodyFont };

    private readonly TextBox _purchaseSearch = Ui.TextBox("Pesquisar compra");
    private readonly DateTimePicker _purchaseFrom = Ui.DatePicker();
    private readonly DateTimePicker _purchaseTo = Ui.DatePicker();
    private readonly ComboBox _purchaseFilterCard = Ui.ComboBox();
    private readonly ComboBox _purchaseFilterCategory = Ui.ComboBox();
    private readonly ComboBox _purchaseFilterStatus = Ui.ComboBox();
    private readonly TextBox _purchaseDescription = Ui.TextBox();
    private readonly TextBox _purchaseAmount = Ui.TextBox("0,00");
    private readonly DateTimePicker _purchaseDate = Ui.DatePicker();
    private readonly ComboBox _purchaseCard = Ui.ComboBox();
    private readonly ComboBox _purchaseCategory = Ui.ComboBox();
    private readonly NumericUpDown _purchaseInstallments = Ui.NumberBox(1, 120);
    private readonly TextBox _purchaseNotes = Ui.TextBox();

    private readonly TextBox _invoiceFilterMonth = Ui.TextBox(DateTime.Today.ToString("yyyy-MM"));
    private readonly TextBox _invoiceActionMonth = Ui.TextBox(DateTime.Today.ToString("yyyy-MM"));
    private readonly ComboBox _invoiceCard = Ui.ComboBox();
    private readonly ComboBox _invoiceAccount = Ui.ComboBox();
    private int _cardId;
    private int _purchaseId;

    public CreditCardsView(FinanceService service) : base(service, "Cartoes de credito")
    {
        _invoiceFilterMonth.Text = _invoiceActionMonth.Text = DateTime.Today.ToString("yyyy-MM");
        _closingDay.Value = 25;
        _dueDay.Value = 8;
        _purchaseFrom.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _purchaseTo.Value = DateTime.Today.AddMonths(1);
        Build();
    }

    public override void RefreshData()
    {
        LoadLookups();
        Ui.BindGrid(_cardsGrid, Service.SearchCreditCards(_cardSearch.Text));
        var filter = new ReportFilter
        {
            StartDate = _purchaseFrom.Value.Date,
            EndDate = _purchaseTo.Value.Date,
            CreditCardId = Ui.SelectedId(_purchaseFilterCard),
            CategoryId = Ui.SelectedId(_purchaseFilterCategory),
            Status = Convert.ToString(_purchaseFilterStatus.SelectedItem)
        };
        Ui.BindGrid(_purchasesGrid, Service.SearchCardPurchases(filter, _purchaseSearch.Text));
        Ui.BindGrid(_invoicesGrid, Service.SearchInvoices(_invoiceFilterMonth.Text));
    }

    private void Build()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.BodyFont };
        tabs.TabPages.Add(BuildCardsTab());
        tabs.TabPages.Add(BuildPurchasesTab());
        tabs.TabPages.Add(BuildInvoicesTab());
        Content.Controls.Add(tabs);
    }

    private TabPage BuildCardsTab()
    {
        var page = new TabPage("Cartoes") { BackColor = Ui.Background };
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_cardSearch);
        toolbar.Controls.Add(Ui.Button("Pesquisar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewCardDialog()));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_cardsGrid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", _cardName);
        Ui.AddField(form, "Instituicao", _institution);
        Ui.AddField(form, "Limite total", _limit);
        Ui.AddField(form, "Dia fechamento", _closingDay);
        Ui.AddField(form, "Dia vencimento", _dueDay);
        Ui.AddField(form, "Status", _cardActive);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(SaveCard, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(DeleteCard, RefreshData)));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Cadastro", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        page.Controls.Add(main);
        _cardsGrid.SelectionChanged += (_, _) => FillCardForm();
        return page;
    }

    private TabPage BuildPurchasesTab()
    {
        var page = new TabPage("Compras") { BackColor = Ui.Background };
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_purchaseSearch);
        toolbar.Controls.Add(Ui.Label("De"));
        toolbar.Controls.Add(_purchaseFrom);
        toolbar.Controls.Add(Ui.Label("Ate"));
        toolbar.Controls.Add(_purchaseTo);
        toolbar.Controls.Add(_purchaseFilterCard);
        toolbar.Controls.Add(_purchaseFilterCategory);
        toolbar.Controls.Add(_purchaseFilterStatus);
        toolbar.Controls.Add(Ui.Button("Filtrar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Exportar CSV", (_, _) => Run(ExportPurchases)));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_purchasesGrid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", _purchaseDescription);
        Ui.AddField(form, "Valor", _purchaseAmount);
        Ui.AddField(form, "Data", _purchaseDate);
        Ui.AddField(form, "Cartao", _purchaseCard);
        Ui.AddField(form, "Categoria", _purchaseCategory);
        Ui.AddField(form, "Parcelas", _purchaseInstallments);
        Ui.AddField(form, "Observacoes", _purchaseNotes);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Adicionar / Salvar", (_, _) => RunSave(SavePurchase, RefreshData), true));
        actions.Controls.Add(Ui.Button("Excluir", (_, _) => Run(DeletePurchase, RefreshData)));
        actions.Controls.Add(Ui.Button("Novo registro", (_, _) => OpenNewPurchaseDialog()));

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(new Label { Text = "Compra no cartao", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        page.Controls.Add(main);
        _purchasesGrid.SelectionChanged += (_, _) => FillPurchaseForm();
        return page;
    }

    private TabPage BuildInvoicesTab()
    {
        var page = new TabPage("Faturas") { BackColor = Ui.Background };
        var main = Ui.EditorSplit();
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Label("Mes"));
        toolbar.Controls.Add(_invoiceFilterMonth);
        toolbar.Controls.Add(Ui.Button("Filtrar", (_, _) => RefreshData()));
        toolbar.Controls.Add(Ui.Button("Todas", (_, _) => { _invoiceFilterMonth.Clear(); RefreshData(); }));

        var listPanel = SurfacePanel();
        listPanel.Controls.Add(_invoicesGrid);
        listPanel.Controls.Add(toolbar);

        var form = Ui.FormGrid();
        Ui.AddField(form, "Cartao", _invoiceCard);
        Ui.AddField(form, "Mes fatura", _invoiceActionMonth);
        Ui.AddField(form, "Conta para pagar", _invoiceAccount);

        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Fechar fatura", (_, _) => Run(CloseInvoice, RefreshData), true));
        actions.Controls.Add(Ui.Button("Pagar fatura", (_, _) => Run(PayInvoice, RefreshData)));

        var hint = new Label
        {
            Text = "Fechar move compras abertas para fatura fechada. Pagar baixa a fatura na conta escolhida.",
            Dock = DockStyle.Top,
            Height = 48,
            Font = Ui.BodyFont,
            ForeColor = Ui.Muted
        };

        var formPanel = SurfacePanel();
        formPanel.Controls.Add(actions);
        formPanel.Controls.Add(form);
        formPanel.Controls.Add(hint);
        formPanel.Controls.Add(new Label { Text = "Controle de fatura", Dock = DockStyle.Top, Height = 32, Font = Ui.HeadingFont, ForeColor = Ui.Text });

        main.Panel1.Controls.Add(listPanel);
        main.Panel2.Controls.Add(formPanel);
        page.Controls.Add(main);
        _invoicesGrid.SelectionChanged += (_, _) => FillInvoiceForm();
        return page;
    }

    private void LoadLookups()
    {
        var card = Ui.SelectedId(_purchaseCard);
        var cat = Ui.SelectedId(_purchaseCategory);
        var fcard = Ui.SelectedId(_purchaseFilterCard);
        var fcat = Ui.SelectedId(_purchaseFilterCategory);
        var invoiceCard = Ui.SelectedId(_invoiceCard);
        var account = Ui.SelectedId(_invoiceAccount);
        Ui.BindLookup(_purchaseCard, Service.GetCreditCards());
        Ui.BindLookup(_purchaseFilterCard, Service.GetCreditCards());
        Ui.BindLookup(_invoiceCard, Service.GetCreditCards());
        Ui.BindLookup(_purchaseCategory, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_purchaseFilterCategory, Service.GetCategories(FinanceTypes.Expense));
        Ui.BindLookup(_invoiceAccount, Service.GetAccounts());
        if (card > 0) Ui.SelectLookup(_purchaseCard, card);
        if (cat > 0) Ui.SelectLookup(_purchaseCategory, cat);
        if (fcard > 0) Ui.SelectLookup(_purchaseFilterCard, fcard);
        if (fcat > 0) Ui.SelectLookup(_purchaseFilterCategory, fcat);
        if (invoiceCard > 0) Ui.SelectLookup(_invoiceCard, invoiceCard);
        if (account > 0) Ui.SelectLookup(_invoiceAccount, account);

        var currentStatus = Convert.ToString(_purchaseFilterStatus.SelectedItem) ?? "";
        _purchaseFilterStatus.Items.Clear();
        _purchaseFilterStatus.Items.AddRange(Service.GetStatuses("card").Cast<object>().ToArray());
        _purchaseFilterStatus.SelectedItem = currentStatus;
        if (_purchaseFilterStatus.SelectedIndex < 0) _purchaseFilterStatus.SelectedIndex = 0;
    }

    private void SaveCard()
    {
        Service.SaveCreditCard(new CreditCardInput
        {
            Id = _cardId,
            Name = _cardName.Text,
            Institution = _institution.Text,
            TotalLimit = Ui.ParseMoney(_limit, "limite"),
            ClosingDay = (int)_closingDay.Value,
            DueDay = (int)_dueDay.Value,
            IsActive = _cardActive.Checked
        });
        ClearCardForm();
    }

    private void OpenNewCardDialog()
    {
        var name = Ui.TextBox();
        var institution = Ui.TextBox();
        var limit = Ui.TextBox("0,00");
        limit.Text = "0,00";
        var closingDay = Ui.NumberBox(1, 31);
        closingDay.Value = 25;
        var dueDay = Ui.NumberBox(1, 31);
        dueDay.Value = 8;
        var active = new CheckBox { Text = "Ativo", Checked = true, AutoSize = true, Font = Ui.BodyFont };

        var form = Ui.FormGrid();
        Ui.AddField(form, "Nome", name);
        Ui.AddField(form, "Instituicao", institution);
        Ui.AddField(form, "Limite total", limit);
        Ui.AddField(form, "Dia fechamento", closingDay);
        Ui.AddField(form, "Dia vencimento", dueDay);
        Ui.AddField(form, "Status", active);

        ShowEntryDialog(
            "Novo cartao",
            form,
            () => Service.SaveCreditCard(new CreditCardInput
            {
                Name = name.Text,
                Institution = institution.Text,
                TotalLimit = Ui.ParseMoney(limit, "limite"),
                ClosingDay = (int)closingDay.Value,
                DueDay = (int)dueDay.Value,
                IsActive = active.Checked
            }),
            () => { ClearCardForm(); RefreshData(); });
    }

    private void DeleteCard()
    {
        if (_cardId <= 0)
        {
            throw new InvalidOperationException("Selecione um cartao.");
        }

        if (MessageBox.Show("Excluir este cartao?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteCreditCard(_cardId);
            ClearCardForm();
        }
    }

    private void FillCardForm()
    {
        _cardId = Ui.SelectedGridId(_cardsGrid);
        if (_cardId == 0)
        {
            return;
        }

        _cardName.Text = Ui.CellText(_cardsGrid, "Nome");
        _institution.Text = Ui.CellText(_cardsGrid, "Instituicao");
        _limit.Text = Ui.CellMoney(_cardsGrid, "LimiteTotal").ToString("0.00");
        if (int.TryParse(Ui.CellText(_cardsGrid, "Fechamento"), out var closing)) _closingDay.Value = Math.Clamp(closing, 1, 31);
        if (int.TryParse(Ui.CellText(_cardsGrid, "Vencimento"), out var due)) _dueDay.Value = Math.Clamp(due, 1, 31);
        _cardActive.Checked = Ui.CellText(_cardsGrid, "Status") == "Ativo";
    }

    private void ClearCardForm()
    {
        _cardId = 0;
        _cardName.Clear();
        _institution.Clear();
        _limit.Text = "0,00";
        _closingDay.Value = 25;
        _dueDay.Value = 8;
        _cardActive.Checked = true;
    }

    private void SavePurchase()
    {
        Service.SaveCardPurchase(new CardPurchaseInput
        {
            Id = _purchaseId,
            Description = _purchaseDescription.Text,
            Amount = Ui.ParseMoney(_purchaseAmount, "valor"),
            PurchaseDate = _purchaseDate.Value.Date,
            CreditCardId = Ui.SelectedId(_purchaseCard),
            CategoryId = Ui.SelectedId(_purchaseCategory),
            InstallmentCount = (int)_purchaseInstallments.Value,
            Notes = _purchaseNotes.Text
        });
        ClearPurchaseForm();
    }

    private void OpenNewPurchaseDialog()
    {
        var description = Ui.TextBox();
        var amount = Ui.TextBox("0,00");
        amount.Text = "0,00";
        var date = Ui.DatePicker();
        date.Value = DateTime.Today;
        var card = Ui.ComboBox();
        var category = Ui.ComboBox();
        Ui.BindLookup(card, Service.GetCreditCards());
        Ui.BindLookup(category, Service.GetCategories(FinanceTypes.Expense));
        var installments = Ui.NumberBox(1, 120);
        installments.Value = 1;
        var notes = Ui.TextBox();

        var form = Ui.FormGrid();
        Ui.AddField(form, "Descricao", description);
        Ui.AddField(form, "Valor", amount);
        Ui.AddField(form, "Data", date);
        Ui.AddField(form, "Cartao", card);
        Ui.AddField(form, "Categoria", category);
        Ui.AddField(form, "Parcelas", installments);
        Ui.AddField(form, "Observacoes", notes);

        ShowEntryDialog(
            "Nova compra no cartao",
            form,
            () => Service.SaveCardPurchase(new CardPurchaseInput
            {
                Description = description.Text,
                Amount = Ui.ParseMoney(amount, "valor"),
                PurchaseDate = date.Value.Date,
                CreditCardId = Ui.SelectedId(card),
                CategoryId = Ui.SelectedId(category),
                InstallmentCount = (int)installments.Value,
                Notes = notes.Text
            }),
            () => { ClearPurchaseForm(); RefreshData(); });
    }

    private void DeletePurchase()
    {
        if (_purchaseId <= 0)
        {
            throw new InvalidOperationException("Selecione uma compra.");
        }

        if (MessageBox.Show("Excluir esta compra?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Service.DeleteCardPurchase(_purchaseId);
            ClearPurchaseForm();
        }
    }

    private void FillPurchaseForm()
    {
        _purchaseId = Ui.SelectedGridId(_purchasesGrid);
        if (_purchaseId == 0)
        {
            return;
        }

        _purchaseDescription.Text = Ui.CellText(_purchasesGrid, "Descricao");
        _purchaseAmount.Text = Ui.CellMoney(_purchasesGrid, "Valor").ToString("0.00");
        _purchaseDate.Value = Ui.CellDate(_purchasesGrid, "Data");
        Ui.SelectLookupByName(_purchaseCard, Ui.CellText(_purchasesGrid, "Cartao"));
        Ui.SelectLookupByName(_purchaseCategory, Ui.CellText(_purchasesGrid, "Categoria"));
        _purchaseInstallments.Value = 1;
        _purchaseNotes.Text = Ui.CellText(_purchasesGrid, "Observacoes");
    }

    private void ClearPurchaseForm()
    {
        _purchaseId = 0;
        _purchaseDescription.Clear();
        _purchaseAmount.Text = "0,00";
        _purchaseDate.Value = DateTime.Today;
        _purchaseInstallments.Value = 1;
        _purchaseNotes.Clear();
        if (_purchaseCard.Items.Count > 0) _purchaseCard.SelectedIndex = 0;
        if (_purchaseCategory.Items.Count > 0) _purchaseCategory.SelectedIndex = 0;
    }

    private void CloseInvoice()
    {
        Service.CloseInvoice(Ui.SelectedId(_invoiceCard), _invoiceActionMonth.Text);
    }

    private void PayInvoice()
    {
        var invoiceId = Ui.SelectedGridId(_invoicesGrid);
        if (invoiceId <= 0)
        {
            throw new InvalidOperationException("Selecione uma fatura.");
        }

        Service.PayInvoice(invoiceId, Ui.SelectedId(_invoiceAccount));
    }

    private void FillInvoiceForm()
    {
        if (Ui.SelectedGridId(_invoicesGrid) <= 0)
        {
            return;
        }

        Ui.SelectLookupByName(_invoiceCard, Ui.CellText(_invoicesGrid, "Cartao"));
        _invoiceActionMonth.Text = Ui.CellText(_invoicesGrid, "Mes");
    }

    private void ExportPurchases()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "compras-cartao.csv" };
        if (dialog.ShowDialog() == DialogResult.OK && Ui.BoundTable(_purchasesGrid) is { } table)
        {
            Service.ExportTableToCsv(table, dialog.FileName);
            Ui.ShowInfo("Arquivo exportado.");
        }
    }
}
