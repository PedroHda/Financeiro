using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;
using Microsoft.VisualBasic.FileIO;
using OfxSharp;

namespace FinanceiroApp.Services;

public static class StatementReader
{
    public const int MaxFileBytes = 10 * 1024 * 1024;

    public static List<ImportEntry> Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxFileBytes) throw new InvalidOperationException("Selecione um extrato com ate 10 MB.");
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
        text = text.TrimStart('\uFEFF');
        return Path.GetExtension(path).Equals(".ofx", StringComparison.OrdinalIgnoreCase) || text.Contains("<OFX", StringComparison.OrdinalIgnoreCase)
            ? ReadOfx(text) : ReadCsv(text);
    }

    public static List<ImportEntry> ReadCsv(string text)
    {
        GuardLength(text);
        using var probe = new StringReader(text);
        var first = probe.ReadLine() ?? "";
        var delimiter = first.Contains(';') ? ";" : first.Contains('\t') ? "\t" : ",";
        using var parser = new TextFieldParser(new StringReader(text)) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(delimiter);
        var header = parser.ReadFields() ?? throw new InvalidOperationException("O CSV esta vazio.");
        int Find(params string[] names) => Array.FindIndex(header, h => names.Contains(h.Trim(), StringComparer.OrdinalIgnoreCase));
        var date = Find("Data", "Date", "Data de lancamento");
        var description = Find("Descricao", "Descrição", "Description", "Historico", "Histórico");
        var amount = Find("Valor", "Amount");
        var identity = Find("Id", "FITID", "Identificador");
        if (date < 0 || description < 0 || amount < 0) throw new InvalidOperationException("O CSV precisa dos cabecalhos Data, Descricao e Valor. Valores negativos sao despesas.");
        var entries = new List<ImportEntry>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields()!;
            if (fields.Length != header.Length) throw new InvalidOperationException($"Quantidade de colunas invalida na linha {parser.LineNumber - 1}.");
            if (!DateTime.TryParseExact(fields[date], ["dd/MM/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var posted))
                throw new InvalidOperationException($"Data invalida na linha {parser.LineNumber - 1}. Use DD/MM/AAAA ou AAAA-MM-DD.");
            var value = FinancialFormatting.ParseMoney(fields[amount], "valor do extrato");
            if (value == 0) continue;
            entries.Add(new ImportEntry { Date = posted.Date, Description = fields[description], Amount = value, SourceId = identity >= 0 && fields[identity].Length > 0 ? "csv:" + fields[identity] : "" });
            if (entries.Count > 10000) throw new InvalidOperationException("Importe ate 10.000 movimentos por arquivo.");
        }
        Validate(entries);
        return entries;
    }

    public static List<ImportEntry> ReadOfx(string text)
    {
        GuardLength(text);
        if (text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || text.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O extrato nao pode conter definicoes de entidades ou documentos externos.");
        var entries = new List<ImportEntry>();
        if (text.Contains("OFXHEADER:100", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var input = new StringReader(text);
                var document = OfxDocumentReader.FromSgmlFile(input);
                if (document.Statements.Count != 1) throw new InvalidOperationException("Selecione um OFX de uma unica conta.");
                var statement = document.Statements[0];
                if (!string.Equals(statement.DefaultCurrency, "BRL", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Somente extratos em BRL sao aceitos.");
                foreach (var item in statement.Transactions)
                {
                    if (!string.IsNullOrEmpty(item.IncorrectTransactionId)) throw new InvalidOperationException("O OFX possui correcoes de lancamentos. Concilie essas correcoes antes de importar.");
                    if (item.Amount == 0) continue;
                    entries.Add(new ImportEntry { Date = item.Date?.Date ?? throw new InvalidOperationException("Movimento sem data."), Amount = item.Amount, Description = string.Join(" - ", new[] { item.Name, item.Memo }.Where(s => !string.IsNullOrWhiteSpace(s))), SourceId = "ofx:" + item.TransactionId });
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            { throw new InvalidOperationException("Nao foi possivel ler este OFX. Confira o arquivo ou exporte o extrato em CSV.", ex); }
        }
        else
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxFileBytes });
            var document = XDocument.Load(reader);
            if (!string.Equals(document.Root?.Name.LocalName, "OFX", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Arquivo OFX invalido.");
            var statements = document.Descendants().Where(e => e.Name.LocalName is "STMTRS" or "CCSTMTRS").ToArray();
            if (statements.Length != 1) throw new InvalidOperationException("Selecione um OFX de uma unica conta.");
            static string Field(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
            if (Field(statements[0], "CURDEF") != "BRL") throw new InvalidOperationException("Somente extratos em BRL sao aceitos.");
            foreach (var item in statements[0].Descendants().Where(e => e.Name.LocalName == "STMTTRN"))
            {
                if (Field(item, "CORRECTFITID").Length > 0) throw new InvalidOperationException("O OFX possui correcoes de lancamentos; revise-as antes de importar.");
                var date = Field(item, "DTPOSTED");
                if (date.Length < 8 || !DateTime.TryParseExact(date[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var posted)) throw new InvalidOperationException("Data invalida no OFX.");
                if (!decimal.TryParse(Field(item, "TRNAMT"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new InvalidOperationException("Valor invalido no OFX.");
                if (value == 0) continue;
                var fitId = Field(item, "FITID");
                if (fitId.Length == 0) throw new InvalidOperationException("Movimento OFX sem identificador FITID.");
                entries.Add(new ImportEntry { Date = posted, Amount = value, SourceId = "ofx:" + fitId, Description = string.Join(" - ", new[] { Field(item, "NAME"), Field(item, "MEMO") }.Where(s => s.Length > 0)) });
            }
        }
        Validate(entries);
        return entries;
    }

    private static void GuardLength(string text)
    {
        if (text.Length > MaxFileBytes) throw new InvalidOperationException("O extrato deve ter ate 10 MB.");
    }

    private static void Validate(List<ImportEntry> entries)
    {
        if (entries.Count == 0) throw new InvalidOperationException("O arquivo nao possui movimentos para importar.");
        if (entries.Count > 10000) throw new InvalidOperationException("Importe ate 10.000 movimentos por arquivo.");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Description)) entry.Description = "Movimento do extrato";
            if (entry.Description.Length > 500 || entry.SourceId.Length > 300) throw new InvalidOperationException("O extrato possui campos muito longos.");
            _ = Data.MoneyStorage.ToCents(entry.Amount);
        }
    }
}
