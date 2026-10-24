using FinanceiroApp.Models;
using FinanceiroApp.Services;

namespace FinanceiroApp.Tests;

public sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "FinanceiroApp.Tests", Guid.NewGuid().ToString("N"));
    public FinanceService Service { get; }
    public int AccountId { get; }
    public int IncomeCategoryId { get; }
    public int ExpenseCategoryId { get; }

    public TestDatabase()
    {
        Service = new FinanceService(Path.Combine(DirectoryPath, "test.db"));
        Service.Initialize();
        Service.SaveAccount(new AccountInput { Name = "Conta teste", Type = "Conta corrente", InitialBalance = 1000 });
        AccountId = Service.GetAccounts(false).Single().Id;
        IncomeCategoryId = Service.GetCategories(FinanceTypes.Income, false).First().Id;
        ExpenseCategoryId = Service.GetCategories(FinanceTypes.Expense, false).First().Id;
    }

    public int CreateCard(string name = "Cartao teste", decimal limit = 1000)
    {
        Service.SaveCreditCard(new CreditCardInput { Name = name, TotalLimit = limit, ClosingDay = 25, DueDay = 8 });
        return Service.GetCreditCards(false).Single(card => card.Name == name).Id;
    }

    public void Dispose()
    {
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FinanceiroApp.Tests")) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(DirectoryPath);
        if (!absolute.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O diretorio de teste esta fora da pasta temporaria esperada.");
        }
        Directory.Delete(absolute, recursive: true);
    }
}
