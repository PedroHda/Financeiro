using FinanceiroApp.Services;

namespace FinanceiroApp.Tests;

public sealed class SecurityTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void EncryptedBackupRoundTripsAndWrongPasswordPreservesDatabase()
    {
        var file = Path.Combine(_db.DirectoryPath, "encrypted.finbackup");
        var encryption = new EncryptedBackupService(_db.Service);
        encryption.Backup(file, "senha forte teste");
        var before = File.ReadAllBytes(_db.Service.DatabasePath);
        Assert.Throws<InvalidOperationException>(() => encryption.Restore(file, "senha errada"));
        Assert.Equal(before, File.ReadAllBytes(_db.Service.DatabasePath));
        _db.Service.ClearFinancialData();
        encryption.Restore(file, "senha forte teste");
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
        var bytes = File.ReadAllBytes(file);
        bytes[^1] ^= 1;
        File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidOperationException>(() => encryption.Restore(file, "senha forte teste"));
        Assert.Equal(1000m, _db.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public void PinHashesAreSaltedAndFailuresAreRateLimited()
    {
        var path = Path.Combine(_db.DirectoryPath, "lock.json");
        var appLock = new AppLockService(path);
        appLock.Configure("123456");
        Assert.DoesNotContain("123456", File.ReadAllText(path));
        Assert.True(appLock.Verify("123456"));
        Assert.False(appLock.Verify("000000"));
        Assert.Throws<InvalidOperationException>(() => appLock.Disable("000000"));
        Assert.Throws<InvalidOperationException>(() => appLock.Configure("654321", "000000"));
        Assert.False(appLock.Verify("000000"));
        Assert.False(appLock.Verify("000000"));
        Assert.True(appLock.RetryIn > TimeSpan.Zero);
        Assert.Throws<InvalidOperationException>(() => appLock.Verify("123456"));
        Assert.True(new AppLockService(path).RetryIn > TimeSpan.Zero);
    }

    public void Dispose() => _db.Dispose();
}
