using System.Data;
using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Financeiro.Mobile.UI;
using FinanceiroApp.Models;
using Path = System.IO.Path;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private string _reportName = "Saldo mensal";

    private void ShowMore()
    {
        Heading("Mais", "Tudo o que faz parte do seu controle.");
        MenuRow("Relatórios", "Resultados, categorias e evolução", "\ue26b", () => Navigate("reports"));
        MenuRow("Categorias", "Receitas e despesas organizadas", "\ue574", () => Navigate("categories"));
        MenuRow("Operações", "Extratos, transferências e conciliação", "\ue8d5", () => Navigate("operations"));
        MenuRow("Sincronização", "Computador e celular", "\ue627", () => Navigate("sync"));
        MenuRow("Preferências e backup", "Aparência, privacidade e seus dados", "\ue8b8", () => Navigate("settings"));
        Section("Financeiro");
        var about = _ui.Card();
        about.AddView(_ui.Text("Seu dinheiro. Seu controle.", 18, bold: true));
        _ui.Gap(about, 8);
        Pair(about, "Versão", AppVersionName + " • Android");
        _ui.Gap(about, 8);
        Pair(about, "Moeda", "Real brasileiro");
        _content.AddView(about);
    }

    private void ShowCategories()
    {
        Heading("Categorias", "Cada movimento no seu lugar.", () => EditCategory());
        var table = _service.SearchCategories("");
        foreach (var type in new[] { "Receita", "Despesa" })
        {
            Section(type == "Receita" ? "Receitas" : "Despesas");
            foreach (DataRow row in table.Rows.Cast<DataRow>().Where(row => Str(row, "Tipo") == type))
            {
                var copy = row;
                MenuRow(Str(row, "Nome"), Str(row, "Status"), "\ue574", () => EditCategory(copy));
            }
        }
        if (table.Rows.Count == 0) Empty("Nenhuma categoria", "Seu controle pode ter as suas próprias categorias.", "Criar categoria", () => EditCategory());
    }

    private void ShowReports()
    {
        Heading("Relatórios", "Uma visão completa dos seus resultados.");
        MonthPicker();
        var filter = MonthFilter();
        var names = new[] { "Saldo mensal", "Receitas por periodo", "Despesas por periodo", "Despesas por categoria", "Contas pagas", "Contas pendentes", "Contas vencidas", "Gastos por cartao", "Evolucao mensal", "Receitas x despesas" };
        var report = Choice(names, _reportName);
        _content.AddView(report, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, _ui.Dp(48)));
        _ui.Gap(_content, 12);
        var dates = _ui.Row();
        var from = _ui.Button("De " + filter.StartDate.ToString("dd/MM/yyyy"), () => { }, false);
        var to = _ui.Button("Até " + filter.EndDate.ToString("dd/MM/yyyy"), () => { }, false);
        dates.AddView(from, new LinearLayout.LayoutParams(0, _ui.Dp(46), 1) { RightMargin = _ui.Dp(5) });
        dates.AddView(to, new LinearLayout.LayoutParams(0, _ui.Dp(46), 1) { LeftMargin = _ui.Dp(5) });
        _content.AddView(dates);
        _ui.Gap(_content, 10);
        var categories = _service.GetCategories("");
        categories[0] = new LookupItem(0, "Todas as categorias");
        var accounts = _service.GetAccounts();
        accounts[0] = new LookupItem(0, "Todas as contas");
        var cards = _service.GetCreditCards();
        cards[0] = new LookupItem(0, "Todos os cartões");
        var category = Choice(categories.Select(item => item.Name).ToArray(), categories[0].Name);
        var account = Choice(accounts.Select(item => item.Name).ToArray(), accounts[0].Name);
        var card = Choice(cards.Select(item => item.Name).ToArray(), cards[0].Name);
        var status = Choice(new[] { "Todos os status", "Recebido", "Pago", "Pendente", "Vencido", "Aberta", "Fechada", "Paga" }, "Todos os status");
        var first = _ui.Row();
        first.AddView(category, MobileUi.Grow());
        first.AddView(account, MobileUi.Grow());
        _content.AddView(first);
        _ui.Gap(_content, 8);
        var second = _ui.Row();
        second.AddView(card, MobileUi.Grow());
        second.AddView(status, MobileUi.Grow());
        _content.AddView(second);
        _ui.Gap(_content, 12);
        _content.AddView(_ui.Button("Exportar CSV", () => Run(ExportReport), false));
        _ui.Gap(_content, 18);
        var result = _ui.Column();
        _content.AddView(result);

        void Fill()
        {
            Run(() =>
            {
                _reportName = report.SelectedItem?.ToString() ?? "Saldo mensal";
                filter.CategoryId = categories[Math.Max(0, category.SelectedItemPosition)].Id;
                filter.AccountId = accounts[Math.Max(0, account.SelectedItemPosition)].Id;
                filter.CreditCardId = cards[Math.Max(0, card.SelectedItemPosition)].Id;
                filter.Status = status.SelectedItemPosition <= 0 ? "" : status.SelectedItem?.ToString();
                var table = _service.GetReport(_reportName, filter);
                _lastExport = table;
                result.RemoveAllViews();
                result.AddView(_ui.Text($"{table.Rows.Count} registro(s)", 12, _ui.Muted));
                _ui.Gap(result, 10);
                if (table.Rows.Count == 0) result.AddView(_ui.Text("Nenhum resultado para estes filtros", 14, _ui.Muted));
                foreach (DataRow row in table.Rows.Cast<DataRow>().Take(200))
                {
                    var block = _ui.Card();
                    foreach (DataColumn column in table.Columns)
                    {
                        if (column.ColumnName == "Id") continue;
                        var value = Str(row, column.ColumnName);
                        if (IsMoneyColumn(column.ColumnName)) value = Cash(Value(row, column.ColumnName));
                        else value = DateLabel(value);
                        Pair(block, column.ColumnName, value);
                        _ui.Gap(block, 8);
                    }
                    result.AddView(block);
                }
                if (table.Rows.Count > 200) result.AddView(_ui.Text("Exibindo 200 registros. O CSV contém todos os resultados.", 12, _ui.Muted));
            });
        }

        void PickDate(bool initial)
        {
            var selected = initial ? filter.StartDate : filter.EndDate;
            new DatePickerDialog(this, (_, e) =>
            {
                if (initial) { filter.StartDate = e.Date; from.Text = "De " + e.Date.ToString("dd/MM/yyyy"); }
                else { filter.EndDate = e.Date; to.Text = "Até " + e.Date.ToString("dd/MM/yyyy"); }
                Fill();
            }, selected.Year, selected.Month - 1, selected.Day).Show();
        }
        from.Click += (_, _) => PickDate(true);
        to.Click += (_, _) => PickDate(false);
        foreach (var spinner in new[] { report, category, account, card, status }) spinner.ItemSelected += (_, _) => Fill();
        Fill();
    }

    private static bool IsMoneyColumn(string name) => name.Contains("Valor", StringComparison.OrdinalIgnoreCase) ||
        name is "Total" or "Saldo" or "Receitas" or "Despesas" or "Limite" or "Gasto";

    private void ShowSettings()
    {
        Heading("Preferências", "Do seu jeito, em cada detalhe.");
        var appearance = _ui.Card();
        AddSwitch(appearance, "Tema escuro", _dark, value =>
        {
            _dark = value;
            _service.SetSetting("MobileTheme", value ? "Escuro" : "Claro");
            RefreshPage();
        });
        _ui.Divider(appearance);
        AddSwitch(appearance, "Ocultar valores", _hidden, value =>
        {
            _hidden = value;
            _service.SetSetting("MobileHideAmounts", value ? "true" : "false");
            RefreshPage();
        });
        _content.AddView(appearance);
        Section("Seus dados");
        MenuRow("Segurança", "PIN e biometria", "\ue897", () => Navigate("security"));
        MenuRow("Backup protegido", "Exportar com senha", "\ue2c4", ExportEncryptedBackup);
        MenuRow("Restaurar backup protegido", "Importar com senha", "\ue2c6", ImportEncryptedBackup);
        MenuRow("Exportar backup", "Salvar uma cópia do banco", "\ue2c4", ExportBackup);
        MenuRow("Restaurar backup", "Importar um banco do Android ou Windows", "\ue2c6", ImportBackup);
        if (File.Exists(_service.DatabasePath + ".before-restore"))
        {
            MenuRow("Backup anterior", "Exportar o estado antes da última restauração", "\ue2c4", () =>
                SaveDocument("financeiro-recuperacao.db", "application/octet-stream", uri =>
                {
                    using var input = File.OpenRead(_service.DatabasePath + ".before-restore");
                    using var output = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Não foi possível abrir o destino.");
                    input.CopyTo(output);
                    Notice("Backup anterior exportado");
                }));
        }
        MenuRow("Dados de demonstração", "Adicionar exemplos fictícios", "\ue2c7", () => Confirm("Adicionar exemplos?", "Serão incluídos registros fictícios no banco atual.", () => { _service.SeedDemoData(); RefreshPage(); Notice("Exemplos adicionados"); }));
        MenuRow("Apagar dados financeiros", "Excluir contas e todos os lançamentos", "\ue872", () => Confirm("Apagar todos os dados?", "Contas, receitas, despesas, cartões, metas e orçamentos serão removidos. Categorias e preferências serão mantidas. Faça um backup antes.", () => { _service.ClearFinancialData(); RefreshPage(); Notice("Dados financeiros removidos"); }));
        Section("Sobre");
        var details = _ui.Card();
        Pair(details, "Versão", AppVersionName);
        _ui.Gap(details, 10);
        Pair(details, "Moeda", "BRL • R$");
        _ui.Gap(details, 10);
        Pair(details, "Data", "DD/MM/AAAA");
        _content.AddView(details);
    }

    private void AddSwitch(LinearLayout parent, string label, bool current, Action<bool> changed)
    {
        var row = _ui.Row();
        row.AddView(_ui.Text(label, 14), MobileUi.Grow());
        var toggle = new Switch(this) { Checked = current, ContentDescription = label };
        toggle.CheckedChange += (_, e) => changed(e.IsChecked);
        row.AddView(toggle);
        parent.AddView(row);
    }

    private void ExportBackup()
    {
        var temporary = Path.Combine(CacheDir!.AbsolutePath, $"backup-{Guid.NewGuid():N}.db");
        _service.BackupDatabase(temporary);
        SaveDocument($"financeiro-{DateTime.Today:yyyy-MM-dd}.db", "application/octet-stream", uri =>
        {
            try
            {
                using var input = File.OpenRead(temporary);
                using var output = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Não foi possível abrir o destino.");
                input.CopyTo(output);
                Notice("Backup exportado");
            }
            finally { File.Delete(temporary); }
        });
    }

    private void ImportBackup()
    {
        _documentAction = uri =>
        {
            var temporary = Path.Combine(CacheDir!.AbsolutePath, $"import-{Guid.NewGuid():N}.db");
            using (var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Não foi possível ler o arquivo."))
            using (var output = File.Create(temporary)) input.CopyTo(output);
            Confirm("Restaurar este backup?", "Os dados financeiros deste aparelho serão substituídos. Uma cópia anterior ficará disponível para recuperação.", () =>
            {
                try { _service.RestoreDatabase(temporary); RefreshPage(); Notice("Backup restaurado"); }
                finally { File.Delete(temporary); }
            });
        };
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.SetType("*/*");
        intent.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(intent, 501);
    }

    private void ExportReport()
    {
        if (_lastExport is null || _lastExport.Rows.Count == 0) throw new InvalidOperationException("Não há dados para exportar.");
        var temporary = Path.Combine(CacheDir!.AbsolutePath, $"report-{Guid.NewGuid():N}.csv");
        _service.ExportTableToCsv(_lastExport, temporary);
        SaveDocument($"financeiro-relatorio-{DateTime.Today:yyyy-MM-dd}.csv", "text/csv", uri =>
        {
            try
            {
                using var input = File.OpenRead(temporary);
                using var output = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Não foi possível abrir o destino.");
                input.CopyTo(output);
                Notice("CSV exportado");
            }
            finally { File.Delete(temporary); }
        });
    }

    private void SaveDocument(string name, string mime, Action<Android.Net.Uri> write)
    {
        _documentAction = write;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.SetType(mime);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.PutExtra(Intent.ExtraTitle, name);
        StartActivityForResult(intent, 501);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != 501) return;
        var callback = _documentAction;
        _documentAction = null;
        if (resultCode == Result.Ok && data?.Data is { } uri && callback is not null)
        {
            if (_appLock.Enabled && (!_authenticated || _backgroundAt != default && DateTime.UtcNow - _backgroundAt >= TimeSpan.FromMinutes(2)))
            { _afterUnlock = () => Run(() => callback(uri)); LockMobile(); }
            else Run(() => callback(uri));
        }
    }

    private void Notice(string text) => Toast.MakeText(this, text, ToastLength.Short)?.Show();
    private string AppVersionName => PackageManager!.GetPackageInfo(PackageName!, (Android.Content.PM.PackageInfoFlags)0)?.VersionName ?? "1.1.0";
}
