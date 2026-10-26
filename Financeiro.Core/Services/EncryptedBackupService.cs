using System.Security.Cryptography;
using System.Text;

namespace FinanceiroApp.Services;

public sealed class EncryptedBackupService(FinanceService service)
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("FINBK001");
    private const int HeaderLength = 8 + 16 + 12 + 16;
    private const int MaxBytes = 256 * 1024 * 1024;
    private const int Iterations = 600000;

    public void Backup(string destination, string password)
    {
        if (password.Length < 10) throw new InvalidOperationException("Use uma senha de backup com ao menos dez caracteres.");
        if (Path.GetFullPath(destination).Equals(service.DatabasePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Escolha um arquivo diferente do banco em uso.");
        var temporary = service.DatabasePath + $".{Guid.NewGuid():N}.plain";
        var output = Path.GetFullPath(destination) + $".{Guid.NewGuid():N}.tmp";
        byte[]? plain = null;
        byte[]? key = null;
        try
        {
            service.BackupDatabase(temporary);
            if (new FileInfo(temporary).Length > MaxBytes) throw new InvalidOperationException("O backup criptografado suporta bancos de ate 256 MB.");
            plain = File.ReadAllBytes(temporary);
            var envelope = new byte[HeaderLength + plain.Length];
            Magic.CopyTo(envelope, 0);
            RandomNumberGenerator.Fill(envelope.AsSpan(8, 16));
            RandomNumberGenerator.Fill(envelope.AsSpan(24, 12));
            key = Rfc2898DeriveBytes.Pbkdf2(password, envelope.AsSpan(8, 16), Iterations, HashAlgorithmName.SHA256, 32);
            using var cipher = new AesGcm(key, 16);
            cipher.Encrypt(envelope.AsSpan(24, 12), plain, envelope.AsSpan(HeaderLength), envelope.AsSpan(36, 16), envelope.AsSpan(0, 36));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            File.WriteAllBytes(output, envelope);
            File.Move(output, destination, true);
        }
        finally
        {
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    public void Restore(string source, string password)
    {
        if (new FileInfo(source).Length is < HeaderLength or > MaxBytes + HeaderLength) throw new InvalidOperationException("Backup criptografado invalido ou muito grande.");
        var envelope = File.ReadAllBytes(source);
        if (!envelope.AsSpan(0, 8).SequenceEqual(Magic)) throw new InvalidOperationException("Formato de backup criptografado desconhecido.");
        var plain = new byte[envelope.Length - HeaderLength];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, envelope.AsSpan(8, 16), Iterations, HashAlgorithmName.SHA256, 32);
        var temporary = service.DatabasePath + $".{Guid.NewGuid():N}.plain";
        try
        {
            using var cipher = new AesGcm(key, 16);
            try { cipher.Decrypt(envelope.AsSpan(24, 12), envelope.AsSpan(HeaderLength), envelope.AsSpan(36, 16), plain, envelope.AsSpan(0, 36)); }
            catch (CryptographicException ex) { throw new InvalidOperationException("Senha incorreta ou backup alterado. Os dados atuais foram preservados.", ex); }
            File.WriteAllBytes(temporary, plain);
            service.RestoreDatabase(temporary);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(key);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
