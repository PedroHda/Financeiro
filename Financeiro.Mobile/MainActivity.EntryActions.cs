using Android.Provider;
using Financeiro.Mobile.Forms;
using FinanceiroApp.Models;
using Path = System.IO.Path;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private void EntryMenu(LedgerItem item)
    {
        if (item.Kind is not ("Receita" or "Despesa")) return;
        var type = item.Kind == "Receita" ? "income" : "expense";
        var labels = new List<string> { "Duplicar como pendente", item.Kind == "Receita" ? "Marcar como recebida" : "Marcar como paga", "Marcar como pendente", "Comprovantes" };
        if (type == "expense") labels.Add("Dividir entre categorias");
        new AlertDialog.Builder(this).SetTitle(item.Description).SetItems(labels.ToArray(), (_, e) => Run(() =>
        {
            switch (e.Which)
            {
                case 0: Confirm("Duplicar lançamento?", "Novo movimento pendente com a data de hoje.", () => { _service.DuplicateEntry(type, Id(item.Row), DateTime.Today); RefreshPage(); }); break;
                case 1: _service.SetEntryStatus(type, [Id(item.Row)], type == "income" ? FinanceStatus.Received : FinanceStatus.Paid); RefreshPage(); break;
                case 2: _service.SetEntryStatus(type, [Id(item.Row)], FinanceStatus.Pending); RefreshPage(); break;
                case 3: AttachmentMenu(type, Id(item.Row)); break;
                case 4: SplitMenu(Id(item.Row)); break;
            }
        })).Show();
    }

    private void AttachmentMenu(string type, int id)
    {
        var attachments = _service.GetAttachments(type, id);
        var labels = new[] { "Adicionar comprovante" }.Concat(attachments.Select(a => a.Name)).ToArray();
        new AlertDialog.Builder(this).SetTitle("Comprovantes").SetItems(labels, (_, e) => Run(() =>
        {
            if (e.Which == 0)
            {
                OpenDocument(uri =>
                {
                    var name = "comprovante";
                    using (var cursor = ContentResolver!.Query(uri, [OpenableColumns.DisplayName!], null, null, null))
                        if (cursor?.MoveToFirst() == true) name = cursor.GetString(0) ?? name;
                    using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível.");
                    using var memory = new MemoryStream();
                    CopyLimited(input, memory, 5 * 1024 * 1024);
                    _service.AddAttachment(type, id, Path.GetFileName(name), memory.ToArray());
                    Notice("Comprovante adicionado");
                });
            }
            else
            {
                var attachment = attachments[e.Which - 1];
                new AlertDialog.Builder(this).SetTitle(attachment.Name).SetItems(new[] { "Exportar", "Excluir" }, (_, choice) => Run(() =>
                {
                    if (choice.Which == 0) SaveDocument(attachment.Name, "application/octet-stream", uri =>
                    {
                        using var output = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Destino indisponível.");
                        output.Write(_service.ReadAttachment(attachment.Id));
                        Notice("Comprovante exportado");
                    });
                    else Confirm("Excluir comprovante?", attachment.Name, () => _service.DeleteAttachment(attachment.Id));
                })).Show();
            }
        })).Show();
    }

    private void SplitMenu(int expenseId)
    {
        var choices = Enumerable.Range(2, 9).Select(n => n + " categorias").Append("Remover rateio").ToArray();
        new AlertDialog.Builder(this).SetTitle("Rateio da despesa").SetItems(choices, (_, e) => Run(() =>
        {
            if (e.Which == choices.Length - 1)
            {
                Confirm("Remover rateio?", "Manter a categoria principal da despesa.", () => { _service.SaveExpenseSplits(expenseId, []); RefreshPage(); });
                return;
            }
            var count = e.Which + 2;
            var existing = _service.GetExpenseSplits(expenseId);
            var form = new EntryForm(_ui, RefreshPage);
            var categories = _service.GetCategories(FinanceTypes.Expense);
            var fields = new List<(EntryForm.SelectionField<LookupItem> Category, EntryForm.MoneyField Amount)>();
            for (var i = 0; i < count; i++)
                fields.Add((form.Lookup($"Categoria {i + 1}", categories, selectedId: i < existing.Count ? existing[i].CategoryId : 0), form.Money($"Valor {i + 1} (R$)", i < existing.Count ? existing[i].Amount : 0)));
            form.Show("Dividir entre categorias", () => _service.SaveExpenseSplits(expenseId, fields.Select(f => new ExpenseSplit(f.Category.Value.Id, f.Amount.Value)).ToArray()));
        })).Show();
    }
}
