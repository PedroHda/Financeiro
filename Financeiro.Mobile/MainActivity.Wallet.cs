using System.Data;
using System.Globalization;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Financeiro.Mobile.UI;

namespace Financeiro.Mobile;

public partial class MainActivity : Activity
{
    private void ShowWallet()
    {
        Heading("Carteira", "Contas e cart\u00f5es");
        _service.RefreshDerivedData();
        var accounts = _service.SearchAccounts("").Rows.Cast<DataRow>().ToArray();
        var cards = _service.SearchCreditCards("").Rows.Cast<DataRow>().ToArray();
        var activeAccounts = accounts.Where(row => Str(row, "Status") == "Ativo").ToArray();
        var activeCards = cards.Where(row => Str(row, "Status") == "Ativo").ToArray();
        var balance = activeAccounts.Sum(row => Value(row, "SaldoAtual"));

        _content.AddView(_ui.Text("Saldo nas contas ativas", 13, _ui.Muted));
        _ui.Gap(_content, 8);
        _content.AddView(_ui.Text(Cash(balance), 28, balance < 0 ? _ui.Danger : _ui.Ink, true));
        _ui.Gap(_content, 16);
        Pair(_content, "Limite dispon\u00edvel", Cash(activeCards.Sum(row => Value(row, "LimiteDisponivel"))));
        _ui.Gap(_content, 20);

        MenuRow("Contas", $"{activeAccounts.Length} ativas - {accounts.Length} no total", "\ue84f", () => Navigate("accounts"));
        MenuRow("Cart\u00f5es", $"{activeCards.Length} ativos - {cards.Length} no total", "\ue870", () => Navigate("cards"));
        MenuRow("Compras", _month.ToString("MMMM 'de' yyyy"), "\ue8cc", () => Navigate("purchases"));
        MenuRow("Faturas", _month.ToString("MMMM 'de' yyyy"), "\uef6e", () => Navigate("invoices"));

        Section("Saldos por conta");
        if (activeAccounts.Length == 0)
            Empty("Nenhuma conta ativa", "Cadastre uma conta para organizar seus saldos.", "Nova conta", () => EditAccount(null));
        else
            foreach (var account in activeAccounts) WalletAccountCard(account, false);

        Section("Limites por cart\u00e3o");
        if (activeCards.Length == 0)
            Empty("Nenhum cart\u00e3o ativo", "Cadastre um cart\u00e3o para acompanhar o limite dispon\u00edvel.", "Novo cart\u00e3o", () => EditCard(null));
        else
            foreach (var card in activeCards) WalletCreditCard(card, false);
    }

    private void ShowAccounts()
    {
        Heading("Contas", "Saldos e cadastros", () => EditAccount(null));
        _service.RefreshDerivedData();
        var accounts = _service.SearchAccounts("");
        if (accounts.Rows.Count == 0)
        {
            Empty("Nenhuma conta cadastrada", "Adicione sua conta banc\u00e1ria ou carteira.", "Nova conta", () => EditAccount(null));
            return;
        }
        foreach (DataRow account in accounts.Rows) WalletAccountCard(account, true);
    }

    private void ShowCards()
    {
        Heading("Cart\u00f5es", "Limites e fechamento de faturas", () => EditCard(null));
        MonthPicker();
        _service.RefreshDerivedData();
        var cards = _service.SearchCreditCards("");
        if (cards.Rows.Count == 0)
        {
            Empty("Nenhum cart\u00e3o cadastrado", "Adicione um cart\u00e3o e suas datas de fechamento e vencimento.", "Novo cart\u00e3o", () => EditCard(null));
            return;
        }

        MenuRow("Compras do m\u00eas", $"Compradas em {_month:MM/yyyy}", "\ue8cc", () => Navigate("purchases"));
        MenuRow("Faturas do m\u00eas", $"Refer\u00eancia {_month:MM/yyyy}", "\uef6e", () => Navigate("invoices"));
        _ui.Gap(_content, 8);
        foreach (DataRow card in cards.Rows) WalletCreditCard(card, true);
    }

