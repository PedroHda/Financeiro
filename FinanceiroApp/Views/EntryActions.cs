using System.ComponentModel;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

internal static class EntryActions
{
    internal static void Add(FlowLayoutPanel toolbar, FinanceService service, DataGridView grid, string type, Action refresh)
    {
        grid.MultiSelect = true;
        var more = Ui.Button("Mais acoes");
        var menu = new ContextMenuStrip();
        void Command(string label, Action action) => menu.Items.Add(label, null, (_, _) =>
        {
            try { action(); refresh(); }
            catch (Exception ex) { Ui.ShowError(ex); }
        });
        int Selected() => Ui.SelectedGridId(grid) is > 0 and var id ? id : throw new InvalidOperationException("Selecione um lancamento.");
        Command("Duplicar como pendente", () => service.DuplicateEntry(type, Selected(), DateTime.Today));
        foreach (var status in type == "income" ? new[] { FinanceStatus.Received, FinanceStatus.Pending } : new[] { FinanceStatus.Paid, FinanceStatus.Pending })
        {
            Command("Marcar selecao: " + status, () =>
            {
                var ids = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToArray();
                if (ids.Length == 0) throw new InvalidOperationException("Selecione um ou mais lancamentos.");
                if (MessageBox.Show($"Alterar {ids.Length} lancamento(s) para {status}?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) service.SetEntryStatus(type, ids, status);
            });
        }
        Command("Comprovantes", () => Attachments(service, type, Selected(), grid.FindForm()!));
        if (type == "expense") Command("Dividir entre categorias", () => Splits(service, Selected(), grid.FindForm()!));
        more.Click += (_, _) => menu.Show(more, new Point(0, more.Height));
        more.Disposed += (_, _) => menu.Dispose();
        toolbar.Controls.Add(more);
    }

    private static void Attachments(FinanceService service, string type, int id, IWin32Window owner)
    {
        using var form = new Form { Text = "Comprovantes", Size = new Size(680, 430), MinimumSize = new Size(540, 330), StartPosition = FormStartPosition.CenterParent, Font = Ui.BodyFont };
        var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name", Font = Ui.BodyFont };
        void Refresh() { list.DataSource = service.GetAttachments(type, id).ToList(); }
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Button("Adicionar arquivo", (_, _) =>
        {
            try
            {
                using var open = new OpenFileDialog { Filter = "Comprovantes|*.*" };
                if (open.ShowDialog(form) != DialogResult.OK) return;
                if (new FileInfo(open.FileName).Length > 5 * 1024 * 1024) throw new InvalidOperationException("O comprovante deve ter ate 5 MB.");
                service.AddAttachment(type, id, Path.GetFileName(open.FileName), File.ReadAllBytes(open.FileName)); Refresh();
            }
            catch (Exception ex) { Ui.ShowError(ex); }
        }));
        toolbar.Controls.Add(Ui.Button("Exportar", (_, _) =>
        {
            try
            {
                if (list.SelectedItem is not AttachmentInfo item) return;
                using var save = new SaveFileDialog { FileName = item.Name };
                if (save.ShowDialog(form) == DialogResult.OK) File.WriteAllBytes(save.FileName, service.ReadAttachment(item.Id));
            }
            catch (Exception ex) { Ui.ShowError(ex); }
        }));
        toolbar.Controls.Add(Ui.Button("Excluir", (_, _) =>
        {
            try
            {
                if (list.SelectedItem is AttachmentInfo item && MessageBox.Show("Excluir este comprovante?", "Financeiro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { service.DeleteAttachment(item.Id); Refresh(); }
            }
            catch (Exception ex) { Ui.ShowError(ex); }
        }));
        form.Controls.Add(list); form.Controls.Add(toolbar); Refresh(); form.ShowDialog(owner);
    }

    private sealed class SplitRow
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
    }

    private static void Splits(FinanceService service, int expenseId, IWin32Window owner)
    {
        using var form = new Form { Text = "Rateio da despesa", Size = new Size(650, 440), MinimumSize = new Size(500, 330), StartPosition = FormStartPosition.CenterParent, Font = Ui.BodyFont };
        var rows = new BindingList<SplitRow>(service.GetExpenseSplits(expenseId).Select(s => new SplitRow { CategoryId = s.CategoryId, Amount = s.Amount }).ToList());
        var grid = Ui.Grid(); grid.ReadOnly = false; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = true; grid.AllowUserToDeleteRows = true;
        grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Categoria", DataPropertyName = "CategoryId", DataSource = service.GetCategories(FinanceTypes.Expense), DisplayMember = "Name", ValueMember = "Id" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Valor", DataPropertyName = "Amount", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        grid.DataSource = rows;
        var actions = Ui.Toolbar(); actions.Dock = DockStyle.Bottom;
        actions.Controls.Add(Ui.Button("Salvar rateio", (_, _) =>
        {
            try { grid.EndEdit(); service.SaveExpenseSplits(expenseId, rows.Select(r => new ExpenseSplit(r.CategoryId, r.Amount)).ToArray()); form.DialogResult = DialogResult.OK; }
            catch (Exception ex) { Ui.ShowError(ex); }
        }, true));
        actions.Controls.Add(Ui.Button("Remover rateio", (_, _) =>
        {
            try { service.SaveExpenseSplits(expenseId, []); form.DialogResult = DialogResult.OK; }
            catch (Exception ex) { Ui.ShowError(ex); }
        }));
        form.Controls.Add(grid); form.Controls.Add(actions); form.ShowDialog(owner);
    }
}
