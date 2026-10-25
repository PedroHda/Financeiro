using System.Data;
using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Financeiro.Mobile.UI;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;
using Path = System.IO.Path;

namespace Financeiro.Mobile;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, Exported = true,
    Name = "com.pedrohda.financeiro.MainActivity", ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
public partial class MainActivity : Activity
{
    private FinanceService _service = null!;
    private MobileUi _ui = null!;
    private LinearLayout _content = null!;
    private LinearLayout _root = null!;
    private string _screen = "home";
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool _hidden;
    private bool _dark;
    private string _ledgerType = "Todos";
    private string _ledgerStatus = "Todos";
    private string _search = "";
    private DataTable? _lastExport;
    private Action<Android.Net.Uri>? _documentAction;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CultureInfo.DefaultThreadCurrentCulture = FinancialFormatting.BrazilianCulture;
        CultureInfo.DefaultThreadCurrentUICulture = FinancialFormatting.BrazilianCulture;
        try
        {
            _service = new FinanceService(Path.Combine(FilesDir!.AbsolutePath, "financeiro.db"));
            _service.Initialize();
            _dark = _service.GetSetting("MobileTheme") == "Escuro";
            _hidden = _service.GetSetting("MobileHideAmounts") == "true";
            _screen = savedInstanceState?.GetString("screen") ?? "home";
            if (DateTime.TryParseExact(savedInstanceState?.GetString("month"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)) _month = month;
            BuildShell();
        }
        catch (Exception ex)
        {
            new AlertDialog.Builder(this).SetTitle("Não foi possível abrir").SetMessage(ex.Message).SetPositiveButton("Fechar", (_, _) => Finish()).Show();
        }
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutString("screen", _screen);
        outState.PutString("month", _month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        base.OnSaveInstanceState(outState);
    }

    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        BuildShell();
    }