    private void ShowPurchases()
    {
        Heading("Compras", "Compras no cart\u00e3o", () => EditPurchase(null));
        MonthPicker();
        var purchases = _service.SearchCardPurchases(MonthFilter(), "");
        if (purchases.Rows.Count == 0)
        {
            Empty("Nenhuma compra neste m\u00eas", "Registre uma compra com sua categoria e cart\u00e3o.", "Nova compra", () => EditPurchase(null));
            return;
        }

        Pair(_content, $"{purchases.Rows.Count} compra(s)", Cash(purchases.Rows.Cast<DataRow>().Sum(row => Value(row, "Valor"))));
        _ui.Gap(_content, 16);
        foreach (DataRow purchase in purchases.Rows)
        {
            var card = WalletCard(Str(purchase, "Descricao"), "\ue8cc", () => EditPurchase(purchase));
            var status = Str(purchase, "Status");
            Pair(card, "Valor", Cash(Value(purchase, "Valor")));
            _ui.Gap(card, 8);
            card.AddView(_ui.Text($"{Str(purchase, "Cartao")} - {Str(purchase, "Categoria")}", 13, _ui.Muted));
            _ui.Gap(card, 8);
            Pair(card, "Compra", DateLabel(Str(purchase, "Data")));
            _ui.Gap(card, 8);
            Pair(card, "Fatura", WalletMonthLabel(Str(purchase, "Fatura")));
            var installment = Str(purchase, "Parcela");
            if (installment.Length > 0 && installment is not "1/1" and not "0/0")
            {
                _ui.Gap(card, 8);
                Pair(card, "Parcela", installment);
            }
            _ui.Gap(card, 10);
            card.AddView(_ui.Text(status, 12, WalletStatusColor(status), true));
        }
    }

