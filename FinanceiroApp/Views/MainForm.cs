using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class MainForm : Form
{
    private readonly FinanceService _service = new();
    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Ui.Background };
    private readonly FlowLayoutPanel _menu = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(8)
    };

    private readonly Dictionary<string, Func<UserControl>> _views;
    private Button? _activeButton;

    public MainForm()
    {
        Text = "Financeiro Pessoal";
        MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Ui.BodyFont;

        _service.Initialize();

        _views = new Dictionary<string, Func<UserControl>>
        {
            ["Dashboard"] = () => new DashboardView(_service),
            ["Contas"] = () => new AccountsView(_service),
            ["Categorias"] = () => new CategoriesView(_service),
            ["Receitas"] = () => new IncomesView(_service),
            ["Despesas"] = () => new ExpensesView(_service),
            ["Recorrentes"] = () => new RecurringExpensesView(_service),
            ["Parcelamentos"] = () => new InstallmentsView(_service),
            ["Cartoes"] = () => new CreditCardsView(_service),
            ["Orcamentos"] = () => new BudgetsView(_service),
            ["Metas"] = () => new GoalsView(_service),
            ["Relatorios"] = () => new ReportsView(_service),
            ["Configuracoes"] = () => new SettingsView(_service)
        };

        BuildLayout();
        ShowView("Dashboard");
    }

    private void BuildLayout()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 205,
            BackColor = Ui.PrimaryDark
        };

        var title = new Label
        {
            Text = "Financeiro",
            Dock = DockStyle.Top,
            Height = 62,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White
        };

        sidebar.Controls.Add(_menu);
        sidebar.Controls.Add(title);

        foreach (var key in _views.Keys)
        {
            var button = new Button
            {
                Text = key,
                Tag = key,
                Width = 180,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                ForeColor = Color.White,
                BackColor = Ui.PrimaryDark,
                Font = Ui.BodyFont,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 3, 0, 3)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 64, 175);
            button.Click += (_, _) => ShowView(Convert.ToString(button.Tag)!);
            _menu.Controls.Add(button);
        }

        Controls.Add(_workspace);
        Controls.Add(sidebar);
    }

    private void ShowView(string key)
    {
        if (!_views.TryGetValue(key, out var factory))
        {
            return;
        }

        foreach (Button button in _menu.Controls.OfType<Button>())
        {
            button.BackColor = button.Tag?.ToString() == key ? Ui.Primary : Ui.PrimaryDark;
            if (button.Tag?.ToString() == key)
            {
                _activeButton = button;
            }
        }

        _workspace.SuspendLayout();
        _workspace.Controls.Clear();
        var view = factory();
        _workspace.Controls.Add(view);
        if (view is IAppView appView)
        {
            appView.RefreshData();
        }
        _workspace.ResumeLayout();
    }
}
