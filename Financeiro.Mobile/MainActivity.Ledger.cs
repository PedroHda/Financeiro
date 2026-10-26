using System.Data;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Financeiro.Mobile.UI;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private sealed record LedgerItem(string Kind, DataRow Row, string Description, string Category, string Account, DateTime Date, decimal Amount, string Status);

    private List<LedgerItem> ReadLedger()
    {
        var filter = MonthFilter();
        var items = new List<LedgerItem>();
        foreach (var (kind, table, dateColumn) in new[]
        {
            ("Receita", _service.SearchIncomes(filter, "", "Data"), "Data"),
            ("Despesa", _service.SearchExpenses(filter, "", "Data"), "Vencimento"),
            ("Cartão", _service.SearchCardPurchases(filter, ""), "Data")
        })
        {
            foreach (DataRow row in table.Rows)
            {
                items.Add(new LedgerItem(kind, row, Str(row, "Descricao"), Str(row, "Categoria"),
                    kind == "Cartão" ? Str(row, "Cartao") : Str(row, "Conta"),
                    DateTime.Parse(Str(row, dateColumn), System.Globalization.CultureInfo.InvariantCulture), Value(row, "Valor"), Str(row, "Status")));
            }
        }
        foreach (System.Data.DataRow row in _service.SearchTransfers(filter.StartDate, filter.EndDate).Rows)
            items.Add(new LedgerItem("Transferência", row, Str(row, "Origem") + " → " + Str(row, "Destino"), "Transferência", "", DateTime.Parse(Str(row, "Data")), Value(row, "Valor"), "Realizada"));
        return items;
    }

    private void ShowLedger()
    {
        Heading("Movimentos", "Seu extrato, do seu jeito.", ChooseNewTransaction);
        MonthPicker();
        var search = new EditText(this)
        {
            Hint = "Buscar descrição, categoria ou conta",
            Text = _search,
            TextSize = 14,
            InputType = InputTypes.ClassText,
            Background = _ui.Background(_ui.Surface)
        };
        search.SetTextColor(_ui.Ink);
        search.SetSingleLine(true);
        search.SetHintTextColor(_ui.Muted);
        search.SetPadding(_ui.Dp(14), _ui.Dp(10), _ui.Dp(14), _ui.Dp(10));
        _content.AddView(search, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, _ui.Dp(50)));
        _ui.Gap(_content, 12);
        var choices = _ui.Row();
        var type = Choice(new[] { "Todos", "Receitas", "Despesas", "Cartão", "Transferências" }, _ledgerType);
        var status = Choice(new[] { "Todos", "Pendentes", "Pagos", "Vencidos" }, _ledgerStatus);
        choices.AddView(type, MobileUi.Grow());
        choices.AddView(status, MobileUi.Grow());
        _content.AddView(choices);
        _ui.Gap(_content, 14);
        var list = _ui.Column();
        _content.AddView(list);
        var items = ReadLedger();

        void Fill()
        {
            list.RemoveAllViews();
            var selected = items.Where(item =>
                (_ledgerType == "Todos" || _ledgerType == "Receitas" && item.Kind == "Receita" || _ledgerType == "Despesas" && item.Kind == "Despesa" || _ledgerType == "Cartão" && item.Kind == "Cartão" || _ledgerType == "Transferências" && item.Kind == "Transferência") &&
                (_ledgerStatus == "Todos" || _ledgerStatus == "Pendentes" && item.Status is "Pendente" or "Aberta" or "Fechada" || _ledgerStatus == "Pagos" && item.Status is "Pago" or "Recebido" or "Paga" || _ledgerStatus == "Vencidos" && item.Status == "Vencido") &&
                (item.Description + " " + item.Category + " " + item.Account).Contains(_search, StringComparison.OrdinalIgnoreCase)).OrderByDescending(item => item.Date).ToList();
            if (selected.Count == 0)
            {
                list.AddView(_ui.Text("Nenhuma movimentação encontrada", 15, _ui.Muted));
                return;
            }
            DateTime? previous = null;
            foreach (var item in selected)
            {
                if (previous != item.Date.Date)
                {
                    _ui.Gap(list, 12);
                    list.AddView(_ui.Text(item.Date.Date == DateTime.Today ? "Hoje" : item.Date.ToString("dd 'de' MMMM"), 12, _ui.Muted, true));
                    _ui.Gap(list, 8);
                    previous = item.Date.Date;
                }
                list.AddView(TransactionRow(item));
            }
        }
        search.TextChanged += (_, _) => { _search = search.Text ?? ""; Fill(); };
        type.ItemSelected += (_, _) => { _ledgerType = type.SelectedItem?.ToString() ?? "Todos"; Fill(); };
        status.ItemSelected += (_, _) => { _ledgerStatus = status.SelectedItem?.ToString() ?? "Todos"; Fill(); };
        Fill();
    }

    private Spinner Choice(string[] choices, string current)
    {
        var spinner = new Spinner(this) { Background = _ui.Background(_ui.Soft), ContentDescription = "Filtro" };
        var adapter = new MobileChoiceAdapter(_ui, choices);
        adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        spinner.Adapter = adapter;
        spinner.SetSelection(Math.Max(0, Array.IndexOf(choices, current)));
        return spinner;
    }

    private View TransactionRow(LedgerItem item)
    {
        var row = _ui.Row();
        row.SetPadding(_ui.Dp(12), _ui.Dp(14), _ui.Dp(12), _ui.Dp(14));
        row.Background = _ui.Background(_ui.Surface);
        row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = _ui.Dp(7) };
        var positive = item.Kind == "Receita";
        var icon = _ui.Icon(positive ? "\uf1e5" : item.Kind == "Cartão" ? "\ue870" : "\uf1e1", positive ? _ui.Accent : _ui.Danger, 22);
        icon.Background = _ui.Background(_ui.Soft);
        row.AddView(icon, new LinearLayout.LayoutParams(_ui.Dp(38), _ui.Dp(38)) { RightMargin = _ui.Dp(10) });
        var description = _ui.Column();
        description.AddView(_ui.Text(item.Description, 14, bold: true));
        _ui.Gap(description, 5);
        description.AddView(_ui.Text(item.Category + " • " + item.Account, 11, _ui.Muted));
        row.AddView(description, MobileUi.Grow());
        var amount = _ui.Column();
        amount.SetGravity(GravityFlags.Right);
        amount.SetPadding(_ui.Dp(8), 0, 0, 0);
        amount.AddView(_ui.Text((positive ? "+ " : item.Kind == "Transferência" ? "" : "− ") + Cash(item.Amount), 13, positive ? _ui.Accent : _ui.Ink, true));
        _ui.Gap(amount, 5);
        amount.AddView(_ui.Text(item.Status, 10, item.Status == "Vencido" ? _ui.Danger : _ui.Muted));
        row.AddView(amount);
        row.Focusable = true;
        row.ContentDescription = item.Description + ", " + Cash(item.Amount) + ", " + item.Status;
        row.Click += (_, _) => Run(() =>
        {
            if (item.Kind == "Receita") EditIncome(item.Row);
            else if (item.Kind == "Cartão") EditPurchase(item.Row);
            else if (item.Kind == "Transferência") EditTransfer(item.Row);
            else EditExpense(item.Row);
        });
        if (item.Kind is "Receita" or "Despesa") row.LongClick += (_, _) => Run(() => EntryMenu(item));
        return row;
    }

    private void ChooseNewTransaction()
    {
        new AlertDialog.Builder(this).SetTitle("Novo lançamento")
            .SetItems(new[] { "Receita", "Despesa", "Compra no cartão", "Parcelamento", "Transferência" }, (_, e) => Run(() =>
            {
                switch (e.Which)
                {
                    case 0: EditIncome(); break;
                    case 1: EditExpense(); break;
                    case 2: EditPurchase(); break;
                    case 3: NewInstallment(); break;
                    case 4: EditTransfer(); break;
                }
            })).Show();
    }
}
