namespace LoyaltyPoints.Core;

public enum LedgerKind
{
    PointsPending,
    PointsCredited,
    PointsRedeemed
}

public class LedgerEntry
{
    public int Id { get; set; }
    public Guid AccountId { get; set; }
    public LedgerKind Kind { get; set; }
    public int Points { get; set; }
    public decimal? Amount { get; set; }
    public DateTime At { get; set; }
}

public sealed record Redemption(string VoucherCode, int PointsSpent, decimal DiscountPercent);

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>
/// A loyalty account. Key business rule: points earned BEFORE the account is
/// activated stay "pending". They show up in the data, but the customer cannot
/// spend them until the account is activated (then they move to the balance).
/// </summary>
public class Account
{
    public const int PointsPerPercent = 10;
    public const int MaxPointsPerRedemption = 999;
    public const decimal MaxDiscountPercent = 99.99m;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string CpfHash { get; set; } = "";
    public string CpfMasked { get; set; } = "";
    public bool IsActive { get; set; }
    public int PendingPoints { get; set; }
    public int Balance { get; set; }
    public List<LedgerEntry> Ledger { get; set; } = new();

    public int RegisterPurchase(decimal amount, PointsCalculator calculator, DateTime now)
    {
        if (amount <= 0)
            throw new DomainException("Purchase amount must be greater than zero.");

        var points = calculator.Calculate(amount);
        if (points == 0)
            return 0;

        if (IsActive)
        {
            Balance += points;
            Ledger.Add(new LedgerEntry { AccountId = Id, Kind = LedgerKind.PointsCredited, Points = points, Amount = amount, At = now });
        }
        else
        {
            PendingPoints += points;
            Ledger.Add(new LedgerEntry { AccountId = Id, Kind = LedgerKind.PointsPending, Points = points, Amount = amount, At = now });
        }

        return points;
    }

    /// <summary>Activates the account and releases pending points. Returns how many points were released.</summary>
    public int Activate(DateTime now)
    {
        if (IsActive)
            return 0;

        IsActive = true;
        var released = PendingPoints;
        Balance += released;
        PendingPoints = 0;

        if (released > 0)
            Ledger.Add(new LedgerEntry { AccountId = Id, Kind = LedgerKind.PointsCredited, Points = released, At = now });

        return released;
    }

    public Redemption Redeem(int points, DateTime now)
    {
        if (!IsActive)
            throw new DomainException("The account must be activated before points can be redeemed.");
        if (points < PointsPerPercent)
            throw new DomainException($"Minimum redemption is {PointsPerPercent} points.");
        if (points > MaxPointsPerRedemption)
            throw new DomainException($"Maximum redemption per request is {MaxPointsPerRedemption} points.");
        if (points > Balance)
            throw new DomainException("Insufficient balance.");

        var percent = Math.Min(points / (decimal)PointsPerPercent, MaxDiscountPercent);
        Balance -= points;
        Ledger.Add(new LedgerEntry { AccountId = Id, Kind = LedgerKind.PointsRedeemed, Points = -points, At = now });

        var code = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return new Redemption(code, points, percent);
    }
}
