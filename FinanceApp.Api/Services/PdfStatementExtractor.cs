using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace FinanceApp.Api.Services;

public sealed class PdfStatementExtractor
{
    private static readonly Regex TransactionLine = new(
        @"^\s*(?<date>\d{2}/\d{2}(?:/\d{2,4})?)\s+(?<description>.+?)\s+(?<amount>-?\s*(?:R\$\s*)?\d[\d.]*,\d{2})\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<PdfTransactionSuggestion> Extract(byte[] pdfContent, DateTime referenceMonth)
    {
        using var document = PdfDocument.Open(pdfContent);
        var suggestions = new List<PdfTransactionSuggestion>();
        var index = 0;

        foreach (var page in document.GetPages())
        {
            foreach (var line in page.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var match = TransactionLine.Match(line);
                if (!match.Success) continue;
                if (!TryParseDate(match.Groups["date"].Value, referenceMonth, out var date)) continue;
                if (!decimal.TryParse(match.Groups["amount"].Value.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(".", string.Empty).Replace(',', '.').Trim(), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) continue;
                if (amount == 0 || string.IsNullOrWhiteSpace(match.Groups["description"].Value)) continue;

                suggestions.Add(new PdfTransactionSuggestion(++index, date, match.Groups["description"].Value.Trim(), Math.Abs(amount), amount < 0 ? 0 : 1));
            }
        }

        return suggestions;
    }

    private static bool TryParseDate(string value, DateTime referenceMonth, out DateTime date)
    {
        var parts = value.Split('/');
        var year = parts.Length == 3 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : referenceMonth.Year;
        if (year < 100) year += 2000;
        return DateTime.TryParseExact($"{parts[0]}/{parts[1]}/{year}", "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}

public sealed record PdfTransactionSuggestion(int Row, DateTime Date, string Description, decimal Amount, int Type);
