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
    private void ShowPlanning()
    {
        Heading("Planejamento", _month.ToString("MMMM 'de' yyyy"));
        MonthPicker();

        var budgets = _service.SearchBudgets(_month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        var goals = _service.SearchGoals("");
        var recurring = _service.SearchRecurringExpenses("");
        var installments = _service.SearchInstallmentGroups();
        var activeGoals = goals.Rows.Cast<DataRow>().Count(row => Str(row, "Status") == "Ativa");
        var activeRecurring = recurring.Rows.Cast<DataRow>().Count(row => Str(row, "Status") == "Ativa");

        MenuRow("Or\u00e7amentos", $"{budgets.Rows.Count} categorias neste m\u00eas", "\ue6c4", () => Navigate("budgets"));
        MenuRow("Metas", $"{activeGoals} ativas - {goals.Rows.Count} no total", "\ue153", () => Navigate("goals"));
        MenuRow("Recorr\u00eancias", $"{activeRecurring} compromissos ativos", "\ue040", () => Navigate("recurring"));
        MenuRow("Parcelamentos", $"{installments.Rows.Count} grupos de parcelas", "\ue242", () => Navigate("installments"));

        if (budgets.Rows.Count > 0)
        {
            Section("Or\u00e7amento do m\u00eas");
            var limit = budgets.Rows.Cast<DataRow>().Sum(row => Value(row, "Limite"));
            var spent = budgets.Rows.Cast<DataRow>().Sum(row => Value(row, "Gasto"));
            Pair(_content, "Planejado", Cash(limit));
            _ui.Gap(_content, 8);
            Pair(_content, "Gasto", Cash(spent), PlanningBudgetColor(limit > 0 ? spent / limit * 100 : 0));
            _ui.Progress(_content, limit > 0 ? spent / limit * 100 : 0, PlanningBudgetColor(limit > 0 ? spent / limit * 100 : 0));
        }
    }

    private void ShowBudgets()
    {
        Heading("Or\u00e7amentos", "Limites por categoria", () => EditBudget(null));
        MonthPicker();
        var budgets = _service.SearchBudgets(_month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        if (budgets.Rows.Count == 0)
        {
            Empty("Nenhum or\u00e7amento neste m\u00eas", "Defina o limite de uma categoria para este per\u00edodo.",
                "Criar or\u00e7amento", () => EditBudget(null));
            return;
        }

        foreach (DataRow budget in budgets.Rows)
        {
            var percentage = Value(budget, "Percentual");
            var color = PlanningBudgetColor(percentage);
            var spent = Value(budget, "Gasto");
            var limit = Value(budget, "Limite");
            var card = PlanningCard(Str(budget, "Categoria"), "\ue6c4", () => EditBudget(budget));
            Pair(card, "Gasto", Cash(spent), color);
            _ui.Gap(card, 8);
            Pair(card, "Limite", Cash(limit));
            _ui.Progress(card, percentage, color);
            var alert = _ui.Row();
            alert.AddView(_ui.Text(Str(budget, "Alerta"), 12, color), MobileUi.Grow());
            alert.AddView(_ui.Text($"{percentage:0.#}%", 12, color, true));
            card.AddView(alert);
            _ui.Gap(card, 10);
            Pair(card, spent > limit ? "Excedido" : "Restante", Cash(Math.Abs(limit - spent)), color);
        }
    }

    private void ShowGoals()
    {
        Heading("Metas", "Objetivos e reservas", () => EditGoal(null));
        var goals = _service.SearchGoals("");
        if (goals.Rows.Count == 0)
        {
            Empty("Qual \u00e9 sua pr\u00f3xima conquista?", "Comece com um objetivo e uma data.", "Criar meta", () => EditGoal(null));
            return;
        }

        foreach (DataRow goal in goals.Rows)
        {
            var percentage = Value(goal, "Percentual");
            var current = Value(goal, "ValorAtual");
            var target = Value(goal, "ValorObjetivo");
            var status = Str(goal, "Status");
            var color = status == "Cancelada" ? _ui.Muted : _ui.Accent;
            var card = PlanningCard(Str(goal, "Nome"), "\ue153", () => EditGoal(goal));
            var description = Str(goal, "Descricao");
            if (description.Length > 0)
            {
                card.AddView(_ui.Text(description, 13, _ui.Muted));
                _ui.Gap(card, 10);
            }
            Pair(card, "Guardado", Cash(current), color);
            _ui.Gap(card, 8);
            Pair(card, "Objetivo", Cash(target));
            _ui.Progress(card, percentage, color);
            Pair(card, "Faltam", Cash(Math.Max(0, target - current)));
            _ui.Gap(card, 8);
            Pair(card, "Prazo", DateLabel(Str(goal, "DataAlvo")));
            _ui.Gap(card, 12);

            var actions = _ui.Row();
            var caption = _ui.Column();
            caption.AddView(_ui.Text(status, 12, color, true));
            _ui.Gap(caption, 4);
            caption.AddView(_ui.Text($"{percentage:0.#}% conclu\u00eddo", 12, _ui.Muted));
            actions.AddView(caption, MobileUi.Grow());
            actions.AddView(_ui.IconButton("\ue148", $"Adicionar aporte em {Str(goal, "Nome")}", () => Run(() => AddContribution(Id(goal)))));
            actions.AddView(_ui.IconButton("\ue889", $"Hist\u00f3rico de aportes de {Str(goal, "Nome")}", () => Run(() => PlanningGoalHistory(goal))));
            card.AddView(actions);
        }
    }

    private void ShowRecurring()
    {
        Heading("Recorr\u00eancias", "Compromissos mensais", () => EditRecurring(null));
        var recurring = _service.SearchRecurringExpenses("");
        if (recurring.Rows.Count == 0)
        {
            Empty("Nenhuma recorr\u00eancia cadastrada", "Cadastre um compromisso mensal para gerar seus vencimentos.",
                "Criar recorr\u00eancia", () => EditRecurring(null));
            return;
        }

        var generate = _ui.Button("Gerar despesas recorrentes", () => Run(PlanningChooseRecurringRange));
        generate.Enabled = recurring.Rows.Cast<DataRow>().Any(row => Str(row, "Status") == "Ativa");
        _content.AddView(generate);
        _ui.Gap(_content, 16);
        foreach (DataRow item in recurring.Rows)
        {
            var active = Str(item, "Status") == "Ativa";
            var card = PlanningCard(Str(item, "Descricao"), "\ue040", () => EditRecurring(item));
            Pair(card, "Valor mensal", Cash(Value(item, "Valor")), active ? _ui.Ink : _ui.Muted);
            _ui.Gap(card, 8);
            Pair(card, "Vencimento", $"Dia {Str(item, "Dia")}");
            _ui.Gap(card, 8);
            card.AddView(_ui.Text($"{Str(item, "Categoria")} - {Str(item, "Conta")}", 13, _ui.Muted));
            var method = Str(item, "Forma");
            if (method.Length > 0)
            {
                _ui.Gap(card, 6);
                card.AddView(_ui.Text(method, 12, _ui.Muted));
            }
            _ui.Gap(card, 8);
            var end = Str(item, "Fim");
            card.AddView(_ui.Text($"Desde {DateLabel(Str(item, "Inicio"))}" + (end.Length > 0 ? $" at\u00e9 {DateLabel(end)}" : " - sem data final"), 12, _ui.Muted));
            _ui.Gap(card, 8);
            card.AddView(_ui.Text(Str(item, "Status"), 12, active ? _ui.Accent : _ui.Muted, true));
        }
    }

    private void ShowInstallments()
    {
        Heading("Parcelamentos", "Compras e despesas parceladas", NewInstallment);
        var groups = _service.SearchInstallmentGroups();
        if (groups.Rows.Count == 0)
        {
            Empty("Nenhum parcelamento cadastrado", "Organize as parcelas de uma compra ou despesa.", "Criar parcelamento", NewInstallment);
            return;
        }

        foreach (DataRow group in groups.Rows)
        {
            var card = PlanningCard(Str(group, "Descricao"), "\ue242", () => PlanningInstallmentDetails(group));
            Pair(card, "Valor total", Cash(Value(group, "ValorTotal")));
            _ui.Gap(card, 8);
            Pair(card, "Parcelas", Str(group, "Parcelas"));
            _ui.Gap(card, 8);
            Pair(card, "Primeiro vencimento", DateLabel(Str(group, "PrimeiroVencimento")));
            _ui.Gap(card, 10);
            card.AddView(_ui.Text(Str(group, "Categoria"), 13, _ui.Muted));
            var payment = Str(group, "Cartao");
            if (payment.Length == 0) payment = Str(group, "Conta");
            if (payment.Length > 0)
            {
                _ui.Gap(card, 6);
                card.AddView(_ui.Text(payment, 12, _ui.Muted));
            }
        }
    }

    private LinearLayout PlanningCard(string title, string glyph, Action click)
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

    private Color PlanningBudgetColor(decimal percentage) => percentage >= 100 ? _ui.Danger : percentage >= 80 ? _ui.Warning : _ui.Accent;

    private void PlanningGoalHistory(DataRow goal)
    {
        var entries = _service.GetGoalContributions(Id(goal)).Rows.Cast<DataRow>().ToArray();
        var title = $"Aportes de {Str(goal, "Nome")}";
        if (entries.Length == 0)
        {
            new AlertDialog.Builder(this).SetTitle(title).SetMessage("Nenhum aporte registrado.")
                .SetNegativeButton("Fechar", (_, _) => { })
                .SetPositiveButton("Adicionar aporte", (_, _) => Run(() => AddContribution(Id(goal)))).Show();
            return;
        }

        var labels = entries.Select(row => $"{DateLabel(Str(row, "Data"))} - {Cash(Value(row, "Valor"))}").ToArray();
        new AlertDialog.Builder(this).SetTitle(title).SetItems(labels, (_, args) => Run(() =>
        {
            var entry = entries[args.Which];
            var notes = Str(entry, "Observacoes");
            new AlertDialog.Builder(this).SetTitle("Detalhes do aporte")
                .SetMessage($"Data: {DateLabel(Str(entry, "Data"))}\nValor: {Cash(Value(entry, "Valor"))}\n\n{(notes.Length > 0 ? notes : "Sem observa\u00e7\u00f5es.")}")
                .SetPositiveButton("Fechar", (_, _) => { }).Show();
        })).SetNegativeButton("Fechar", (_, _) => { })
            .SetPositiveButton("Adicionar aporte", (_, _) => Run(() => AddContribution(Id(goal)))).Show();
    }

    private void PlanningChooseRecurringRange()
    {
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var choices = Enumerable.Range(0, 25)
            .Select(months => $"{months} {(months == 1 ? "m\u00eas futuro" : "meses futuros")} - at\u00e9 {thisMonth.AddMonths(months):MM/yyyy}")
            .ToArray();
        new AlertDialog.Builder(this).SetTitle("Gerar despesas recorrentes")
            .SetItems(choices, (_, args) => Run(() =>
            {
                var monthsAhead = args.Which;
                var until = thisMonth.AddMonths(monthsAhead);
                Confirm("Gerar despesas?",
                    $"Ser\u00e3o gerados os vencimentos das recorr\u00eancias ativas desde a data inicial de cada cadastro at\u00e9 {until:MM/yyyy}, respeitando a data final. Vencimentos j\u00e1 gerados ser\u00e3o preservados.",
                    () =>
                    {
                        var count = _service.GenerateRecurringExpenses(monthsAhead);
                        RefreshPage();
                        new AlertDialog.Builder(this).SetTitle("Recorr\u00eancias atualizadas")
                            .SetMessage(count == 0 ? "Nenhum vencimento novo para gerar nesse per\u00edodo." : $"{count} despesa(s) gerada(s).")
                            .SetPositiveButton("OK", (_, _) => { }).Show();
                    });
            })).SetNegativeButton("Cancelar", (_, _) => { }).Show();
    }

    private void PlanningInstallmentDetails(DataRow group)
    {
        var entries = _service.GetInstallmentDetails(Id(group)).Rows.Cast<DataRow>()
            .OrderBy(row => int.TryParse(Str(row, "Parcela").Split('/')[0], out var number) ? number : int.MaxValue)
            .ToArray();
        var body = _ui.Column(20);
        Pair(body, "Valor total", Cash(Value(group, "ValorTotal")));
        _ui.Gap(body, 8);
        Pair(body, "Parcelas", Str(group, "Parcelas"));
        if (entries.Length == 0)
        {
            _ui.Gap(body, 16);
            body.AddView(_ui.Text("Nenhuma parcela vinculada.", 13, _ui.Muted));
        }
        foreach (var entry in entries)
        {
            _ui.Divider(body);
            var status = Str(entry, "Status");
            var color = status is "Pago" or "Paga" ? _ui.Accent : status == "Vencido" ? _ui.Danger : _ui.Warning;
            body.AddView(_ui.Text(Str(entry, "Descricao"), 14, bold: true));
            _ui.Gap(body, 8);
            Pair(body, $"Parcela {Str(entry, "Parcela")}", Cash(Value(entry, "Valor")), color);
            _ui.Gap(body, 6);
            var due = Str(entry, "Vencimento");
            var isMonth = DateTime.TryParseExact(due, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month);
            body.AddView(_ui.Text($"{(isMonth ? "Fatura" : "Vencimento")}: {(isMonth ? month.ToString("MM/yyyy") : DateLabel(due))}", 12, _ui.Muted));
            _ui.Gap(body, 6);
            body.AddView(_ui.Text(status, 12, color, true));
        }
        var scroll = new ScrollView(this);
        scroll.AddView(body);
        new AlertDialog.Builder(this).SetTitle(Str(group, "Descricao")).SetView(scroll)
            .SetPositiveButton("Fechar", (_, _) => { }).Show();
    }
}
