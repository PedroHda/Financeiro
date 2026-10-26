using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Financeiro.Sync.Contracts;

namespace FinanceiroApp.Services;

public sealed class SyncClient : IDisposable
{
    private readonly FinanceService _service;
    private readonly SyncEngine _engine;
    private HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private string? _refreshToken;
    private DateTimeOffset _expires;
    private sealed record Tokens(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record Profile(string Id);
    public bool Connected { get; private set; }
    public string Email { get; private set; } = "";
    public DateTimeOffset? LastSync { get; private set; }
    public string? LastError { get; private set; }
    public bool AutoSync { get; set; }

    public SyncClient(FinanceService service, HttpClient? httpClient = null)
    {
        _service = service; _engine = new SyncEngine(service);
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public async Task LoginAsync(string url, string email, string password, bool register = false, bool allowInsecureLoopback = false, CancellationToken ct = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        ct = cancellation.Token;
        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.Scheme != Uri.UriSchemeHttps && !(allowInsecureLoopback && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp))
            throw new InvalidOperationException("Informe uma URL HTTPS valida para o servidor.");
        await _gate.WaitAsync(ct);
        try
        {
            if (_http.BaseAddress is null) _http.BaseAddress = uri;
            else if (_http.BaseAddress != uri) { _http.Dispose(); _http = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(90) }; }
            _http.DefaultRequestHeaders.Authorization = null;
            Connected = false;
            AutoSync = false;
            if (register)
            {
                using var registration = await _http.PostAsJsonAsync("auth/register", new { email, password }, ct);
                if (!registration.IsSuccessStatusCode) throw new InvalidOperationException("Nao foi possivel cadastrar. Use uma senha de pelo menos 12 caracteres, com maiuscula, minuscula, numero e simbolo, ou confira se o servidor permite cadastros.");
            }
            using var response = await _http.PostAsJsonAsync("auth/login?useCookies=false", new { email, password }, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode == HttpStatusCode.TooManyRequests ? "Muitas tentativas. Aguarde antes de entrar novamente." : "Email ou senha incorretos, ou conta temporariamente bloqueada.");
            SetTokens(await response.Content.ReadFromJsonAsync<Tokens>(ct) ?? throw new InvalidOperationException("Resposta de login invalida."));
            var profile = await _http.GetFromJsonAsync<Profile>("api/me", ct) ?? throw new InvalidOperationException("Perfil de usuario indisponivel.");
            _engine.BindWorkspace(uri.AbsoluteUri + "|" + profile.Id);
            Email = email; Connected = true; LastError = null;
            _service.SetSetting("SyncServer", uri.AbsoluteUri);
            _service.SetSetting("SyncEmail", email);
        }
        finally { _gate.Release(); }
    }

    public async Task SynchronizeAsync(IReadOnlyList<SyncResolution>? resolutions = null, CancellationToken ct = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        ct = cancellation.Token;
        await _gate.WaitAsync(ct);
        try
        {
            if (!Connected) throw new InvalidOperationException("Entre na sua conta de sincronizacao.");
            await RefreshAsync(ct);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var snapshot = _engine.Capture();
                var cursor = snapshot.Cursor;
                long? through = null;
                var incoming = new List<SyncRecord>();
                bool more;
                do
                {
                    using var response = await _http.GetAsync($"api/sync?cursor={cursor}" + (through.HasValue ? $"&through={through}" : ""), ct);
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Nao foi possivel buscar as alteracoes. Confira a conexao e a conta.");
                    var page = await response.Content.ReadFromJsonAsync<SyncPullPage>(ct) ?? throw new InvalidOperationException("Resposta de sincronizacao invalida.");
                    if (page.Cursor < cursor || page.HasMore && page.Cursor == cursor) throw new InvalidOperationException("O servidor retornou um cursor invalido.");
                    incoming.AddRange(page.Records); cursor = page.Cursor; through = page.WorkspaceRevision; more = page.HasMore;
                } while (more);
                _engine.Merge(snapshot, incoming, cursor, resolutions);
                var pending = _engine.Capture();
                if (pending.Pending.Count == 0) { LastSync = DateTimeOffset.Now; LastError = null; return; }
                var batch = new List<SyncChange>();
                var bytes = 0;
                foreach (var change in pending.Pending)
                {
                    var size = Encoding.UTF8.GetByteCount(change.Payload ?? "") + 256;
                    if (batch.Count > 0 && (batch.Count >= SyncProtocol.MaxChanges || bytes + size > 12 * 1024 * 1024)) break;
                    batch.Add(change); bytes += size;
                }
                using var pushed = await _http.PostAsJsonAsync("api/sync", new SyncPushRequest(SyncProtocol.Version, cursor, batch), ct);
                if (pushed.StatusCode == HttpStatusCode.Conflict) continue;
                if (!pushed.IsSuccessStatusCode) throw new InvalidOperationException("O servidor nao aceitou as alteracoes. Os dados locais foram mantidos.");
                var accepted = await pushed.Content.ReadFromJsonAsync<SyncPushResult>(ct) ?? throw new InvalidOperationException("Resposta de envio invalida.");
                _engine.Merge(pending, accepted.Records, accepted.WorkspaceRevision);
                attempt = -1;
            }
            throw new InvalidOperationException("Outros aparelhos estao alterando os dados. Tente sincronizar novamente em instantes.");
        }
        catch (Exception ex) { LastError = ex.Message; throw; }
        finally { _gate.Release(); }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (_expires > DateTimeOffset.UtcNow.AddMinutes(1)) return;
        using var response = await _http.PostAsJsonAsync("auth/refresh", new { refreshToken = _refreshToken }, ct);
        if (!response.IsSuccessStatusCode) { Connected = false; throw new InvalidOperationException("A sessao expirou. Entre novamente."); }
        SetTokens(await response.Content.ReadFromJsonAsync<Tokens>(ct) ?? throw new InvalidOperationException("Resposta de sessao invalida."));
    }

    private void SetTokens(Tokens tokens)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        _refreshToken = tokens.RefreshToken; _expires = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
    }

    public void Disconnect()
    {
        Connected = false; AutoSync = false; _refreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public void Dispose() { _lifetime.Cancel(); Disconnect(); _http.Dispose(); }
}
