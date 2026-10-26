using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed partial class MainForm : Form
{
    private readonly FinanceService _service;
    private readonly SyncClient _syncClient;
    private readonly System.Windows.Forms.Timer _syncTimer = new() { Interval = 120000 };
    private bool _syncing;
    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, BackColor = Ui.Background };
    private readonly FlowLayoutPanel _menu = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(8)
    };

    private readonly Dictionary<string, Func<UserControl>> _views;
    private string? _activeViewKey;
    private bool _hideDashboardValues;

    public MainForm() : this(new FinanceService())
    {
    }

    internal MainForm(FinanceService service)
    {
        _service = service;
        _syncClient = new SyncClient(service);
        Text = "Financeiro Pessoal";
        MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Ui.BodyFont;

        _service.Initialize();

        _views = new Dictionary<string, Func<UserControl>>
        {
            ["Dashboard"] = () => new DashboardView(_service, OpenNewEntry) { ValuesHidden = _hideDashboardValues },
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
            ["Operacoes"] = () => new BankingView(_service),
            ["Seguranca"] = () => new SecurityView(_service),
            ["Sincronizacao"] = () => new SyncView(_service, _syncClient),
            ["Configuracoes"] = () => new SettingsView(_service)
        };

        BuildLayout();
        InitializeSecurity();
        ShowView("Dashboard");
        _syncTimer.Tick += async (_, _) =>
        {
            if (_syncing || _locking || !_syncClient.Connected || !_syncClient.AutoSync) return;
            _syncing = true;
            try { await _syncClient.SynchronizeAsync(); }
            catch (Financeiro.Sync.Contracts.SyncConflictException) { _syncClient.AutoSync = false; }
            catch (Exception) { }
            finally { _syncing = false; }
        };
        _syncTimer.Start();
        FormClosed += (_, _) => { _syncTimer.Dispose(); _syncClient.Dispose(); };
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
        if (key == _activeViewKey || !_views.TryGetValue(key, out var factory))
        {
            return;
        }

        if (_workspace.Controls.OfType<DashboardView>().FirstOrDefault() is { } dashboard)
        {
            _hideDashboardValues = dashboard.ValuesHidden;
        }

        UserControl? view = null;
        var previousViews = _workspace.Controls.Cast<Control>().ToArray();
        _workspace.SuspendLayout();
        try
        {
            view = factory();
            _workspace.Controls.Add(view);
            if (view is IAppView appView)
            {
                appView.RefreshData();
            }

            view.BringToFront();
            foreach (var previousView in previousViews)
            {
                previousView.Dispose();
            }

            _activeViewKey = key;
            foreach (var button in _menu.Controls.OfType<Button>())
            {
                button.BackColor = button.Tag?.ToString() == key ? Ui.Primary : Ui.PrimaryDark;
                button.AccessibleDescription = button.Tag?.ToString() == key ? "Tela atual" : null;
            }
        }
        catch (Exception ex)
        {
            view?.Dispose();
            Ui.ShowError(ex);
        }
        finally
        {
            _workspace.ResumeLayout(true);
        }
    }

    private void OpenNewEntry(string key)
    {
        ShowView(key);
        if (_activeViewKey != key || _workspace.Controls.Count != 1)
        {
            return;
        }

        // Reuse the existing entry dialogs without duplicating their fields or validation.
        var createButton = Descendants(_workspace.Controls[0]).OfType<Button>()
            .FirstOrDefault(button => button.Text == "Novo registro");
        if (createButton is null)
        {
            return;
        }

        BeginInvoke((Action)(() =>
        {
            if (!IsDisposed && _activeViewKey == key && !createButton.IsDisposed)
            {
                createButton.PerformClick();
            }
        }));
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
