using System.Data;
using Android.Graphics;
using Android.Views;
using Financeiro.Mobile.UI;
using FinanceiroApp.Models;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private void ShowHome()
    {
        Heading("Visão geral", DateTime.Now.Hour < 12 ? "Bom dia. Seu dinheiro em dia." : DateTime.Now.Hour < 18 ? "Boa tarde. Seu dinheiro em dia." : "Boa noite. Seu dinheiro em dia.");
        MonthPicker();
        var summary = _service.GetDashboardSummary(_month);
        var balance = _ui.Card(20);
        balance.Background = _ui.Background(Color.ParseColor(_dark ? "#164C38" : "#0B6348"));
        balance.AddView(_ui.Text("Saldo em contas", 13, Color.ParseColor("#C5EADB")));
        _ui.Gap(balance, 10);
        balance.AddView(_ui.Text(Cash(summary.CurrentBalance), 32, Color.White, true));
        _ui.Gap(balance, 16);
        var result = _ui.Row();
        result.AddView(_ui.Icon(summary.MonthlyBalance >= 0 ? "\uf1e1" : "\uf1e5", Color.ParseColor("#C5EADB"), 18), new LinearLayout.LayoutParams(_ui.Dp(26), _ui.Dp(22)));
        result.AddView(_ui.Text("Resultado do mês  " + Cash(summary.MonthlyBalance), 13, Color.White));
        balance.AddView(result);
        _content.AddView(balance);

        var totals = _ui.Row();
        foreach (var (label, value, positive) in new[] { ("Receitas", summary.MonthlyIncome, true), ("Despesas", summary.MonthlyExpense, false) })
        {
            var column = _ui.Column();
            var labelRow = _ui.Row();
            labelRow.AddView(_ui.Icon(positive ? "\uf1e5" : "\uf1e1", positive ? _ui.Accent : _ui.Danger, 18), new LinearLayout.LayoutParams(_ui.Dp(24), _ui.Dp(24)));
            labelRow.AddView(_ui.Text(label, 13, _ui.Muted));
            column.AddView(labelRow);
            _ui.Gap(column, 5);
            column.AddView(_ui.Text(Cash(value), 20, positive ? _ui.Accent : _ui.Danger, true));
            totals.AddView(column, MobileUi.Grow());
        }
        _content.AddView(totals);
        _ui.Gap(_content, 20);
        var quick = _ui.Row();
        quick.AddView(_ui.Button("+ Receita", () => Run(() => EditIncome())), new LinearLayout.LayoutParams(0, _ui.Dp(48), 1) { RightMargin = _ui.Dp(5) });
        quick.AddView(_ui.Button("− Despesa", () => Run(() => EditExpense()), false), new LinearLayout.LayoutParams(0, _ui.Dp(48), 1) { LeftMargin = _ui.Dp(5) });
        _content.AddView(quick);

        if (_service.GetAccounts(false).Count == 0)
        {
            Section("Sua carteira");
            Empty("Sua primeira conta", "Nenhuma conta cadastrada.", "Criar conta", () => EditAccount());
        }

        var overdue = _service.GetOverdue();
        if (overdue.Rows.Count > 0)
        {
            Section("Atenção aos vencimentos");
            MenuRow($"{overdue.Rows.Count} conta(s) vencida(s)", Cash(overdue.Rows.Cast<DataRow>().Sum(row => Value(row, "Valor"))), "\ue002", () => Navigate("overdue"));
        }
        else if (summary.ExpensesAboveIncome)
        {
            Section("Atenção ao mês");
            var warning = _ui.Card();
            warning.AddView(_ui.Text("Despesas acima das receitas", 14, _ui.Danger, true));
            _ui.Gap(warning, 6);
            warning.AddView(_ui.Text("Diferença de " + Cash(summary.MonthlyExpense - summary.MonthlyIncome), 13, _ui.Muted));
            _content.AddView(warning);
        }

        Section("Para onde vai o dinheiro");
        var categories = _service.GetDashboardCategoryExpenses(_month);
        if (categories.Rows.Count == 0) Empty("Nenhuma despesa neste mês", "Os gastos registrados aparecerão aqui.");
        else
        {
            var total = categories.Rows.Cast<DataRow>().Sum(row => Value(row, "Total"));
            var spending = _ui.Card();
            var colors = new[] { _ui.Accent, Color.ParseColor("#5A83C6"), _ui.Warning, Color.ParseColor("#B779A2"), _ui.Muted };
            var index = 0;
            foreach (DataRow row in categories.Rows.Cast<DataRow>().Take(5))
            {
                Pair(spending, Str(row, "Categoria"), Cash(Value(row, "Total")));
                _ui.Progress(spending, total == 0 ? 0 : Value(row, "Total") / total * 100, colors[index++]);
                _ui.Gap(spending, 8);
            }
            _content.AddView(spending);
        }

        Section("Próximos 7 dias");
        var due = _service.GetDueSoon();
        if (due.Rows.Count == 0) Empty("Tudo em dia", "Nenhuma conta próxima do vencimento.");
        else foreach (DataRow row in due.Rows.Cast<DataRow>().Take(4))
            MenuRow(Str(row, "Descricao"), DateLabel(Str(row, "Vencimento")) + "  •  " + Cash(Value(row, "Valor")), "\ue8b5", () => Navigate(Str(row, "Tipo") == "Fatura" ? "invoices" : "ledger"));

        Section("Últimas movimentações");
        var recent = ReadLedger().OrderByDescending(item => item.Date).Take(4).ToList();
        if (recent.Count == 0) Empty("Nenhum lançamento", "Seu extrato começa com o primeiro registro.");
        else foreach (var item in recent) _content.AddView(TransactionRow(item));
    }

    private void ShowOverdue()
    {
        Heading("Contas vencidas", "Pendências de todos os meses.");
        var filter = new ReportFilter { StartDate = new DateTime(1900, 1, 1), EndDate = DateTime.Today.AddDays(-1), Status = "Vencido" };
        var expenses = _service.SearchExpenses(filter, "", "Data");
        foreach (DataRow row in expenses.Rows)
        {
            MenuRow(Str(row, "Descricao"), DateLabel(Str(row, "Vencimento")) + " • " + Cash(Value(row, "Valor")), "\ue002", () => EditExpense(row));
        }
        var invoices = _service.SearchInvoices("").Rows.Cast<DataRow>().Where(row => Str(row, "Status") == "Fechada" && DateTime.Parse(Str(row, "Vencimento"), System.Globalization.CultureInfo.InvariantCulture) < DateTime.Today).ToList();
        foreach (var invoice in invoices)
        {
            MenuRow("Fatura " + Str(invoice, "Cartao"), DateLabel(Str(invoice, "Vencimento")) + " • " + Cash(Value(invoice, "Valor")), "\uef6e", () => PayInvoice(invoice));
        }
        if (expenses.Rows.Count == 0 && invoices.Count == 0) Empty("Tudo em dia", "Nenhuma conta vencida.");
    }
}
