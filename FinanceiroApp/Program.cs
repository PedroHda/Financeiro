namespace FinanceiroApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = Helpers.FinancialFormatting.BrazilianCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = Helpers.FinancialFormatting.BrazilianCulture;
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Helpers.Ui.ShowError(e.Exception);
        try
        {
            Application.Run(new Views.MainForm());
        }
        catch (Exception ex)
        {
            Helpers.Ui.ShowError(new InvalidOperationException(
                "Nao foi possivel iniciar o Financeiro. Verifique o acesso ao banco em %LOCALAPPDATA%\\FinanceiroApp. " + ex.Message, ex));
        }
    }
}
