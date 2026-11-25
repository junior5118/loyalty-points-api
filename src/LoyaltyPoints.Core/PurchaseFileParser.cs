using System.Globalization;

namespace LoyaltyPoints.Core;

public sealed record PurchaseLine(string CpfDigits, decimal Amount);

/// <summary>Errors never echo the CPF, so logs stay free of personal data.</summary>
public sealed record ParseError(int LineNumber, string Reason);

public sealed record ParseResult(IReadOnlyList<PurchaseLine> Lines, IReadOnlyList<ParseError> Errors);

/// <summary>Parses a plain-text export: one purchase per line, formatted as "cpf;amount".</summary>
public static class PurchaseFileParser
{
    public static ParseResult Parse(IEnumerable<string> lines)
    {
        var ok = new List<PurchaseLine>();
        var errors = new List<ParseError>();
        var number = 0;

        foreach (var raw in lines)
        {
            number++;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var parts = raw.Split(';');
            if (parts.Length != 2)
            {
                errors.Add(new ParseError(number, "Expected 'cpf;amount'."));
                continue;
            }

            if (!Cpf.IsValid(parts[0]))
            {
                errors.Add(new ParseError(number, "Invalid CPF."));
                continue;
            }

            var amountText = parts[1].Trim().Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                errors.Add(new ParseError(number, "Invalid amount."));
                continue;
            }

            ok.Add(new PurchaseLine(Cpf.OnlyDigits(parts[0]), amount));
        }

        return new ParseResult(ok, errors);
    }
}
