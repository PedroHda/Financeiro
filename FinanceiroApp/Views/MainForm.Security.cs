using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed partial class MainForm : IMessageFilter
{
    private AppLockService _appLock = null!;
    private readonly System.Windows.Forms.Timer _lockTimer = new() { Interval = 10000 };
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _locking;

    private void InitializeSecurity()
    {
        _appLock = new AppLockService(_service.DatabasePath + ".lock.json");
        _workspace.Visible = !_appLock.Enabled;
        _menu.Enabled = !_appLock.Enabled;
        Application.AddMessageFilter(this);
        Shown += (_, _) => { if (_appLock.Enabled) LockApplication(); _lockTimer.Start(); };
        _lockTimer.Tick += (_, _) => { if (_appLock.Enabled && !_locking && DateTime.UtcNow - _lastActivity >= TimeSpan.FromMinutes(2)) LockApplication(); };
        FormClosed += (_, _) => { _lockTimer.Dispose(); Application.RemoveMessageFilter(this); };
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is >= 0x100 and <= 0x109 or >= 0x200 and <= 0x20E) _lastActivity = DateTime.UtcNow;
        return false;
    }

    private void LockApplication()
    {
        if (_locking) return;
        _locking = true;
        _workspace.Visible = false; _menu.Enabled = false;
        using var dialog = new Form
        {
            Text = "Financeiro bloqueado",
            StartPosition = FormStartPosition.CenterParent,
            Size = Size,
            MinimumSize = new Size(440, 280),
            Font = Ui.BodyFont,
            BackColor = Ui.Surface,
            MaximizeBox = false,
            MinimizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog
        };
        var coveredBounds = Application.OpenForms.Cast<Form>().Where(f => f.Visible).Aggregate(Bounds, (bounds, form) => Rectangle.Union(bounds, form.Bounds));
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Bounds = coveredBounds;
        var fields = Ui.FormGrid();
        var pin = Ui.TextBox(); pin.UseSystemPasswordChar = true;
        var error = Ui.Label(""); error.AutoSize = true;
        Ui.AddField(fields, "PIN", pin); Ui.AddField(fields, "", error);
        var actions = Ui.Toolbar(); actions.Dock = DockStyle.Bottom;
        var unlock = Ui.Button("Desbloquear", (_, _) =>
        {
            try
            {
                if (_appLock.Verify(pin.Text)) dialog.DialogResult = DialogResult.OK;
                else { error.Text = "PIN incorreto."; pin.Clear(); pin.Focus(); }
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }, true);
        actions.Controls.Add(unlock);
        dialog.Controls.Add(fields); dialog.Controls.Add(actions); dialog.AcceptButton = unlock;
        var result = dialog.ShowDialog(Form.ActiveForm ?? this);
        _locking = false;
        if (result != DialogResult.OK) { Close(); return; }
        _workspace.Visible = true; _menu.Enabled = true; _lastActivity = DateTime.UtcNow;
    }
}
