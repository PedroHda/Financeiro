using System.Globalization;
using FinanceiroApp.Helpers;

namespace FinanceiroApp.Tests;

public sealed class FormattingTests
{
    [Theory]
    [InlineData("123,45", "123.45")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("R$ 1.234,56", "1234.56")]
    [InlineData("123.45", "123.45")]
    [InlineData("1.234", "1234")]
    [InlineData("0,10", "0.10")]
    [InlineData("-100,25", "-100.25")]
    public void MoneyParsingIsIndependentOfWindowsCulture(string input, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), FinancialFormatting.ParseMoney(input, "valor"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,234")]
    [InlineData("12.34.56")]
    [InlineData("1,234.56")]
    public void InvalidMoneyIsRejected(string input)
    {
        Assert.Throws<InvalidOperationException>(() => FinancialFormatting.ParseMoney(input, "valor"));
    }
}
