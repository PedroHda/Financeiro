using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceiroApp.Helpers;

public static partial class FinancialFormatting
{
    public static CultureInfo BrazilianCulture { get; } = CultureInfo.GetCultureInfo("pt-BR");

    public static decimal ParseMoney(string text, string fieldName)
    {
        var value = text.Trim();
        if (value.StartsWith("R$", StringComparison.OrdinalIgnoreCase)) value = value[2..].Trim();

        if (BrazilianAmount().IsMatch(value) &&
            decimal.TryParse(value, NumberStyles.Number, BrazilianCulture, out var amount))
        {
            return amount;
        }

        if (DecimalPointAmount().IsMatch(value) &&
            decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount))
        {
            return amount;
        }

        throw new InvalidOperationException($"Informe um valor valido para {fieldName}, por exemplo 1.234,56.");
    }

    [GeneratedRegex(@"^[+-]?(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,2})?$")]
    private static partial Regex BrazilianAmount();

    [GeneratedRegex(@"^[+-]?[0-9]+\.[0-9]{1,2}$")]
    private static partial Regex DecimalPointAmount();
}
