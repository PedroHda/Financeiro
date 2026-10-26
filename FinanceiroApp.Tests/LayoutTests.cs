using System.Runtime.ExceptionServices;
using FinanceiroApp.Views;
using FinanceiroApp.Services;

namespace FinanceiroApp.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void AccountEditorRemainsUsableAtMinimumWindowWidth()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var db = new TestDatabase();
                using var host = new Form { ClientSize = new Size(939, 620) };
                using var view = new AccountsView(db.Service);
                host.Controls.Add(view);
                host.CreateControl();
                view.CreateControl();
                host.PerformLayout();
                view.PerformLayout();
                var split = Descendants(view).OfType<SplitContainer>().Single();
                Assert.True(split.Panel2.Width >= 350, $"O formulario de cadastro ficou com apenas {split.Panel2.Width}px de largura.");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void NewOperationSecurityAndSyncViewsRenderAtMinimumSize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var db = new TestDatabase();
                db.Service.SeedDemoData();
                using var client = new SyncClient(db.Service);
                var factories = new (string Name, Func<BaseView> Create)[]
                {
                    ("operacoes", () => new BankingView(db.Service)),
                    ("seguranca", () => new SecurityView(db.Service)),
                    ("sincronizacao", () => new SyncView(db.Service, client))
                };
                foreach (var (name, create) in factories)
                {
                    using var host = new Form { ClientSize = new Size(939, 620), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) };
                    using var view = create();
                    host.Controls.Add(view); host.Show(); view.RefreshData();
                    var tabs = Descendants(view).OfType<TabControl>().FirstOrDefault();
                    for (var i = 0; i < (tabs?.TabPages.Count ?? 1); i++)
                    {
                        if (tabs is not null) tabs.SelectedIndex = i;
                        host.PerformLayout(); view.PerformLayout();
                        Application.DoEvents();
                        using var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height);
                        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.ClientSize));
                        Assert.Contains(Descendants(view).OfType<Button>(), button => button.Width >= 50);
                        var colors = new HashSet<int>();
                        for (var x = 50; x < bitmap.Width - 50; x += 10)
                            for (var y = 70; y < bitmap.Height - 50; y += 10) colors.Add(bitmap.GetPixel(x, y).ToArgb());
                        Assert.True(colors.Count > 4, "A captura da interface ficou vazia.");
                        if (Environment.GetEnvironmentVariable("FINANCEIRO_UI_ARTIFACTS") is { Length: > 0 } output)
                        {
                            Directory.CreateDirectory(output);
                            bitmap.Save(Path.Combine(output, $"{name}-{i}.png"));
                        }
                    }
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
