using System.Data;
using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class DashboardView : BaseView
{
    private readonly Label _balance = Ui.ValueLabel("R$ 0,00");
    private readonly Label _income = Ui.ValueLabel("R$ 0,00", Ui.Success);
    private readonly Label _expense = Ui.ValueLabel("R$ 0,00", Ui.Danger);
    private readonly Label _monthBalance = Ui.ValueLabel("R$ 0,00");
    private readonly Label _cards = Ui.ValueLabel("R$ 0,00", Ui.Warning);
    private readonly Label _alert = new()
    {
        Dock = DockStyle.Top,
        Height = 34,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = Ui.HeadingFont,
        ForeColor = Color.White,
        Padding = new Padding(12, 0, 0, 0),
        Visible = false
    };

    private readonly DataGridView _dueSoon = Ui.Grid();
    private readonly DataGridView _overdue = Ui.Grid();
    private readonly DataGridView _monthly = Ui.Grid();
    private readonly DataGridView _categories = Ui.Grid();
    private readonly DashboardChart _chart = new() { Dock = DockStyle.Fill };

    public DashboardView(FinanceService service) : base(service, "Dashboard")
    {
        Build();
    }

    public override void RefreshData()
    {
        var summary = Service.GetDashboardSummary();
        _balance.Text = Ui.Money(summary.CurrentBalance);
        _income.Text = Ui.Money(summary.MonthlyIncome);
        _expense.Text = Ui.Money(summary.MonthlyExpense);
        _monthBalance.Text = Ui.Money(summary.MonthlyBalance);
        _monthBalance.ForeColor = summary.MonthlyBalance >= 0 ? Ui.Success : Ui.Danger;
        _cards.Text = Ui.Money(summary.CreditCardTotal);
        _alert.Visible = summary.ExpensesAboveIncome;
        _alert.BackColor = Ui.Danger;
        _alert.Text = "Alerta: as despesas do mes ultrapassaram as receitas.";

        var categoryData = Service.GetDashboardCategoryExpenses();
        Ui.BindGrid(_categories, categoryData);
        _chart.SetData(categoryData, "Categoria", "Total");
        Ui.BindGrid(_dueSoon, Service.GetDueSoon());
        Ui.BindGrid(_overdue, Service.GetOverdue());
        Ui.BindGrid(_monthly, Service.GetDashboardMonthlyEvolution());
    }

    private void Build()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = Ui.Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var summary = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        summary.Controls.Add(SummaryCard("Saldo atual", _balance));
        summary.Controls.Add(SummaryCard("Receitas do mes", _income));
        summary.Controls.Add(SummaryCard("Despesas do mes", _expense));
        summary.Controls.Add(SummaryCard("Saldo mensal", _monthBalance));
        summary.Controls.Add(SummaryCard("Total em cartoes", _cards));

        layout.Controls.Add(summary, 0, 0);
        layout.SetColumnSpan(summary, 2);

        var chartPanel = PanelWithTitle("Despesas por categoria", _chart);
        layout.Controls.Add(chartPanel, 0, 1);

        var duePanel = PanelWithTitle("Proximas contas", _dueSoon);
        layout.Controls.Add(duePanel, 1, 1);

        var categoryPanel = PanelWithTitle("Categorias no mes", _categories);
        layout.Controls.Add(categoryPanel, 0, 2);

        var overduePanel = PanelWithTitle("Contas vencidas", _overdue);
        layout.Controls.Add(overduePanel, 1, 2);

        var monthlyPanel = PanelWithTitle("Evolucao mensal", _monthly);
        layout.Controls.Add(monthlyPanel, 0, 3);
        layout.SetColumnSpan(monthlyPanel, 2);

        Content.Controls.Add(layout);
        Content.Controls.Add(_alert);
    }

    private static Control SummaryCard(string title, Label value)
    {
        var panel = Ui.Card();
        panel.Width = 205;
        panel.Height = 88;
        panel.Controls.Add(value);
        panel.Controls.Add(Ui.Label(title));
        value.Location = new Point(8, 34);
        return panel;
    }

    private static Control PanelWithTitle(string title, Control child)
    {
        var panel = SurfacePanel();
        panel.Margin = new Padding(6);
        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 32,
            Font = Ui.HeadingFont,
            ForeColor = Ui.Text
        };
        panel.Controls.Add(child);
        panel.Controls.Add(label);
        return panel;
    }
}

public sealed class DashboardChart : Control
{
    private DataTable? _data;
    private string _labelColumn = "";
    private string _valueColumn = "";

    public DashboardChart()
    {
        DoubleBuffered = true;
        BackColor = Ui.Surface;
        MinimumSize = new Size(260, 160);
    }

    public void SetData(DataTable data, string labelColumn, string valueColumn)
    {
        _data = data;
        _labelColumn = labelColumn;
        _valueColumn = valueColumn;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Ui.Surface);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (_data is null || _data.Rows.Count == 0)
        {
            DrawCentered(e.Graphics, "Sem dados para o mes");
            return;
        }

        var rows = _data.Rows.Cast<DataRow>().Take(8).ToList();
        var values = rows.Select(row => Convert.ToDecimal(row[_valueColumn])).ToList();
        var max = values.Max();
        if (max <= 0)
        {
            DrawCentered(e.Graphics, "Sem despesas");
            return;
        }

        var area = new Rectangle(16, 18, Width - 32, Height - 44);
        var barHeight = Math.Max(16, area.Height / Math.Max(1, rows.Count) - 8);
        using var barBrush = new SolidBrush(Ui.Primary);
        using var textBrush = new SolidBrush(Ui.Text);
        using var mutedBrush = new SolidBrush(Ui.Muted);
        using var linePen = new Pen(Ui.Border);

        for (var i = 0; i < rows.Count; i++)
        {
            var y = area.Top + i * (barHeight + 8);
            var label = Convert.ToString(rows[i][_labelColumn]) ?? "";
            var value = values[i];
            var width = (int)Math.Max(8, (area.Width - 150) * (double)(value / max));

            e.Graphics.DrawString(label, Ui.BodyFont, textBrush, area.Left, y);
            e.Graphics.DrawLine(linePen, area.Left + 116, y + barHeight + 2, area.Right, y + barHeight + 2);
            e.Graphics.FillRectangle(barBrush, area.Left + 120, y + 2, width, barHeight);
            e.Graphics.DrawString(Ui.Money(value), Ui.BodyFont, mutedBrush, area.Right - 88, y);
        }
    }

    private void DrawCentered(Graphics graphics, string text)
    {
        using var brush = new SolidBrush(Ui.Muted);
        var size = graphics.MeasureString(text, Ui.BodyFont);
        graphics.DrawString(text, Ui.BodyFont, brush, (Width - size.Width) / 2, (Height - size.Height) / 2);
    }
}
