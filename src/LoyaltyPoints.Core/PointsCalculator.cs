namespace LoyaltyPoints.Core;

public sealed record PointsRules
{
    /// <summary>How much the customer spends to earn 1 point.</summary>
    public decimal AmountPerPoint { get; init; } = 10m;

    /// <summary>Purchases at or above this amount earn bonus points.</summary>
    public decimal BonusThreshold { get; init; } = 200m;

    public int BonusMultiplier { get; init; } = 2;
}

public sealed class PointsCalculator
{
    private readonly PointsRules _rules;

    public PointsCalculator(PointsRules rules)
    {
        if (rules.AmountPerPoint <= 0)
            throw new ArgumentException("AmountPerPoint must be positive.", nameof(rules));
        _rules = rules;
    }

    public int Calculate(decimal amount)
    {
        if (amount <= 0)
            return 0;

        var basePoints = (int)Math.Floor(amount / _rules.AmountPerPoint);
        return amount >= _rules.BonusThreshold ? basePoints * _rules.BonusMultiplier : basePoints;
    }
}
