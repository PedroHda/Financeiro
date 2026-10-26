using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class SecurityView : BaseView
{
    private readonly AppLockService _lock;
    private readonly TextBox _current = Ui.TextBox();
    private readonly TextBox _new = Ui.TextBox();
    private readonly TextBox _confirm = Ui.TextBox();
    private readonly Label _status = Ui.Label("");

    public SecurityView(FinanceService service) : base(service, "Seguranca")
    {
        _lock = new AppLockService(service.DatabasePath + ".lock.json");
        foreach (var field in new[] { _current, _new, _confirm }) field.UseSystemPasswordChar = true;
        var form = Ui.FormGrid();
        Ui.AddField(form, "Bloqueio", _status);
        Ui.AddField(form, "PIN atual", _current);
        Ui.AddField(form, "Novo PIN", _new);
        Ui.AddField(form, "Confirmar PIN", _confirm);
        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Salvar PIN", (_, _) => RunSave(() =>
        {
            if (_new.Text != _confirm.Text) throw new InvalidOperationException("A confirmacao do PIN nao confere.");
            _lock.Configure(_new.Text, _current.Text);
        }, RefreshData), true));
        actions.Controls.Add(Ui.Button("Remover bloqueio", (_, _) => RunSave(() => _lock.Disable(_current.Text), RefreshData)));
        var panel = SurfacePanel(); panel.Controls.Add(actions); panel.Controls.Add(form);
        Content.Controls.Add(panel);
    }

    public override void RefreshData()
    {
        _status.Text = _lock.Enabled ? "Ativado" : "Desativado";
        _current.Clear(); _new.Clear(); _confirm.Clear();
    }
}

internal static class PasswordPrompt
{
    internal static string? Ask(IWin32Window owner, string title, bool confirmation = false)
    {
        using var form = new Form { Text = title, Size = new Size(440, 240), MinimumSize = new Size(440, 240), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Ui.BodyFont };
        var fields = Ui.FormGrid();
        var password = Ui.TextBox(); password.UseSystemPasswordChar = true;
        var confirm = Ui.TextBox(); confirm.UseSystemPasswordChar = true;
        Ui.AddField(fields, "Senha", password);
        if (confirmation) Ui.AddField(fields, "Confirmar senha", confirm);
        var actions = Ui.Toolbar(); actions.Dock = DockStyle.Bottom;
        var ok = Ui.Button("Confirmar", (_, _) =>
        {
            if (confirmation && password.Text != confirm.Text) { Ui.ShowError(new InvalidOperationException("As senhas nao conferem.")); return; }
            form.DialogResult = DialogResult.OK;
        }, true);
        var cancel = Ui.Button("Cancelar", (_, _) => form.Close());
        actions.Controls.Add(ok); actions.Controls.Add(cancel);
        form.Controls.Add(fields); form.Controls.Add(actions); form.AcceptButton = ok; form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK ? password.Text : null;
    }
}
