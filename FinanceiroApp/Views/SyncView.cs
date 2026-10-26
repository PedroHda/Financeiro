using System.ComponentModel;
using System.Text.Json.Nodes;
using Financeiro.Sync.Contracts;
using FinanceiroApp.Helpers;
using FinanceiroApp.Services;

namespace FinanceiroApp.Views;

public sealed class SyncView : BaseView
{
    private readonly SyncClient _client;
    private readonly TextBox _server = Ui.TextBox();
    private readonly TextBox _email = Ui.TextBox();
    private readonly TextBox _password = Ui.TextBox();
    private readonly CheckBox _register = new() { Text = "Criar conta", AutoSize = true };
    private readonly CheckBox _automatic = new() { Text = "Sincronizar durante o uso", AutoSize = true };
    private readonly Label _status = Ui.Label("");
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 5000 };

    public SyncView(FinanceService service, SyncClient client) : base(service, "Sincronizacao")
    {
        _client = client;
        _password.UseSystemPasswordChar = true;
        var fields = Ui.FormGrid();
        Ui.AddField(fields, "Servidor HTTPS", _server); Ui.AddField(fields, "Email", _email);
        Ui.AddField(fields, "Senha", _password); Ui.AddField(fields, "", _register);
        Ui.AddField(fields, "", _automatic); Ui.AddField(fields, "Status", _status);
        _automatic.CheckedChanged += (_, _) => { _client.AutoSync = _automatic.Checked; Service.SetSetting("SyncAuto", _automatic.Checked ? "true" : "false"); };
        var actions = Ui.Toolbar();
        actions.Controls.Add(Ui.Button("Entrar", async (_, _) => await Login(), true));
        actions.Controls.Add(Ui.Button("Sincronizar agora", async (_, _) => await Synchronize()));
        actions.Controls.Add(Ui.Button("Desconectar", (_, _) => { _client.Disconnect(); RefreshStatus(); }));
        var panel = SurfacePanel(); panel.Controls.Add(actions); panel.Controls.Add(fields); Content.Controls.Add(panel);
        _statusTimer.Tick += (_, _) => RefreshStatus(); _statusTimer.Start();
        Disposed += (_, _) => _statusTimer.Dispose();
    }

    public override void RefreshData()
    {
        _server.Text = Service.GetSetting("SyncServer"); _email.Text = Service.GetSetting("SyncEmail");
        _automatic.Checked = _client.AutoSync; RefreshStatus();
    }

    private void RefreshStatus()
    {
        _status.Text = _client.LastError ?? (_client.Connected ? $"Conectado: {_client.Email}\nUltima sincronizacao: {_client.LastSync?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "pendente"}" : "Desconectado");
        _status.AutoSize = true;
    }

    private async Task Login()
    {
        if (MessageBox.Show($"Vincular os dados locais a {_email.Text.Trim()}? Ao sincronizar, os registros deste banco serao enviados para essa conta.", "Conectar conta", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Enabled = false;
        try
        {
            await _client.LoginAsync(_server.Text, _email.Text.Trim(), _password.Text, _register.Checked);
            _automatic.Checked = false; _client.AutoSync = false; _register.Checked = false;
            Ui.ShowInfo("Conta conectada.");
        }
        catch (Exception ex) { Ui.ShowError(ex); }
        finally { _password.Clear(); Enabled = true; RefreshStatus(); }
    }

    private async Task Synchronize()
    {
        Enabled = false;
        try
        {
            IReadOnlyList<SyncResolution>? resolutions = null;
            while (true)
            {
                try { await _client.SynchronizeAsync(resolutions); Ui.ShowInfo("Sincronizacao concluida."); break; }
                catch (SyncConflictException ex)
                {
                    resolutions = ResolveConflicts(ex.Conflicts);
                    if (resolutions is null) break;
                }
            }
        }
        catch (Exception ex) { Ui.ShowError(ex); }
        finally { Enabled = true; RefreshStatus(); }
    }

    private sealed class Choice
    {
        public string Registro { get; set; } = "";
        public string Local { get; set; } = "";
        public string Servidor { get; set; } = "";
        public string Manter { get; set; } = "Escolha";
    }

    private IReadOnlyList<SyncResolution>? ResolveConflicts(IReadOnlyList<SyncConflict> conflicts)
    {
        using var dialog = new Form { Text = "Conflitos de sincronizacao", Size = new Size(1000, 540), MinimumSize = new Size(760, 400), StartPosition = FormStartPosition.CenterParent, Font = Ui.BodyFont };
        var rows = new BindingList<Choice>(conflicts.Select(c => new Choice { Registro = c.Table, Local = Describe(c.LocalPayload), Servidor = Describe(c.Remote.Payload) }).ToList());
        var grid = Ui.Grid(); grid.AutoGenerateColumns = false; grid.ReadOnly = false;
        foreach (var name in new[] { "Registro", "Local", "Servidor" }) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = name, HeaderText = name, ReadOnly = true });
        grid.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = "Manter", HeaderText = "Manter", DataSource = new[] { "Escolha", "Este aparelho", "Servidor" } });
        grid.DataSource = rows;
        var actions = Ui.Toolbar(); actions.Dock = DockStyle.Bottom;
        actions.Controls.Add(Ui.Button("Confirmar escolhas", (_, _) =>
        {
            grid.EndEdit();
            if (rows.Any(r => r.Manter == "Escolha")) { Ui.ShowError(new InvalidOperationException("Escolha qual versao manter em cada registro.")); return; }
            dialog.DialogResult = DialogResult.OK;
        }, true));
        actions.Controls.Add(Ui.Button("Cancelar", (_, _) => dialog.Close()));
        dialog.Controls.Add(grid); dialog.Controls.Add(actions);
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? conflicts.Select((c, i) => new SyncResolution(c.Id, c.Remote.Revision, rows[i].Manter == "Este aparelho")).ToArray() : null;
    }

    private static string Describe(string? payload)
    {
        if (payload is null) return "Excluido";
        var record = JsonNode.Parse(payload)!;
        var name = record["Description"]?.ToString() ?? record["Name"]?.ToString() ?? record["Month"]?.ToString() ?? "Registro";
        var amount = record["Amount"] ?? record["TotalAmount"] ?? record["InitialBalance"];
        return name + (amount is null ? "" : " · " + (amount.GetValue<long>() / 100m).ToString("C2"));
    }
}
