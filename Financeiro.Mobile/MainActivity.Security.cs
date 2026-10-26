using Android.Hardware.Biometrics;
using Android.Text;
using Android.Text.Method;
using Android.Views;
using Financeiro.Mobile.Forms;
using Financeiro.Mobile.UI;
using FinanceiroApp.Services;
using Path = System.IO.Path;

namespace Financeiro.Mobile;

public partial class MainActivity
{
    private AppLockService _appLock = null!;
    private Dialog? _lockDialog;
    private bool _authenticated;
    private DateTime _backgroundAt;
    private Action? _afterUnlock;
    private Android.OS.CancellationSignal? _biometricCancellation;

    protected override void OnPause()
    {
        StopAutoSync();
        _backgroundAt = DateTime.UtcNow;
        _biometricCancellation?.Cancel();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_syncClient is not null) StartAutoSync();
        if (_appLock is null || !_appLock.Enabled) return;
        if (!_authenticated || _backgroundAt != default && DateTime.UtcNow - _backgroundAt >= TimeSpan.FromMinutes(2)) LockMobile();
    }

    private void LockMobile()
    {
        if (_lockDialog is not null) return;
        _authenticated = false;
        Window!.AddFlags(WindowManagerFlags.Secure);
        var layout = _ui.Column(28);
        layout.SetBackgroundColor(_ui.BackgroundColor);
        layout.SetGravity(GravityFlags.CenterVertical);
        layout.AddView(_ui.Text("Financeiro", 28, bold: true));
        _ui.Gap(layout, 24);
        layout.AddView(_ui.Text("Aplicativo bloqueado", 18, bold: true));
        _ui.Gap(layout, 20);
        var pin = new EditText(this) { Hint = "PIN", TextSize = 20, InputType = InputTypes.ClassNumber | InputTypes.NumberVariationPassword, TransformationMethod = PasswordTransformationMethod.Instance };
        pin.SetTextColor(_ui.Ink); pin.SetHintTextColor(_ui.Muted);
        layout.AddView(pin, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, _ui.Dp(56)));
        var error = _ui.Text("", 13, _ui.Danger);
        layout.AddView(error);
        _ui.Gap(layout, 20);
        layout.AddView(_ui.Button("Desbloquear", () =>
        {
            try
            {
                if (_appLock.Verify(pin.Text ?? "")) UnlockMobile();
                else { error.Text = "PIN incorreto"; pin.Text = ""; }
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }));
        if (OperatingSystem.IsAndroidVersionAtLeast(28) && _service.GetSetting("MobileBiometric") == "true")
        {
            _ui.Gap(layout, 12);
            layout.AddView(_ui.Button("Usar biometria", () => AuthenticateBiometric(error), false));
        }
        _lockDialog = new Dialog(this, Android.Resource.Style.ThemeMaterialLightNoActionBar);
        _lockDialog.SetCancelable(false);
        _lockDialog.SetContentView(layout);
        _lockDialog.Window!.AddFlags(WindowManagerFlags.Secure);
        _lockDialog.Show();
        _lockDialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        _lockDialog.Window.SetSoftInputMode(SoftInput.AdjustResize);
    }

    private void UnlockMobile()
    {
        _authenticated = true;
        _backgroundAt = default;
        _lockDialog?.Dismiss(); _lockDialog?.Dispose(); _lockDialog = null;
        var callback = _afterUnlock; _afterUnlock = null;
        callback?.Invoke();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private void AuthenticateBiometric(TextView error)
    {
        try
        {
            _biometricCancellation?.Dispose();
            _biometricCancellation = new Android.OS.CancellationSignal();
            var prompt = new BiometricPrompt.Builder(this).SetTitle("Desbloquear Financeiro")!
                .SetNegativeButton("Usar PIN", MainExecutor!, new BiometricNegative())!.Build()!;
            prompt.Authenticate(_biometricCancellation, MainExecutor!, new BiometricResult(UnlockMobile, message => error.Text = message));
        }
        catch (Exception) { error.Text = "Biometria indisponível. Use o PIN."; }
    }

    private sealed class BiometricNegative : Java.Lang.Object, Android.Content.IDialogInterfaceOnClickListener
    {
        public void OnClick(Android.Content.IDialogInterface? dialog, int which) { }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private sealed class BiometricResult(Action success, Action<string> error) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) => success();
        public override void OnAuthenticationFailed() => error("Biometria não reconhecida.");
        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString) => error("Biometria indisponível. Use o PIN.");
    }

    private void ShowSecurity()
    {
        Heading("Segurança", _appLock.Enabled ? "Bloqueio ativado" : "Bloqueio desativado");
        MenuRow(_appLock.Enabled ? "Alterar PIN" : "Ativar bloqueio por PIN", "PIN de 6 a 12 números", "\ue897", () =>
        {
            var form = new EntryForm(_ui, RefreshPage);
            var current = _appLock.Enabled ? form.Password("PIN atual", true) : null;
            var pin = form.Password("Novo PIN", true);
            var confirm = form.Password("Confirmar PIN", true);
            form.Show("Bloqueio por PIN", () =>
            {
                if (pin.Text != confirm.Text) throw new InvalidOperationException("A confirmação do PIN não confere.");
                _appLock.Configure(pin.Text ?? "", current?.Text);
                _authenticated = true; Window!.AddFlags(WindowManagerFlags.Secure);
            });
        });
        if (_appLock.Enabled)
        {
            var biometric = _ui.Card();
            if (OperatingSystem.IsAndroidVersionAtLeast(28)) AddSwitch(biometric, "Desbloqueio por biometria", _service.GetSetting("MobileBiometric") == "true", value => _service.SetSetting("MobileBiometric", value ? "true" : "false"));
            _content.AddView(biometric);
            MenuRow("Bloquear agora", "Exigir desbloqueio", "\ue897", LockMobile);
            MenuRow("Remover bloqueio", "Confirmar PIN atual", "\ue898", () =>
            {
                var form = new EntryForm(_ui, RefreshPage);
                var pin = form.Password("PIN atual", true);
                form.Show("Remover bloqueio", () => { _appLock.Disable(pin.Text ?? ""); _service.SetSetting("MobileBiometric", "false"); Window!.ClearFlags(WindowManagerFlags.Secure); });
            });
        }
    }

    private void PromptBackupPassword(bool confirmation, Action<string> action)
    {
        var form = new EntryForm(_ui, () => { });
        var password = form.Password("Senha do backup");
        var confirm = confirmation ? form.Password("Confirmar senha") : null;
        form.Show("Backup protegido", () =>
        {
            if (confirmation && password.Text != confirm!.Text) throw new InvalidOperationException("As senhas não conferem.");
            if (confirmation && (password.Text?.Length ?? 0) < 10) throw new InvalidOperationException("Use ao menos dez caracteres.");
            action(password.Text ?? "");
        });
    }

    private void ExportEncryptedBackup()
    {
        PromptBackupPassword(true, password => _ = RunAsync(async () =>
        {
            var temporary = Path.Combine(CacheDir!.AbsolutePath, $"backup-{Guid.NewGuid():N}.finbackup");
            await Task.Run(() => new EncryptedBackupService(_service).Backup(temporary, password));
            RunOnUiThread(() => SaveDocument($"financeiro-{DateTime.Today:yyyyMMdd}.finbackup", "application/octet-stream", uri =>
            {
                try
                {
                    using var input = File.OpenRead(temporary);
                    using var output = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Destino indisponível.");
                    input.CopyTo(output); Notice("Backup protegido exportado");
                }
                finally { File.Delete(temporary); }
            }));
        }));
    }

    private void ImportEncryptedBackup()
    {
        OpenDocument(uri =>
        {
            var temporary = Path.Combine(CacheDir!.AbsolutePath, $"restore-{Guid.NewGuid():N}.finbackup");
            using (var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo indisponível."))
            using (var output = File.Create(temporary)) CopyLimited(input, output, 256 * 1024 * 1024 + 52);
            PromptBackupPassword(false, password => Confirm("Restaurar backup?", "Os dados atuais serão substituídos.", () => _ = RunAsync(async () =>
            {
                try { await Task.Run(() => new EncryptedBackupService(_service).Restore(temporary, password)); }
                finally { File.Delete(temporary); }
            }, () => { RefreshPage(); Notice("Backup restaurado"); })));
        });
    }
}
