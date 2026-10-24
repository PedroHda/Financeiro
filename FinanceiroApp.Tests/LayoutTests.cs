using System.Runtime.ExceptionServices;
using FinanceiroApp.Views;

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

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