    private void BuildShell()
    {
        _ui = new MobileUi(this, _dark);
        _root = _ui.Column();
        _root.SetBackgroundColor(_ui.BackgroundColor);
#pragma warning disable CS0618
        var systemUi = _dark ? (SystemUiFlags)0 : SystemUiFlags.LightStatusBar;
        if (!_dark && OperatingSystem.IsAndroidVersionAtLeast(26)) systemUi |= SystemUiFlags.LightNavigationBar;
        Window!.DecorView.SystemUiVisibility = (StatusBarVisibility)systemUi;
#pragma warning restore CS0618
        _root.SetOnApplyWindowInsetsListener(new InsetsListener((top, bottom) => _root.SetPadding(0, top, 0, bottom)));
        var brand = _ui.Row();
        brand.SetPadding(_ui.Dp(20), _ui.Dp(8), _ui.Dp(20), _ui.Dp(8));
        var mark = _ui.Icon("\ue850", _ui.Accent, 26);
        brand.AddView(mark, new LinearLayout.LayoutParams(_ui.Dp(34), _ui.Dp(40)));
        brand.AddView(_ui.Text("Financeiro", 20, bold: true), MobileUi.Grow());
        brand.AddView(_ui.IconButton(_hidden ? "\ue8f5" : "\ue8f4", _hidden ? "Mostrar valores" : "Ocultar valores", () =>
        {
            _hidden = !_hidden;
            _service.SetSetting("MobileHideAmounts", _hidden ? "true" : "false");
            BuildShell();
        }));
        _root.AddView(brand);

        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.SetClipToPadding(false);
        _content = _ui.Column(20);
        scroll.AddView(_content);
        _root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        var navigation = _ui.Row();
        navigation.SetBackgroundColor(_ui.Surface);
        var currentTab = TabFor(_screen);
        foreach (var (key, label, glyph) in new[]
        {
            ("home", "Início", "\ue88a"), ("ledger", "Movimentos", "\ue8d5"), ("planning", "Planejar", "\ue6c4"),
            ("wallet", "Carteira", "\ue850"), ("more", "Mais", "\ue5d3")
        })
        {
            var item = _ui.Column();
            item.SetGravity(GravityFlags.Center);
            item.SetPadding(0, _ui.Dp(8), 0, _ui.Dp(8));
            item.ContentDescription = label;
            item.Focusable = true;
            var selected = currentTab == key;
            var icon = _ui.Icon(glyph, selected ? _ui.Accent : _ui.Muted);
            icon.Background = _ui.Background(selected ? _ui.Soft : _ui.Surface, 8);
            item.AddView(icon, new LinearLayout.LayoutParams(_ui.Dp(48), _ui.Dp(30)));
            var caption = _ui.Text(label, 10, selected ? _ui.Accent : _ui.Muted, selected);
            caption.Gravity = GravityFlags.Center;
            item.AddView(caption, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, _ui.Dp(22)));
            item.Click += (_, _) => Navigate(key);
            navigation.AddView(item, new LinearLayout.LayoutParams(0, _ui.Dp(68), 1));
        }
        _root.AddView(navigation);
        SetContentView(_root);
        _root.RequestApplyInsets();
        Run(RenderPage);
    }

    private static string TabFor(string screen) => screen switch
    {
        "accounts" or "cards" or "purchases" or "invoices" => "wallet",
        "budgets" or "goals" or "recurring" or "installments" => "planning",
        "categories" or "reports" or "settings" => "more",
        "overdue" => "home",
        _ => screen
    };

    private void Navigate(string screen)
    {
        _screen = screen;
        BuildShell();
    }

    private void RefreshPage() => BuildShell();

    private void RenderPage()
    {
        switch (_screen)
        {
            case "home": ShowHome(); break;
            case "overdue": ShowOverdue(); break;
            case "ledger": ShowLedger(); break;
            case "planning": ShowPlanning(); break;
            case "budgets": ShowBudgets(); break;
            case "goals": ShowGoals(); break;
            case "recurring": ShowRecurring(); break;
            case "installments": ShowInstallments(); break;
            case "wallet": ShowWallet(); break;
            case "accounts": ShowAccounts(); break;
            case "cards": ShowCards(); break;
            case "purchases": ShowPurchases(); break;
            case "invoices": ShowInvoices(); break;
            case "categories": ShowCategories(); break;
            case "reports": ShowReports(); break;
            case "settings": ShowSettings(); break;
            default: ShowMore(); break;
        }
    }

    private void Heading(string title, string subtitle, Action? add = null)
    {
        var row = _ui.Row();
        row.AddView(_ui.Text(title, 25, bold: true), MobileUi.Grow());
        if (add is not null) row.AddView(_ui.IconButton("\ue145", "Adicionar", () => Run(add)));
        _content.AddView(row);
        _ui.Gap(_content, 6);
        _content.AddView(_ui.Text(subtitle, 13, _ui.Muted));
        _ui.Gap(_content, 20);
    }

    private void MonthPicker()
    {
        var row = _ui.Row();
        row.AddView(_ui.IconButton("\ue5cb", "Mês anterior", () => { _month = _month.AddMonths(-1); RefreshPage(); }));
        var label = _ui.Text(_month.ToString("MMMM 'de' yyyy"), 14, bold: true);
        label.Gravity = GravityFlags.Center;
        row.AddView(label, MobileUi.Grow());
        row.AddView(_ui.IconButton("\ue5cc", "Próximo mês", () => { _month = _month.AddMonths(1); RefreshPage(); }));
        _content.AddView(row);
        _ui.Gap(_content, 16);
    }

    private string Cash(decimal value) => _hidden ? "••••" : value.ToString("C2", FinancialFormatting.BrazilianCulture);
    private static decimal Value(DataRow row, string column) => row.IsNull(column) ? 0 : Convert.ToDecimal(row[column], CultureInfo.InvariantCulture);
    private static int Id(DataRow row) => Convert.ToInt32(row["Id"]);
    private static string Str(DataRow row, string column) => row.Table.Columns.Contains(column) && !row.IsNull(column) ? Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? "" : "";
    private static string DateLabel(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("dd/MM/yyyy") : value;
    private ReportFilter MonthFilter() => new() { StartDate = _month, EndDate = _month.AddMonths(1).AddDays(-1) };

    private void Section(string title)
    {
        _ui.Gap(_content, 14);
        _content.AddView(_ui.Text(title, 17, bold: true));
        _ui.Gap(_content, 12);
    }

    private void Empty(string title, string subtitle, string? action = null, Action? click = null)
    {
        var card = _ui.Card(20);
        card.AddView(_ui.Text(title, 16, bold: true));
        _ui.Gap(card, 8);
        card.AddView(_ui.Text(subtitle, 13, _ui.Muted));
        if (action is not null && click is not null)
        {
            _ui.Gap(card, 16);
            card.AddView(_ui.Button(action, () => Run(click)));
        }
        _content.AddView(card);
    }

    private void MenuRow(string title, string detail, string glyph, Action action)
    {
        var row = _ui.Row();
        row.SetPadding(_ui.Dp(14), _ui.Dp(14), _ui.Dp(10), _ui.Dp(14));
        row.Background = _ui.Background(_ui.Surface);
        row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { BottomMargin = _ui.Dp(8) };
        row.AddView(_ui.Icon(glyph, _ui.Accent), new LinearLayout.LayoutParams(_ui.Dp(40), _ui.Dp(40)));
        var text = _ui.Column();
        text.SetPadding(_ui.Dp(8), 0, _ui.Dp(4), 0);
        text.AddView(_ui.Text(title, 15, bold: true));
        _ui.Gap(text, 4);
        text.AddView(_ui.Text(detail, 12, _ui.Muted));
        row.AddView(text, MobileUi.Grow());
        row.AddView(_ui.Icon("\ue5cc", _ui.Muted, 22), new LinearLayout.LayoutParams(_ui.Dp(24), _ui.Dp(36)));
        row.Focusable = true;
        row.ContentDescription = title;
        row.Click += (_, _) => Run(action);
        _content.AddView(row);
    }

    private void Pair(LinearLayout parent, string label, string value, Color? color = null)
    {
        var row = _ui.Row();
        row.AddView(_ui.Text(label, 13, _ui.Muted), MobileUi.Grow());
        row.AddView(_ui.Text(value, 14, color ?? _ui.Ink, true));
        parent.AddView(row);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Android.Util.Log.Error("Financeiro", ex.ToString());
            new AlertDialog.Builder(this).SetTitle("Confira os dados").SetMessage(ex.Message).SetPositiveButton("OK", (_, _) => { }).Show();
        }
    }

    private void Confirm(string title, string message, Action action) =>
        new AlertDialog.Builder(this).SetTitle(title).SetMessage(message)
            .SetNegativeButton("Cancelar", (_, _) => { }).SetPositiveButton("Confirmar", (_, _) => Run(action)).Show();

#pragma warning disable CS0672, CS0618
    public override void OnBackPressed()
    {
        var tab = TabFor(_screen);
        if (_screen != tab) Navigate(tab);
        else if (_screen != "home") Navigate("home");
        else base.OnBackPressed();
    }
#pragma warning restore CS0672, CS0618
}
