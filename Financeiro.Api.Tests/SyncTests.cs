using System.Data;
using System.Net;
using Financeiro.Api;
using Financeiro.Sync.Contracts;
using FinanceiroApp.Models;
using FinanceiroApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Financeiro.Api.Tests;

public sealed class SyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Financeiro.Api.Tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<SyncClient> _clients = [];
    private const string Url = "https://localhost/";
    private const string Email = "teste@example.test";
    private const string Password = "SenhaForte!123456";
    private static ReportFilter All => new() { StartDate = new DateTime(2000, 1, 1), EndDate = new DateTime(2100, 12, 31) };

    public SyncTests()
    {
        Directory.CreateDirectory(_root);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Financeiro"] = $"Data Source={Path.Combine(_root, "server.db")};Pooling=False",
                ["DataProtection:KeysPath"] = Path.Combine(_root, "keys"),
                ["Auth:AllowRegistration"] = "true"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SyncDbContext>>();
                services.AddDbContext<SyncDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(_root, "server.db")};Pooling=False"));
            });
        });
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SyncDbContext>().Database.EnsureCreated();
    }

    private (FinanceService Service, SyncClient Client) Device(string name)
    {
        var service = new FinanceService(Path.Combine(_root, name + ".db")); service.Initialize();
        var client = new SyncClient(service, _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(Url) }));
        _clients.Add(client); return (service, client);
    }

    private static int Account(FinanceService service, string name, decimal balance = 0)
    {
        service.SaveAccount(new AccountInput { Name = name, Type = "Conta", InitialBalance = balance });
        return service.GetAccounts(false).Single(a => a.Name == name).Id;
    }

    private static void Income(FinanceService service, int account, decimal amount, string description = "Receita", int id = 0)
    {
        service.SaveIncome(new IncomeInput { Id = id, Description = description, Amount = amount, AccountId = account, CategoryId = service.GetCategories(FinanceTypes.Income, false).First().Id });
    }

    [Fact]
    public async Task EndpointsRequireAuthentication()
    {
        using var http = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/sync?cursor=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task IndependentLocalIdsAreRemappedAndTransfersKeepBalances()
    {
        var a = Device("a"); var b = Device("b");
        var accountA = Account(a.Service, "Banco A", 1000); Income(a.Service, accountA, 100.29m);
        var accountB = Account(b.Service, "Banco B", 500); Income(b.Service, accountB, 50.11m, "Receita B");
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync(); await a.Client.SynchronizeAsync();
        Assert.Equal(2, a.Service.GetAccounts(false).Count);
        Assert.Equal(2, b.Service.GetAccounts(false).Count);
        Assert.Equal(1650.40m, a.Service.GetDashboardSummary().CurrentBalance);
        Assert.Equal(1650.40m, b.Service.GetDashboardSummary().CurrentBalance);
        var accounts = b.Service.GetAccounts(false);
        b.Service.SaveTransfer(new TransferInput { FromAccountId = accounts.Single(x => x.Name == "Banco A").Id, ToAccountId = accounts.Single(x => x.Name == "Banco B").Id, Amount = 10.01m });
        await b.Client.SynchronizeAsync(); await a.Client.SynchronizeAsync();
        Assert.Single(a.Service.SearchTransfers(All.StartDate, All.EndDate).Rows.Cast<DataRow>());
        Assert.Equal(1090.28m, a.Service.GetAccountBalanceAt(accountA, DateTime.Today));
    }

    [Fact]
    public async Task ConcurrentEditsRequireExplicitChoiceAndKeepLocalWorks()
    {
        var a = Device("a"); var b = Device("b"); var account = Account(a.Service, "Banco", 1000); Income(a.Service, account, 10);
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync();
        var idA = Convert.ToInt32(a.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        var idB = Convert.ToInt32(b.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        Income(a.Service, account, 20, id: idA); Income(b.Service, b.Service.GetAccounts(false).Single().Id, 30, id: idB);
        await a.Client.SynchronizeAsync();
        var conflict = await Assert.ThrowsAsync<SyncConflictException>(() => b.Client.SynchronizeAsync());
        Assert.Equal(1030m, b.Service.GetDashboardSummary().CurrentBalance);
        await b.Client.SynchronizeAsync(conflict.Conflicts.Select(c => new SyncResolution(c.Id, c.Remote.Revision, true)).ToArray());
        await a.Client.SynchronizeAsync();
        Assert.Equal(1030m, a.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public async Task DeletedRecordsAndAttachmentsPropagate()
    {
        var a = Device("a"); var b = Device("b"); var account = Account(a.Service, "Banco"); Income(a.Service, account, 10);
        var id = Convert.ToInt32(a.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        a.Service.AddAttachment("income", id, "recibo.txt", [1, 2, 3]);
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync();
        var idB = Convert.ToInt32(b.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, b.Service.ReadAttachment(b.Service.GetAttachments("income", idB).Single().Id));
        a.Service.DeleteIncome(id); await a.Client.SynchronizeAsync(); await b.Client.SynchronizeAsync();
        Assert.Equal(0, b.Service.SearchIncomes(All, "", "Data").Rows.Count);
        await b.Client.SynchronizeAsync();
    }

    [Fact]
    public async Task CreditCardsGoalsContributionsAndSplitsRoundTrip()
    {
        var a = Device("a"); var b = Device("b"); var account = Account(a.Service, "Banco", 1000);
        var category = a.Service.GetCategories(FinanceTypes.Expense, false).First().Id;
        a.Service.SaveCreditCard(new CreditCardInput { Name = "Cartao", TotalLimit = 1000, ClosingDay = 25, DueDay = 8 });
        var card = a.Service.GetCreditCards(false).Single().Id;
        a.Service.SaveCardPurchase(new CardPurchaseInput { Description = "Compra", Amount = 50.01m, CreditCardId = card, CategoryId = category });
        a.Service.SaveGoal(new GoalInput { Name = "Reserva", TargetAmount = 1000, CurrentAmount = 50 });
        var goal = Convert.ToInt32(a.Service.SearchGoals("").Rows[0]["Id"]);
        a.Service.AddGoalContribution(goal, 10, DateTime.Today, "");
        a.Service.SaveExpense(new ExpenseInput { Description = "Rateio", Amount = 20, AccountId = account, CategoryId = category });
        var expense = Convert.ToInt32(a.Service.SearchExpenses(All, "", "Data").Rows[0]["Id"]);
        a.Service.SaveExpenseSplits(expense, [new ExpenseSplit(category, 12), new ExpenseSplit(a.Service.GetCategories(FinanceTypes.Expense, false).Last().Id, 8)]);
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync();
        Assert.Equal(949.99m, b.Service.SearchCreditCards("").Rows[0]["LimiteDisponivel"]);
        Assert.Equal(60m, b.Service.SearchGoals("").Rows[0]["ValorAtual"]);
        var goalB = Convert.ToInt32(b.Service.SearchGoals("").Rows[0]["Id"]);
        a.Service.AddGoalContribution(goal, 10, DateTime.Today, ""); b.Service.AddGoalContribution(goalB, 20, DateTime.Today, "");
        await a.Client.SynchronizeAsync(); await b.Client.SynchronizeAsync(); await a.Client.SynchronizeAsync();
        Assert.Equal(90m, a.Service.SearchGoals("").Rows[0]["ValorAtual"]);
        Assert.Equal(90m, b.Service.SearchGoals("").Rows[0]["ValorAtual"]);
        var expenseB = Convert.ToInt32(b.Service.SearchExpenses(All, "", "Data").Rows[0]["Id"]);
        Assert.Equal(2, b.Service.GetExpenseSplits(expenseB).Count);
    }

    [Fact]
    public async Task DifferentUsersCannotReadEachOthersFinancialRecords()
    {
        var a = Device("a"); var b = Device("b"); Account(a.Service, "Segredo", 1000);
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, "outro@example.test", Password, true); await b.Client.SynchronizeAsync();
        Assert.Empty(b.Service.GetAccounts(false));
    }

    [Fact]
    public async Task ConcurrentImportOfSameBankIdentityDoesNotDuplicateTransaction()
    {
        var a = Device("a"); var b = Device("b"); var account = Account(a.Service, "Banco");
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync();
        foreach (var device in new[] { a, b })
        {
            var entries = device.Service.PrepareImport(device.Service.GetAccounts(false).Single().Id, [new ImportEntry { Date = DateTime.Today, Description = "Mesmo movimento", Amount = 1.01m, SourceId = "ofx:123" }]);
            device.Service.ImportStatement(device.Service.GetAccounts(false).Single().Id, entries, device.Service.GetCategories(FinanceTypes.Income, false).First().Id, device.Service.GetCategories(FinanceTypes.Expense, false).First().Id);
        }
        await a.Client.SynchronizeAsync(); await b.Client.SynchronizeAsync(); await a.Client.SynchronizeAsync();
        Assert.Single(a.Service.SearchIncomes(All, "", "Data").Rows.Cast<DataRow>());
        Assert.Single(b.Service.SearchIncomes(All, "", "Data").Rows.Cast<DataRow>());
    }

    [Fact]
    public async Task ServerChoiceDiscardsOnlyTheConflictingLocalEdit()
    {
        var a = Device("a"); var b = Device("b"); var account = Account(a.Service, "Banco"); Income(a.Service, account, 10);
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await b.Client.LoginAsync(Url, Email, Password); await b.Client.SynchronizeAsync();
        var idA = Convert.ToInt32(a.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        var idB = Convert.ToInt32(b.Service.SearchIncomes(All, "", "Data").Rows[0]["Id"]);
        Income(a.Service, account, 20, id: idA); Income(b.Service, b.Service.GetAccounts(false).Single().Id, 30, id: idB);
        await a.Client.SynchronizeAsync();
        var conflict = await Assert.ThrowsAsync<SyncConflictException>(() => b.Client.SynchronizeAsync());
        await b.Client.SynchronizeAsync(conflict.Conflicts.Select(c => new SyncResolution(c.Id, c.Remote.Revision, false)).ToArray());
        Assert.Equal(20m, b.Service.GetDashboardSummary().CurrentBalance);
    }

    [Fact]
    public async Task RepeatedLoginOnSameSessionIsSupported()
    {
        var a = Device("a");
        await a.Client.LoginAsync(Url, Email, Password, true); await a.Client.SynchronizeAsync();
        await a.Client.LoginAsync(Url, Email, Password); await a.Client.SynchronizeAsync();
        Assert.True(a.Client.Connected);
    }

    public void Dispose()
    {
        foreach (var client in _clients) client.Dispose();
        _factory.Dispose();
        var absolute = Path.GetFullPath(_root);
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Financeiro.Api.Tests")) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Pasta de teste invalida.");
        Directory.Delete(absolute, true);
    }
}
