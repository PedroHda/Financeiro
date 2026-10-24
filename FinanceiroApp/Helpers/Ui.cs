using System.Data;
using System.Globalization;
using FinanceiroApp.Models;

namespace FinanceiroApp.Helpers;

public static class Ui
{
    public static readonly Color Background = Color.FromArgb(245, 247, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(220, 226, 235);
    public static readonly Color Primary = Color.FromArgb(30, 64, 175);
    public static readonly Color PrimaryDark = Color.FromArgb(23, 37, 84);
    public static readonly Color Success = Color.FromArgb(22, 101, 52);
    public static readonly Color Danger = Color.FromArgb(185, 28, 28);
    public static readonly Color Warning = Color.FromArgb(180, 83, 9);
    public static readonly Color Text = Color.FromArgb(31, 41, 55);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);

    public static Font TitleFont => new("Segoe UI", 17F, FontStyle.Bold);
    public static Font HeadingFont => new("Segoe UI", 11F, FontStyle.Bold);
    public static Font BodyFont => new("Segoe UI", 9.5F, FontStyle.Regular);

    public static Button Button(string text, EventHandler? click = null, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = Math.Max(116, TextRenderer.MeasureText(text, BodyFont).Width + 28),
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Primary : Color.White,
            ForeColor = primary ? Color.White : Text,
            Font = BodyFont,
            Cursor = Cursors.Hand,
            Margin = new Padding(4)
        };
        button.FlatAppearance.BorderColor = primary ? Primary : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(241, 245, 249);
        if (click is not null)
        {
            button.Click += click;
        }

        return button;
    }

    public static TextBox TextBox(string placeholder = "")
    {
        return new TextBox
        {
            PlaceholderText = placeholder,
            Width = 220,
            Font = BodyFont,
            Margin = new Padding(4)
        };
    }

    public static ComboBox ComboBox()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Font = BodyFont,
            Margin = new Padding(4)
        };
    }

    public static DateTimePicker DatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy",
            Width = 130,
            Font = BodyFont,
            Margin = new Padding(4)
        };
    }

    public static NumericUpDown NumberBox(decimal minimum = 0, decimal maximum = 1000000, decimal increment = 1)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            DecimalPlaces = increment < 1 ? 2 : 0,
            Width = 120,
            Font = BodyFont,
            Margin = new Padding(4)
        };
    }

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeight = 36,
            Dock = DockStyle.Fill,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 32 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 242, 247);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = HeadingFont;
        grid.DefaultCellStyle.Font = BodyFont;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
        grid.CellFormatting += (_, e) => FormatGridCell(grid, e);
        return grid;
    }

    public static Panel Card()
    {
        return new Panel
        {
            BackColor = Surface,
            Padding = new Padding(12),
            Margin = new Padding(6)
        };
    }

    public static Label Label(string text, bool heading = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = heading ? HeadingFont : BodyFont,
            ForeColor = heading ? Text : Muted,
            Margin = new Padding(4, 8, 4, 2)
        };
    }

    public static Label ValueLabel(string text, Color? color = null)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = color ?? Text,
            Margin = new Padding(4, 0, 4, 4)
        };
    }

    public static void BindLookup(ComboBox combo, List<LookupItem> items)
    {
        combo.DataSource = items;
        combo.DisplayMember = nameof(LookupItem.Name);
        combo.ValueMember = nameof(LookupItem.Id);
    }

    public static int SelectedId(ComboBox combo)
    {
        if (combo.SelectedValue is int id)
        {
            return id;
        }

        return combo.SelectedItem is LookupItem item ? item.Id : 0;
    }

    public static void SelectLookup(ComboBox combo, int id)
    {
        combo.SelectedValue = id;
    }

    public static void SelectLookupByName(ComboBox combo, string name)
    {
        foreach (var item in combo.Items)
        {
            if (item is LookupItem lookup && lookup.Name == name)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    public static decimal ParseMoney(TextBox textBox, string fieldName)
    {
        return FinancialFormatting.ParseMoney(textBox.Text, fieldName);
    }

    public static string Money(decimal value)
    {
        return value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    }

    public static int SelectedGridId(DataGridView grid)
    {
        if (grid.CurrentRow?.DataBoundItem is DataRowView row && row.Row.Table.Columns.Contains("Id"))
        {
            return Convert.ToInt32(row["Id"]);
        }

        return 0;
    }

    public static string CellText(DataGridView grid, string column)
    {
        if (grid.CurrentRow?.DataBoundItem is DataRowView row && row.Row.Table.Columns.Contains(column))
        {
            return Convert.ToString(row[column]) ?? "";
        }

        return "";
    }

    public static decimal CellMoney(DataGridView grid, string column)
    {
        if (grid.CurrentRow?.DataBoundItem is DataRowView row &&
            row.Row.Table.Columns.Contains(column) && row[column] is not DBNull)
        {
            return Convert.ToDecimal(row[column], CultureInfo.InvariantCulture);
        }

        return 0m;
    }

    public static DateTime CellDate(DataGridView grid, string column)
    {
        var text = CellText(grid, column);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
                ? date
                : DateTime.Today;
    }

    public static void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, "Financeiro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void ShowInfo(string message)
    {
        MessageBox.Show(message, "Financeiro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static TableLayoutPanel FormGrid()
    {
        return new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            Margin = new Padding(0)
        };
    }

    public static void AddField(TableLayoutPanel form, string label, Control control)
    {
        var row = form.RowCount++;
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.Controls.Add(Label(label), 0, row);
        form.Controls.Add(control, 1, row);
    }

    public static FlowLayoutPanel Toolbar()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            WrapContents = true
        };
    }

    public static SplitContainer EditorSplit()
    {
        return new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1100, 650),
            Panel1MinSize = 300,
            Panel2MinSize = 400,
            SplitterDistance = 656,
            FixedPanel = FixedPanel.Panel2
        };
    }

    public static void BindGrid(DataGridView grid, DataTable table)
    {
        grid.DataSource = table;
        if (grid.Columns.Contains("Id") && grid.Columns["Id"] is { } idColumn)
        {
            idColumn.Visible = false;
        }
    }

    public static DataTable? BoundTable(DataGridView grid)
    {
        return grid.DataSource as DataTable;
    }

    private static void FormatGridCell(DataGridView grid, DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is null || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var name = grid.Columns[e.ColumnIndex].Name;
        if (LooksLikeMoneyColumn(name) && e.Value is double or float or decimal or long or int)
        {
            var money = Convert.ToDecimal(e.Value, CultureInfo.InvariantCulture);
            e.Value = Money(money);
            e.FormattingApplied = true;
            e.CellStyle!.ForeColor = money < 0 ? Danger : Text;
        }

        if ((name is "Data" or "Vencimento" or "Inicio" or "Fim" or "DataAlvo" or "PrimeiroVencimento" or "Fechamento" or "PagoEm") &&
            e.Value is string dateText && DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            e.Value = date.ToString("dd/MM/yyyy");
            e.FormattingApplied = true;
        }

        if (name is "Status" or "Alerta")
        {
            var status = Convert.ToString(e.Value) ?? "";
            grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = status switch
            {
                "Vencido" or "Limite ultrapassado" => Danger,
                "Pendente" or "Alerta 90%" or "Alerta 80%" => Warning,
                "Recebido" or "Pago" or "Paga" or "Concluida" => Success,
                _ => Text
            };
        }
    }

    private static bool LooksLikeMoneyColumn(string name)
    {
        return name.Contains("Valor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Saldo", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Limite", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Total", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Gasto", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Receitas", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Despesas", StringComparison.OrdinalIgnoreCase);
    }
}