    private void ShowInvoices()
    {
        Heading("Faturas", "Fechamento e pagamento");
        MonthPicker();
        MenuRow("Fechar fatura", $"Refer\u00eancia {_month:MM/yyyy}", "\ue862", WalletChooseInvoiceCard);
        var invoices = _service.SearchInvoices(_month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        if (invoices.Rows.Count == 0)
        {
            Empty("Nenhuma fatura neste m\u00eas", "As faturas aparecem aqui ap\u00f3s o fechamento.", "Fechar fatura", WalletChooseInvoiceCard);
            return;
        }

        _ui.Gap(_content, 8);
        foreach (DataRow invoice in invoices.Rows)
        {
            var status = Str(invoice, "Status");
            var overdue = status == "Fechada"
                && DateTime.TryParseExact(Str(invoice, "Vencimento"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due)
                && due.Date < DateTime.Today;
            var color = overdue ? _ui.Danger : WalletStatusColor(status);
            var card = WalletCard(Str(invoice, "Cartao"), "\uef6e", () =>
            {
                if (status == "Fechada") PayInvoice(invoice);
                else WalletInvoiceDetails(invoice);
            });
            Pair(card, "Valor da fatura", Cash(Value(invoice, "Valor")), color);
            _ui.Gap(card, 8);
            Pair(card, "Refer\u00eancia", WalletMonthLabel(Str(invoice, "Mes")));
            _ui.Gap(card, 8);
            Pair(card, "Fechamento", DateLabel(Str(invoice, "Fechamento")));
            _ui.Gap(card, 8);
            Pair(card, "Vencimento", DateLabel(Str(invoice, "Vencimento")), overdue ? _ui.Danger : _ui.Ink);
            _ui.Gap(card, 10);
            card.AddView(_ui.Text(overdue ? $"{status} - vencida" : status, 12, color, true));
            if (status == "Paga")
            {
                _ui.Gap(card, 8);
                card.AddView(_ui.Text($"Paga em {DateLabel(Str(invoice, "PagoEm"))}", 12, _ui.Muted));
                var account = Str(invoice, "ContaPagamento");
                if (account.Length > 0)
                {
                    _ui.Gap(card, 6);
                    card.AddView(_ui.Text(account, 12, _ui.Muted));
                }
            }
            else if (status == "Fechada")
            {
                _ui.Gap(card, 10);
                card.AddView(_ui.Button("Pagar fatura", () => Run(() => PayInvoice(invoice)), false));
            }
        }
    }

    private LinearLayout WalletCard(string title, string glyph, Action click)
    {
        var card = _ui.Card();
        var heading = _ui.Row();
        heading.AddView(_ui.Icon(glyph, _ui.Accent), new LinearLayout.LayoutParams(_ui.Dp(32), _ui.Dp(36)));
        heading.AddView(_ui.Text(title, 16, bold: true), MobileUi.Grow());
        heading.AddView(_ui.Icon("\ue5cc", _ui.Muted, 20), new LinearLayout.LayoutParams(_ui.Dp(24), _ui.Dp(36)));
        card.AddView(heading);
        _ui.Gap(card, 12);
        card.Focusable = true;
        card.ContentDescription = title;
        card.Click += (_, _) => Run(click);
        _content.AddView(card);
        return card;
    }

    private void WalletAccountCard(DataRow account, bool details)
    {
        var card = WalletCard(Str(account, "Nome"), "\ue84f", () => EditAccount(account));
        var balance = Value(account, "SaldoAtual");
        Pair(card, "Saldo atual", Cash(balance), balance < 0 ? _ui.Danger : _ui.Accent);
        if (details)
        {
            _ui.Gap(card, 8);
            Pair(card, "Saldo inicial", Cash(Value(account, "SaldoInicial")));
        }
        _ui.Gap(card, 10);
        card.AddView(_ui.Text($"{Str(account, "Tipo")} - {Str(account, "Status")}", 12, _ui.Muted));
        var notes = Str(account, "Observacoes");
        if (details && notes.Length > 0)
        {
            _ui.Gap(card, 8);
            card.AddView(_ui.Text(notes, 12, _ui.Muted));
        }
    }

    private void WalletCreditCard(DataRow creditCard, bool details)
    {
        var card = WalletCard(Str(creditCard, "Nome"), "\ue870", () => EditCard(creditCard));
        var total = Value(creditCard, "LimiteTotal");
        var available = Value(creditCard, "LimiteDisponivel");
        var used = total - available;
        var percentage = total > 0 ? used / total * 100 : 0;
        var color = available <= 0 ? _ui.Danger : percentage >= 80 ? _ui.Warning : _ui.Accent;
        Pair(card, "Dispon\u00edvel", Cash(available), color);
        _ui.Gap(card, 8);
        Pair(card, "Limite total", Cash(total));
        _ui.Progress(card, percentage, color);
        Pair(card, "Utilizado", Cash(used), color);
        _ui.Gap(card, 10);
        var institution = Str(creditCard, "Instituicao");
        card.AddView(_ui.Text((institution.Length > 0 ? institution + " - " : "") + Str(creditCard, "Status"), 12, _ui.Muted));
        if (!details) return;

        _ui.Divider(card);
        Pair(card, "Fechamento", $"Dia {Str(creditCard, "Fechamento")}");
        _ui.Gap(card, 8);
        Pair(card, "Vencimento", $"Dia {Str(creditCard, "Vencimento")}");
        _ui.Gap(card, 12);
        var actions = _ui.Row();
        actions.AddView(_ui.Text($"Fatura {_month:MM/yyyy}", 13, _ui.Muted), MobileUi.Grow());
        actions.AddView(_ui.IconButton("\ue862", $"Fechar fatura de {Str(creditCard, "Nome")} em {_month:MM/yyyy}", () => Run(() => CloseInvoice(Id(creditCard)))));
        card.AddView(actions);
    }

    private void WalletChooseInvoiceCard()
    {
        var cards = _service.SearchCreditCards("").Rows.Cast<DataRow>().ToArray();
        if (cards.Length == 0)
        {
            new AlertDialog.Builder(this).SetTitle("Nenhum cart\u00e3o cadastrado")
                .SetMessage("Cadastre um cart\u00e3o antes de fechar a fatura.")
                .SetNegativeButton("Cancelar", (_, _) => { })
                .SetPositiveButton("Novo cart\u00e3o", (_, _) => Run(() => EditCard(null))).Show();
            return;
        }
        var choices = cards.Select(row => Str(row, "Nome")
            + (Str(row, "Instituicao").Length > 0 ? $" - {Str(row, "Instituicao")}" : "")
            + (Str(row, "Status") == "Inativo" ? " (inativo)" : "")).ToArray();
        new AlertDialog.Builder(this).SetTitle($"Fechar fatura - {_month:MM/yyyy}")
            .SetItems(choices, (_, args) => Run(() => CloseInvoice(Id(cards[args.Which]))))
            .SetNegativeButton("Cancelar", (_, _) => { }).Show();
    }

    private void WalletInvoiceDetails(DataRow invoice)
    {
        var message = $"Refer\u00eancia: {WalletMonthLabel(Str(invoice, "Mes"))}\nValor: {Cash(Value(invoice, "Valor"))}"
            + $"\nFechamento: {DateLabel(Str(invoice, "Fechamento"))}\nVencimento: {DateLabel(Str(invoice, "Vencimento"))}\nStatus: {Str(invoice, "Status")}";
        if (Str(invoice, "Status") == "Paga")
            message += $"\nPaga em: {DateLabel(Str(invoice, "PagoEm"))}\nConta: {Str(invoice, "ContaPagamento")}";
        new AlertDialog.Builder(this).SetTitle(Str(invoice, "Cartao")).SetMessage(message)
            .SetPositiveButton("Fechar", (_, _) => { }).Show();
    }

    private Color WalletStatusColor(string status) => status switch
    {
        "Paga" or "Pago" => _ui.Accent,
        "Fechada" => _ui.Warning,
        "Vencido" => _ui.Danger,
        _ => _ui.Muted
    };

    private static string WalletMonthLabel(string value) =>
        DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("MM/yyyy") : value;
}
