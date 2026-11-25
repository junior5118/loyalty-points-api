using System.Security.Cryptography;
using System.Text;

namespace LoyaltyPoints.Core;

public static class Cpf
{
    public static string OnlyDigits(string? value) =>
        new((value ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Validates length, repeated digits and both check digits.</summary>
    public static bool IsValid(string? value)
    {
        var d = OnlyDigits(value);
        if (d.Length != 11 || d.Distinct().Count() == 1)
            return false;

        return CheckDigit(d, 9) == d[9] - '0' && CheckDigit(d, 10) == d[10] - '0';
    }

    private static int CheckDigit(string digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (digits[i] - '0') * (length + 1 - i);

        var rest = sum * 10 % 11;
        return rest == 10 ? 0 : rest;
    }

    /// <summary>Keeps only the first 3 and last 2 digits: 123.***.***-09</summary>
    public static string Mask(string? value)
    {
        var d = OnlyDigits(value);
        return d.Length != 11 ? "***" : $"{d[..3]}.***.***-{d[9..]}";
    }

    /// <summary>
    /// Keyed hash (HMAC-SHA256) so the raw CPF never has to be stored.
    /// The key comes from configuration and is never committed.
    /// </summary>
    public static string Hash(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A hashing key is required.", nameof(key));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(OnlyDigits(value))));
    }
}
