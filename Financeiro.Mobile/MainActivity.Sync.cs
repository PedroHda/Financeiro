using System.Text.Json.Nodes;
using Financeiro.Mobile.Forms;
using Financeiro.Sync.Contracts;
using FinanceiroApp.Services;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private SyncClient _syncClient = null!;
    private Android.OS.Handler? _syncHandler;
    private bool _syncResumed;
    private bool _autoSyncing;

    private void ShowSync()
    {
        Heading("Sincronização", _syncClient.Connected ? _syncClient.Email : "Desconectado");
        if (_syncClient.LastSync is { } last) Pair(_content, "Última sincronização", last.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        if (_syncClient.LastError is { } error) _content.AddView(_ui.Text(error, 13, _ui.Danger));
        MenuRow(_syncClient.Connected ? "Trocar conta" : "Entrar ou criar conta", "Servidor HTTPS", "\ue7fd", LoginSync);
        if (!_syncClient.Connected) return;
        MenuRow("Sincronizar agora", "Enviar e receber alterações", "\ue627", () => _ = SynchronizeMobile());
        var block = _ui.Card();
        AddSwitch(block, "Sincronizar durante o uso", _syncClient.AutoSync, value =>
        {
            _syncClient.AutoSync = value; _service.SetSetting("SyncAuto", value ? "true" : "false");
        });
        _content.AddView(block);
        MenuRow("Desconectar", "Encerrar sessão neste aparelho", "\ue879", () => { _syncClient.Disconnect(); RefreshPage(); });
    }

    private void LoginSync()
    {
        var form = new EntryForm(_ui, () => { });
        var server = form.Text("Servidor HTTPS", _service.GetSetting("SyncServer"));
        server.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationUri;
        var email = form.Text("Email", _service.GetSetting("SyncEmail"));
        email.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationEmailAddress;
        var password = form.Password("Senha");
        var register = form.Check("Criar conta", false);
        form.Show("Conectar conta", () =>
        {
            var secret = password.Text ?? "";
            Confirm("Vincular os dados locais?", "Os registros deste banco poderão ser enviados para " + (email.Text ?? "") + ".", () => _ = RunAsync(async () =>
            {
                await _syncClient.LoginAsync(server.Text ?? "", (email.Text ?? "").Trim(), secret, register.Checked);
                _syncClient.AutoSync = false;
                password.Text = "";
            }, RefreshPage));
        });
    }

    private Task SynchronizeMobile() => RunAsync(async () =>
    {
        IReadOnlyList<SyncResolution>? resolutions = null;
        while (true)
        {
            try { await Task.Run(() => _syncClient.SynchronizeAsync(resolutions)); break; }
            catch (SyncConflictException ex)
            {
                var choices = new List<SyncResolution>();
                foreach (var conflict in ex.Conflicts)
                {
                    var choice = await ChooseConflict(conflict);
                    if (choice is null) return;
                    choices.Add(choice);
                }
                resolutions = choices;
            }
        }
    }, () => { RefreshPage(); Notice("Sincronização finalizada"); });

    private Task<SyncResolution?> ChooseConflict(SyncConflict conflict)
    {
        var completion = new TaskCompletionSource<SyncResolution?>();
        string Describe(string? payload)
        {
            if (payload is null) return "Excluído";
            var node = JsonNode.Parse(payload)!;
            var title = node["Description"]?.ToString() ?? node["Name"]?.ToString() ?? conflict.Table;
            var value = node["Amount"] ?? node["TotalAmount"] ?? node["InitialBalance"];
            return title + (value is null ? "" : " · " + Cash(value.GetValue<long>() / 100m));
        }
        new AlertDialog.Builder(this).SetTitle("Qual versão manter?")!
            .SetMessage("Local: " + Describe(conflict.LocalPayload) + "\n\nServidor: " + Describe(conflict.Remote.Payload))!
            .SetNeutralButton("Local", (_, _) => completion.TrySetResult(new SyncResolution(conflict.Id, conflict.Remote.Revision, true)))!
            .SetPositiveButton("Servidor", (_, _) => completion.TrySetResult(new SyncResolution(conflict.Id, conflict.Remote.Revision, false)))!
            .SetNegativeButton("Cancelar", (_, _) => completion.TrySetResult(null))!.SetCancelable(false)!.Show();
        return completion.Task;
    }

    private void StartAutoSync()
    {
        _syncResumed = true;
        _syncHandler ??= new Android.OS.Handler(Android.OS.Looper.MainLooper!);
        _syncHandler.RemoveCallbacks(AutoSyncTick);
        _syncHandler.PostDelayed(AutoSyncTick, 120000);
    }

    private void StopAutoSync()
    {
        _syncResumed = false; _syncHandler?.RemoveCallbacks(AutoSyncTick);
    }

    private async void AutoSyncTick()
    {
        try
        {
            if (_syncResumed && !_busy && !_autoSyncing && _syncClient.Connected && _syncClient.AutoSync && (!_appLock.Enabled || _authenticated))
            {
                _autoSyncing = true;
                try { await Task.Run(() => _syncClient.SynchronizeAsync()); }
                catch (SyncConflictException) { _syncClient.AutoSync = false; }
                catch (Exception) { }
                finally { _autoSyncing = false; }
            }
        }
        finally { if (_syncResumed) _syncHandler?.PostDelayed(AutoSyncTick, 120000); }
    }

    protected override void OnDestroy()
    {
        StopAutoSync(); _syncHandler?.Dispose(); _syncClient?.Dispose();
        _biometricCancellation?.Cancel(); _biometricCancellation?.Dispose(); _lockDialog?.Dispose();
        base.OnDestroy();
    }
}
