using System.Data;
using Android.Content;
using Android.Views;
using Financeiro.Mobile.Forms;
using Financeiro.Mobile.UI;
using FinanceiroApp.Models;
using FinanceiroApp.Services;
using Path = System.IO.Path;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private List<ImportEntry> _importEntries = [];
    private int _importAccountId;
    private int _defaultIncomeCategory;
    private int _defaultExpenseCategory;
    private int _importPage;
    private bool _busy;

    private void ShowOperations()
    {
        Heading("Operações", "Contas e movimentos");
        MenuRow("Transferências", "Entre suas contas", "\ue8d5", () => Navigate("transfers"));
        MenuRow("Conciliação", "Conferência dos saldos", "\ue86c", () => Navigate("reconciliation"));
        MenuRow("Importar extrato", "OFX e CSV", "\ue2c6", ChooseStatement);
        MenuRow("Regras de categorias", "Categorias por descrição", "\ue8b8", () => Navigate("rules"));
        MenuRow("Previsão de saldo", "Compromissos dos próximos meses", "\ue6c4", () => Navigate("forecast"));
    }

    private void ShowTransfers()
    {
        Heading("Transferências", "Entre suas contas", () => EditTransfer());
        MonthPicker();
        var table = _service.SearchTransfers(_month, _month.AddMonths(1).AddDays(-1));
        foreach (DataRow row in table.Rows)
        {
            var copy = row;
            MenuRow(Str(row, "Origem") + " → " + Str(row, "Destino"), Cash(Value(row, "Valor")) + " · " + DateLabel(Str(row, "Data")), "\ue8d5", () => EditTransfer(copy));
        }
        if (table.Rows.Count == 0) Empty("Nenhuma transferência", "Este mês", "Nova transferência", () => EditTransfer());
    }

    private void EditTransfer(DataRow? row = null)
    {
        var form = new EntryForm(_ui, RefreshPage);
        var from = form.Lookup("Conta de origem", _service.GetAccounts(), row is null ? "" : Str(row, "Origem"));
        var to = form.Lookup("Conta de destino", _service.GetAccounts(), row is null ? "" : Str(row, "Destino"));
        var amount = form.Money("Valor (R$)", row is null ? 0 : Value(row, "Valor"));
        var date = form.Date("Data", row is null ? DateTime.Today : EditorDate(row, "Data", DateTime.Today));
        var notes = form.Text("Observações", row is null ? "" : Str(row, "Observacoes"), true);
        form.Show(row is null ? "Nova transferência" : "Editar transferência", () => _service.SaveTransfer(new TransferInput
        {
            Id = row is null ? 0 : Id(row), FromAccountId = from.Value.Id, ToAccountId = to.Value.Id,
            Amount = amount.Value, Date = date.Value, Notes = notes.Text ?? ""
        }), row is null ? null : () => _service.DeleteTransfer(Id(row)));
    }

    private void ShowReconciliation()
    {
        Heading("Conciliação", "Histórico de conferências", Reconcile);
        foreach (DataRow row in _service.GetReconciliations().Rows)
        {
            var block = _ui.Card();
            block.AddView(_ui.Text(Str(row, "Conta") + " · " + DateLabel(Str(row, "Data")), 15, bold: true));
            _ui.Gap(block, 12);
            Pair(block, "Saldo no banco", Cash(Value(row, "SaldoInformado")));
            Pair(block, "Saldo no aplicativo", Cash(Value(row, "SaldoAtual")));
            Pair(block, "Diferença", Cash(Value(row, "Diferenca")), Value(row, "Diferenca") == 0 ? _ui.Accent : _ui.Danger);
            if (Str(row, "Observacoes").Length > 0) block.AddView(_ui.Text(Str(row, "Observacoes"), 12, _ui.Muted));
            _content.AddView(block);
        }
    }

    private void Reconcile()
    {
        var form = new EntryForm(_ui, RefreshPage);
        var account = form.Lookup("Conta", _service.GetAccounts());
        var date = form.Date("Data do saldo", DateTime.Today);
        var balance = form.Money("Saldo no banco (R$)");
        var notes = form.Text("Observações", "", true);
        form.Show("Conferir saldo", () => _service.ReconcileAccount(account.Value.Id, date.Value, balance.Value, notes.Text ?? ""));
    }

    private void ShowForecast()
    {
        Heading("Previsão de saldo", "Próximos seis meses");
        foreach (var month in _service.GetForecast())
        {
            var block = _ui.Card();
            block.AddView(_ui.Text(month.Month.ToString("MMMM 'de' yyyy"), 16, bold: true));
            _ui.Gap(block, 12);
            Pair(block, "Saldo inicial", Cash(month.OpeningBalance));
            Pair(block, "Receitas previstas", Cash(month.Income), _ui.Accent);
            Pair(block, "Despesas previstas", Cash(month.Outgoing), _ui.Danger);
            _ui.Divider(block);
            Pair(block, "Saldo previsto", Cash(month.ClosingBalance), month.ClosingBalance >= 0 ? _ui.Accent : _ui.Danger);
            _content.AddView(block);
        }
    }

    private void ShowRules()
    {
        Heading("Regras de categorias", "Por descrição", NewRule);
        foreach (DataRow row in _service.GetCategoryRules().Rows)
        {
            var copy = row;
            MenuRow(Str(row, "Texto"), Str(row, "Tipo") + " · " + Str(row, "Categoria"), "\ue574",
                () => Confirm("Excluir regra?", Str(copy, "Texto"), () => { _service.DeleteCategoryRule(Id(copy)); RefreshPage(); }));
        }
    }

    private void NewRule()
    {
        new AlertDialog.Builder(this).SetTitle("Tipo de movimento").SetItems(new[] { "Receita", "Despesa" }, (_, e) => Run(() =>
        {
            var type = e.Which == 0 ? FinanceTypes.Income : FinanceTypes.Expense;
            var form = new EntryForm(_ui, RefreshPage);
            var text = form.Text("Texto na descrição");
            var category = form.Lookup("Categoria", _service.GetCategories(type));
            form.Show("Nova regra", () => _service.SaveCategoryRule(text.Text ?? "", type, category.Value.Id));
        })).Show();
    }

    private void ChooseStatement()
    {
        var accounts = _service.GetAccounts(false);
        if (accounts.Count == 0) throw new InvalidOperationException("Cadastre uma conta antes de importar.");
        new AlertDialog.Builder(this).SetTitle("Conta do extrato").SetItems(accounts.Select(a => a.Name).ToArray(), (_, e) =>
        {
            _importAccountId = accounts[e.Which].Id;
            OpenDocument(uri =>
            {
                var temporary = Path.Combine(CacheDir!.AbsolutePath, $"extrato-{Guid.NewGuid():N}.csv");
                using (var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível."))
                using (var output = File.Create(temporary)) CopyLimited(input, output, StatementReader.MaxFileBytes);
                _ = RunAsync(async () =>
                {
                    try { _importEntries = await Task.Run(() => _service.PrepareImport(_importAccountId, StatementReader.Read(temporary))); }
                    finally { File.Delete(temporary); }
                }, () => { _importPage = 0; Navigate("import"); });
            });
        }).Show();
    }

    private void ShowImport()
    {
        Heading("Prévia do extrato", $"{_importEntries.Count} movimento(s)");
        MenuRow("Categorias padrão", "Receitas e despesas", "\ue574", ImportCategories);
        var selected = _ui.Text($"{_importEntries.Count(e => e.Selected)} selecionado(s)", 13, _ui.Muted);
        _content.AddView(selected);
        _ui.Gap(_content, 10);
        _content.AddView(_ui.Button("Confirmar importação", () => Confirm("Importar movimentos?", $"{_importEntries.Count(e => e.Selected)} selecionado(s)", () =>
            _ = RunAsync(async () =>
            {
                var count = await Task.Run(() => _service.ImportStatement(_importAccountId, _importEntries, _defaultIncomeCategory, _defaultExpenseCategory));
                _importEntries.Clear();
                RunOnUiThread(() => Notice($"{count} movimento(s) importado(s)"));
            }, () => Navigate("ledger")))));
        _ui.Gap(_content, 18);
        foreach (var entry in _importEntries.Skip(_importPage * 50).Take(50))
        {
            var block = _ui.Card();
            var check = new CheckBox(this) { Text = entry.Description, Checked = entry.Selected, TextSize = 14 };
            check.SetTextColor(_ui.Ink);
            check.CheckedChange += (_, e) => { entry.Selected = e.IsChecked; selected.Text = $"{_importEntries.Count(item => item.Selected)} selecionado(s)"; };
            block.AddView(check);
            Pair(block, DateLabel(entry.Date.ToString("yyyy-MM-dd")), Cash(entry.Amount));
            if (entry.Duplicate) block.AddView(_ui.Text("Possível duplicado", 12, _ui.Danger));
            block.AddView(_ui.Button("Categoria", () =>
            {
                var categories = _service.GetCategories(entry.Type);
                new AlertDialog.Builder(this).SetTitle("Categoria").SetItems(categories.Select(c => c.Name).ToArray(), (_, e) => entry.CategoryId = categories[e.Which].Id).Show();
            }, false));
            _content.AddView(block);
        }
        var pages = _ui.Row();
        if (_importPage > 0) pages.AddView(_ui.IconButton("\ue5cb", "Anterior", () => { _importPage--; RefreshPage(); }));
        pages.AddView(_ui.Text($"Página {_importPage + 1} de {Math.Max(1, (_importEntries.Count + 49) / 50)}", 13, _ui.Muted), MobileUi.Grow());
        if ((_importPage + 1) * 50 < _importEntries.Count) pages.AddView(_ui.IconButton("\ue5cc", "Próxima", () => { _importPage++; RefreshPage(); }));
        _content.AddView(pages);
    }

    private void ImportCategories()
    {
        var form = new EntryForm(_ui, RefreshPage);
        var income = form.Lookup("Receita padrão", _service.GetCategories(FinanceTypes.Income), selectedId: _defaultIncomeCategory);
        var expense = form.Lookup("Despesa padrão", _service.GetCategories(FinanceTypes.Expense), selectedId: _defaultExpenseCategory);
        form.Show("Categorias padrão", () => { _defaultIncomeCategory = income.Value.Id; _defaultExpenseCategory = expense.Value.Id; });
    }

    private void OpenDocument(Action<Android.Net.Uri> callback)
    {
        _documentAction = callback;
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.SetType("*/*"); intent.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(intent, 501);
    }

    private static void CopyLimited(Stream input, Stream output, int limit)
    {
        var buffer = new byte[81920];
        var total = 0;
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidOperationException("Arquivo maior que o limite permitido.");
            output.Write(buffer, 0, count);
        }
    }

    private async Task RunAsync(Func<Task> action, Action? after = null)
    {
        if (_busy) return;
        _busy = true;
        var progress = new ProgressBar(this);
        using var dialog = new AlertDialog.Builder(this).SetTitle("Processando").SetView(progress).SetCancelable(false).Create()!;
        dialog.Show();
        try { await action(); _busy = false; after?.Invoke(); }
        catch (Exception ex) { _busy = false; Run(() => throw new InvalidOperationException(ex.Message, ex)); }
        finally { _busy = false; dialog.Dismiss(); }
    }
}
