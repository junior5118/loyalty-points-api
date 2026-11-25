using LoyaltyPoints.Core;
using Xunit;

namespace LoyaltyPoints.Tests;

public class PointsCalculatorTests
{
    private static readonly PointsCalculator Calc = new(new PointsRules());

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9.99, 0)]
    [InlineData(10, 1)]
    [InlineData(199.99, 19)]
    [InlineData(200, 40)]   // bonus kicks in at the threshold
    [InlineData(1000, 200)]
    public void Calculates_points_with_bonus(double amount, int expected)
    {
        Assert.Equal(expected, Calc.Calculate((decimal)amount));
    }

    [Fact]
    public void Negative_amounts_earn_nothing()
    {
        Assert.Equal(0, Calc.Calculate(-50m));
    }
}

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("111.111.111-11", false)]   // repeated digits
    [InlineData("529.982.247-26", false)]   // wrong check digit
    [InlineData("123", false)]
    [InlineData("", false)]
    public void Validates_check_digits(string value, bool expected)
    {
        Assert.Equal(expected, Cpf.IsValid(value));
    }

    [Fact]
    public void Mask_hides_the_middle_digits()
    {
        Assert.Equal("529.***.***-25", Cpf.Mask("52998224725"));
    }

    [Fact]
    public void Hash_is_stable_for_the_same_key_and_ignores_formatting()
    {
        Assert.Equal(Cpf.Hash("529.982.247-25", "k1"), Cpf.Hash("52998224725", "k1"));
    }

    [Fact]
    public void Hash_changes_with_the_key()
    {
        Assert.NotEqual(Cpf.Hash("52998224725", "k1"), Cpf.Hash("52998224725", "k2"));
    }

    [Fact]
    public void Hash_requires_a_key()
    {
        Assert.Throws<ArgumentException>(() => Cpf.Hash("52998224725", ""));
    }
}

public class PurchaseFileParserTests
{
    [Fact]
    public void Parses_valid_lines_and_reports_errors_without_echoing_cpf()
    {
        var result = PurchaseFileParser.Parse(new[]
        {
            "529.982.247-25;120,50",
            "",
            "111.111.111-11;10",
            "52998224725;abc",
            "only-one-field",
            "52998224725;99.90"
        });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(120.50m, result.Lines[0].Amount);
        Assert.Equal("52998224725", result.Lines[0].CpfDigits);
        Assert.Equal(3, result.Errors.Count);
        Assert.All(result.Errors, e => Assert.DoesNotContain("529", e.Reason));
        Assert.Equal(new[] { 3, 4, 5 }, result.Errors.Select(e => e.LineNumber).ToArray());
    }
}
