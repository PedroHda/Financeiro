using System.Security.Cryptography;
using System.Text.Json;

namespace FinanceiroApp.Services;

public sealed class AppLockService(string path)
{
    private const int Iterations = 600000;
    private sealed record LockState(string Salt, string Hash, int Failures = 0, DateTimeOffset? RetryAfter = null);
    public bool Enabled => File.Exists(path);
    public TimeSpan RetryIn => Read()?.RetryAfter is { } after && after > DateTimeOffset.UtcNow ? after - DateTimeOffset.UtcNow : TimeSpan.Zero;

    public void Configure(string newPin, string? currentPin = null)
    {
        if (Enabled && !Verify(currentPin ?? "")) throw new InvalidOperationException("PIN atual incorreto.");
        if (newPin.Length is < 6 or > 12 || newPin.Any(c => c is < '0' or > '9')) throw new InvalidOperationException("Use um PIN de 6 a 12 numeros.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(newPin, salt, Iterations, HashAlgorithmName.SHA256, 32);
        Write(new LockState(Convert.ToBase64String(salt), Convert.ToBase64String(hash)));
        CryptographicOperations.ZeroMemory(hash);
    }

    public bool Verify(string pin)
    {
        var state = Read();
        if (state is null) return true;
        if (RetryIn > TimeSpan.Zero) throw new InvalidOperationException($"Aguarde {Math.Ceiling(RetryIn.TotalSeconds)} segundos antes de tentar novamente.");
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, Convert.FromBase64String(state.Salt), Iterations, HashAlgorithmName.SHA256, 32);
        var valid = CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(state.Hash));
        CryptographicOperations.ZeroMemory(hash);
        var failures = valid ? 0 : state.Failures + 1;
        Write(state with { Failures = failures, RetryAfter = !valid && failures >= 5 ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(1800, 30 * Math.Pow(2, Math.Min(6, failures - 5)))) : null });
        return valid;
    }

    public void Disable(string pin)
    {
        if (!Verify(pin)) throw new InvalidOperationException("PIN incorreto.");
        if (File.Exists(path)) File.Delete(path);
    }

    private LockState? Read() => Enabled ? JsonSerializer.Deserialize<LockState>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Configuracao de bloqueio invalida.") : null;
    private void Write(LockState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, path, true);
    }
}
