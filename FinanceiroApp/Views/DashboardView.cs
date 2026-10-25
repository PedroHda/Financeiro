using System.ComponentModel;
using System.Data;
using System.Globalization;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class DashboardView : BaseView
{
    private const string HiddenAmount = "R$ ----";
    private readonly Action<string>? _navigateToCreate;
    private readonly Label _greeting;
    private readonly Label _month = new()
    {
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = Ui.BodyFont,
        ForeColor = Ui.Muted
    };
    private readonly CheckBox _privacy = new()
    {
        Name = "HideDashboardValues",
        Text = "Ocultar valores",
        AutoSize = true,
        Anchor = AnchorStyles.Right,
        Font = Ui.BodyFont,
        ForeColor = Ui.Text,
        Cursor = Cursors.Hand,
        AccessibleName = "Ocultar valores do dashboard"
    };
    private DashboardSummary _summary = new(0m, 0m, 0m, 0m, 0m, 0, 0, false);
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

    public DashboardView(FinanceService service, Action<string>? navigateToCreate = null) : base(service, "Dashboard")
    {
        _navigateToCreate = navigateToCreate;
        var header = Controls.OfType<Panel>().Single(panel => panel != Content);
        _greeting = header.Controls.OfType<Label>().Single();
        BuildHeader(header);
        Build();
        foreach (var grid in DashboardGrids())
        {
            grid.CellFormatting += MaskMoneyCell;
        }
        _privacy.CheckedChanged += (_, _) => ApplyPrivacy();
        UpdateGreeting();
    }

    [DefaultValue(false)]
    public bool ValuesHidden
    {
        get => _privacy.Checked;
        set => _privacy.Checked = value;
    }

    public override void RefreshData()
    {
        _summary = Service.GetDashboardSummary();
        UpdateGreeting();
        _alert.BackColor = Ui.Danger;
        _alert.Text = "Alerta: as despesas do mes ultrapassaram as receitas.";

        var categoryData = Service.GetDashboardCategoryExpenses();
        Ui.BindGrid(_categories, categoryData);
        _chart.SetData(categoryData, "Categoria", "Total");
        Ui.BindGrid(_dueSoon, Service.GetDueSoon());
        Ui.BindGrid(_overdue, Service.GetOverdue());
        Ui.BindGrid(_monthly, Service.GetDashboardMonthlyEvolution());
        ApplyPrivacy();
    }

    private void BuildHeader(Panel header)
    {
        header.Height = 86;
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0)
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        _greeting.AutoSize = false;
        _greeting.AutoEllipsis = true;
        _greeting.Dock = DockStyle.Fill;
        _greeting.TextAlign = ContentAlignment.MiddleLeft;
        heading.Controls.Add(_greeting, 0, 0);
        heading.Controls.Add(_month, 0, 1);
        heading.Controls.Add(_privacy, 1, 0);
        heading.SetRowSpan(_privacy, 2);
        header.Controls.Add(heading);
    }

    private void UpdateGreeting()
    {
        var now = DateTime.Now;
        _greeting.Text = now.Hour < 12 ? "Bom dia!" : now.Hour < 18 ? "Boa tarde!" : "Boa noite!";
        _month.Text = now.ToString("MMMM 'de' yyyy", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private void ApplyPrivacy()
    {
        _balance.Text = ValuesHidden ? HiddenAmount : Ui.Money(_summary.CurrentBalance);
        _income.Text = ValuesHidden ? HiddenAmount : Ui.Money(_summary.MonthlyIncome);
        _expense.Text = ValuesHidden ? HiddenAmount : Ui.Money(_summary.MonthlyExpense);
        _monthBalance.Text = ValuesHidden ? HiddenAmount : Ui.Money(_summary.MonthlyBalance);
        _cards.Text = ValuesHidden ? HiddenAmount : Ui.Money(_summary.CreditCardTotal);
        _balance.ForeColor = ValuesHidden ? Ui.Muted : Ui.Text;
        _income.ForeColor = ValuesHidden ? Ui.Muted : Ui.Success;
        _expense.ForeColor = ValuesHidden ? Ui.Muted : Ui.Danger;
        _monthBalance.ForeColor = ValuesHidden ? Ui.Muted : _summary.MonthlyBalance >= 0 ? Ui.Success : Ui.Danger;
        _cards.ForeColor = ValuesHidden ? Ui.Muted : Ui.Warning;
        _alert.Visible = !ValuesHidden && _summary.ExpensesAboveIncome;

        _chart.ValuesHidden = ValuesHidden;
        foreach (var grid in DashboardGrids())
        {
            grid.ClipboardCopyMode = ValuesHidden
                ? DataGridViewClipboardCopyMode.Disable
                : DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;
            grid.ShowCellToolTips = !ValuesHidden;
            grid.Invalidate();
        }
    }

    private IEnumerable<DataGridView> DashboardGrids()
    {
        yield return _dueSoon;
        yield return _overdue;
        yield return _monthly;
        yield return _categories;
    }

    private void MaskMoneyCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (!ValuesHidden || sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        if (grid.Columns[e.ColumnIndex].Name is "Valor" or "Total" or "Receitas" or "Despesas" or "Saldo")
        {
            e.Value = HiddenAmount;
            e.FormattingApplied = true;
            e.CellStyle!.ForeColor = Ui.Muted;
            e.CellStyle.SelectionForeColor = Ui.Muted;
        }
    }

    private void Build()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 5,
            Margin = new Padding(0),
            BackColor = Ui.Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));

        var quickActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        AddQuickAction(quickActions, "Nova receita", "Receitas", true);
        AddQuickAction(quickActions, "Nova despesa", "Despesas");
        AddQuickAction(quickActions, "Nova conta", "Contas");
        AddQuickAction(quickActions, "Nova meta", "Metas");
        layout.Controls.Add(quickActions, 0, 0);
        layout.SetColumnSpan(quickActions, 2);

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0)
        };
        for (var column = 0; column < 5; column++)
        {
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summary.Controls.Add(SummaryCard("Saldo atual", _balance), 0, 0);
        summary.Controls.Add(SummaryCard("Receitas do mes", _income), 1, 0);
        summary.Controls.Add(SummaryCard("Despesas do mes", _expense), 2, 0);
        summary.Controls.Add(SummaryCard("Saldo mensal", _monthBalance), 3, 0);
        summary.Controls.Add(SummaryCard("Total em cartoes", _cards), 4, 0);

        layout.Controls.Add(summary, 0, 1);
        layout.SetColumnSpan(summary, 2);

        var chartPanel = PanelWithTitle("Despesas por categoria", _chart);
        layout.Controls.Add(chartPanel, 0, 2);

        var duePanel = PanelWithTitle("Contas nos proximos 7 dias", _dueSoon);
        layout.Controls.Add(duePanel, 1, 2);

        var categoryPanel = PanelWithTitle("Categorias no mes", _categories);
        layout.Controls.Add(categoryPanel, 0, 3);

        var overduePanel = PanelWithTitle("Contas vencidas", _overdue);
        layout.Controls.Add(overduePanel, 1, 3);

        var monthlyPanel = PanelWithTitle("Evolucao mensal", _monthly);
        layout.Controls.Add(monthlyPanel, 0, 4);
        layout.SetColumnSpan(monthlyPanel, 2);

        Content.AutoScroll = true;
        Content.Controls.Add(layout);
        Content.Controls.Add(_alert);
    }

    private void AddQuickAction(FlowLayoutPanel toolbar, string title, string destination, bool primary = false)
    {
        var button = Ui.Button(title, (_, _) => _navigateToCreate?.Invoke(destination), primary);
        button.Enabled = _navigateToCreate is not null;
        button.AccessibleDescription = $"Abrir cadastro: {title.ToLowerInvariant()}";
        toolbar.Controls.Add(button);
    }

    private static Control SummaryCard(string title, Label value)
    {
        var panel = Ui.Card();
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(4, 0, 4, 8);
        value.AutoSize = false;
        value.AutoEllipsis = true;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.AccessibleName = title;
        var valueFonts = new[]
        {
            value.Font,
            new Font(value.Font.FontFamily, 14F, FontStyle.Bold),
            new Font(value.Font.FontFamily, 12F, FontStyle.Bold),
            new Font(value.Font.FontFamily, 10F, FontStyle.Bold)
        };
        var fittingValue = false;
        void FitValue(object? sender, EventArgs args)
        {
            if (fittingValue)
            {
                return;
            }
            fittingValue = true;
            try
            {
                // Changing a Label font can reset its bounds, even when docked.
                var bounds = value.Bounds;
                value.Font = valueFonts.FirstOrDefault(font => TextRenderer.MeasureText(value.Text, font).Width <= value.ClientSize.Width)
                    ?? valueFonts[^1];
                value.Bounds = bounds;
            }
            finally
            {
                fittingValue = false;
            }
        }
        value.SizeChanged += FitValue;
        value.TextChanged += FitValue;
        value.Disposed += (_, _) =>
        {
            foreach (var font in valueFonts)
            {
                font.Dispose();
            }
        };
        var caption = Ui.Label(title);
        caption.AutoSize = false;
        caption.AutoEllipsis = true;
        caption.Dock = DockStyle.Top;
        caption.Height = 24;
        panel.Controls.Add(value);
        panel.Controls.Add(caption);
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
            AutoEllipsis = true,
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
    private bool _valuesHidden;
    private DataTable? _data;
    private string _labelColumn = "";
    private string _valueColumn = "";

    public DashboardChart()
    {
        DoubleBuffered = true;
        BackColor = Ui.Surface;
        Font = Ui.BodyFont;
        MinimumSize = new Size(260, 160);
    }

    [DefaultValue(false)]
    public bool ValuesHidden
    {
        get => _valuesHidden;
        set
        {
            _valuesHidden = value;
            Invalidate();
        }
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

        if (ValuesHidden)
        {
            DrawCentered(e.Graphics, "Valores ocultos");
            return;
        }

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

        var area = new Rectangle(12, 8, Width - 24, Height - 16);
        var rowHeight = area.Height / rows.Count;
        var barHeight = Math.Max(4, rowHeight - 8);
        var labelWidth = Math.Min(116, area.Width / 3);
        var valueWidth = Math.Min(area.Width / 3, values.Max(value => TextRenderer.MeasureText(Ui.Money(value), Font).Width));
        var barLeft = area.Left + labelWidth + 8;
        var barWidth = Math.Max(1, area.Width - labelWidth - valueWidth - 24);
        using var barBrush = new SolidBrush(Ui.Primary);
        using var linePen = new Pen(Ui.Border);

        for (var i = 0; i < rows.Count; i++)
        {
            var y = area.Top + i * rowHeight;
            var label = Convert.ToString(rows[i][_labelColumn]) ?? "";
            var value = values[i];
            var width = Math.Max(1, (int)(barWidth * (double)(value / max)));

            TextRenderer.DrawText(e.Graphics, label, Font, new Rectangle(area.Left, y, labelWidth, rowHeight), Ui.Text,
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            e.Graphics.DrawLine(linePen, barLeft, y + rowHeight - 2, barLeft + barWidth, y + rowHeight - 2);
            e.Graphics.FillRectangle(barBrush, barLeft, y + (rowHeight - barHeight) / 2, width, barHeight);
            TextRenderer.DrawText(e.Graphics, Ui.Money(value), Font, new Rectangle(area.Right - valueWidth, y, valueWidth, rowHeight), Ui.Muted,
                TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawCentered(Graphics graphics, string text)
    {
        TextRenderer.DrawText(graphics, text, Font, ClientRectangle, Ui.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}
