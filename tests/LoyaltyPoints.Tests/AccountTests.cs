using LoyaltyPoints.Core;
using Xunit;

namespace LoyaltyPoints.Tests;

public class AccountTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PointsCalculator Calc = new(new PointsRules());

    [Fact]
    public void Purchase_before_activation_is_pending_not_spendable()
    {
        var account = new Account();

        account.RegisterPurchase(100m, Calc, Now);

        Assert.Equal(10, account.PendingPoints);
        Assert.Equal(0, account.Balance);
    }

    [Fact]
    public void Activation_releases_pending_points_to_the_balance()
    {
        var account = new Account();
        account.RegisterPurchase(100m, Calc, Now);
        account.RegisterPurchase(50m, Calc, Now);

        var released = account.Activate(Now);

        Assert.Equal(15, released);
        Assert.Equal(15, account.Balance);
        Assert.Equal(0, account.PendingPoints);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Activating_twice_does_not_release_points_twice()
    {
        var account = new Account();
        account.RegisterPurchase(100m, Calc, Now);
        account.Activate(Now);

        Assert.Equal(0, account.Activate(Now));
        Assert.Equal(10, account.Balance);
    }

    [Fact]
    public void Purchase_after_activation_goes_straight_to_the_balance()
    {
        var account = new Account();
        account.Activate(Now);

        account.RegisterPurchase(100m, Calc, Now);

        Assert.Equal(10, account.Balance);
        Assert.Equal(0, account.PendingPoints);
    }

    [Fact]
    public void Redeem_requires_an_active_account()
    {
        var account = new Account();
        account.RegisterPurchase(1000m, Calc, Now);

        Assert.Throws<DomainException>(() => account.Redeem(10, Now));
    }

    [Fact]
    public void Redeem_debits_points_and_returns_a_voucher()
    {
        var account = new Account();
        account.Activate(Now);
        account.RegisterPurchase(1000m, Calc, Now); // 100 pts x2 bonus = 200

        var redemption = account.Redeem(100, Now);

        Assert.Equal(100, account.Balance);
        Assert.Equal(10m, redemption.DiscountPercent);
        Assert.Equal(10, redemption.VoucherCode.Length);
    }

    [Fact]
    public void Redeem_rejects_more_than_the_balance()
    {
        var account = new Account();
        account.Activate(Now);
        account.RegisterPurchase(100m, Calc, Now);

        Assert.Throws<DomainException>(() => account.Redeem(50, Now));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1000)]
    public void Redeem_rejects_out_of_range_points(int points)
    {
        var account = new Account { IsActive = true, Balance = 5000 };

        Assert.Throws<DomainException>(() => account.Redeem(points, Now));
    }

    [Fact]
    public void Discount_never_reaches_100_percent()
    {
        var account = new Account { IsActive = true, Balance = 5000 };

        var redemption = account.Redeem(Account.MaxPointsPerRedemption, Now);

        Assert.True(redemption.DiscountPercent <= Account.MaxDiscountPercent);
    }

    [Fact]
    public void Ledger_keeps_a_record_of_every_movement()
    {
        var account = new Account();
        account.RegisterPurchase(100m, Calc, Now);
        account.Activate(Now);
        account.Redeem(10, Now);

        Assert.Equal(
            new[] { LedgerKind.PointsPending, LedgerKind.PointsCredited, LedgerKind.PointsRedeemed },
            account.Ledger.Select(l => l.Kind).ToArray());
    }
}
