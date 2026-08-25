using Finances.Application.Credits.Calculations;
using Finances.Domain.Entities;
using Xunit;

namespace Finances.Application.Tests;

/// <summary>
/// Tests for <see cref="AmortizationCalculator.Simulate"/>, focused on extra principal
/// prepayments (abonos a capital) and their two effects: reduce term vs reduce installment.
/// A 0% interest, 12-month, $1,200 loan (installment $100) keeps the arithmetic obvious.
/// </summary>
public class PrepaymentSimulationTests
{
    private static readonly DateTime Start = new(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private static PaymentEvent Installment(int monthsAfter, decimal amount) =>
        new(Start.AddMonths(monthsAfter), amount, false, PrepaymentEffect.ReduceTerm);

    private static PaymentEvent Prepayment(int monthsAfter, decimal amount, PrepaymentEffect effect) =>
        new(Start.AddMonths(monthsAfter), amount, true, effect);

    private static CreditSimulation Run(IReadOnlyList<PaymentEvent> payments, decimal annualRate = 0m) =>
        AmortizationCalculator.Simulate(1200m, annualRate, 12, InterestModel.CompoundFrench, Start, payments);

    [Fact]
    public void NoPrepayments_MatchesClassicProgress()
    {
        var plan = AmortizationCalculator.BuildPlan(1200m, 6m, 12, InterestModel.CompoundFrench);
        var classic = AmortizationCalculator.ComputeProgress(plan, 1200m, plan.MonthlyInstallment * 2);

        var sim = AmortizationCalculator.Simulate(
            1200m, 6m, 12, InterestModel.CompoundFrench, Start,
            new[] { Installment(1, plan.MonthlyInstallment), Installment(2, plan.MonthlyInstallment) });

        Assert.Equal(classic.OutstandingPrincipal, sim.OutstandingPrincipal, 2);
        Assert.Equal(classic.InterestPaid, sim.InterestPaid, 2);
        Assert.Equal(classic.PrincipalPaid, sim.PrincipalPaid, 2);
        Assert.Equal(classic.InstallmentsCovered, sim.InstallmentsCovered);
        Assert.Equal(0m, sim.PrepaidPrincipal);
    }

    [Fact]
    public void Prepayment_GoesEntirelyToPrincipal()
    {
        // 3 installments ($300) then a $300 abono a capital.
        var sim = Run(new[]
        {
            Installment(1, 100m),
            Installment(2, 100m),
            Installment(3, 100m),
            Prepayment(4, 300m, PrepaymentEffect.ReduceTerm),
        });

        // 3*100 principal + 300 abono = 600 principal repaid, so 600 remains.
        Assert.Equal(600m, sim.OutstandingPrincipal);
        Assert.Equal(300m, sim.PrepaidPrincipal);
        Assert.Equal(600m, sim.PrincipalPaid);
        Assert.Equal(0m, sim.InterestPaid);
    }

    [Fact]
    public void ReduceTerm_KeepsInstallmentAndShortensPlan()
    {
        var sim = Run(new[]
        {
            Installment(1, 100m),
            Installment(2, 100m),
            Installment(3, 100m),
            Prepayment(4, 300m, PrepaymentEffect.ReduceTerm),
        });

        Assert.Equal(600m, sim.OutstandingPrincipal);
        Assert.Equal(100m, sim.CurrentInstallment);       // installment unchanged
        Assert.Equal(6, sim.RemainingInstallments);       // 600 / 100 = 6 left (instead of 8)
        Assert.Equal(3, sim.InstallmentsCovered);
        Assert.False(sim.IsPaidOff);
    }

    [Fact]
    public void ReduceInstallment_LowersInstallmentAndKeepsTerm()
    {
        var sim = Run(new[]
        {
            Installment(1, 100m),
            Installment(2, 100m),
            Installment(3, 100m),
            Prepayment(4, 300m, PrepaymentEffect.ReduceInstallment),
        });

        // 12-month term, 4 months elapsed -> 8 periods remain: 600 / 8 = 75.
        Assert.Equal(600m, sim.OutstandingPrincipal);
        Assert.Equal(75m, sim.CurrentInstallment);
        Assert.Equal(8, sim.RemainingInstallments);
        Assert.False(sim.IsPaidOff);
    }

    [Fact]
    public void LargePrepayment_PaysOffTheCredit()
    {
        var sim = Run(new[]
        {
            Installment(1, 100m),
            Prepayment(2, 5000m, PrepaymentEffect.ReduceTerm),
        });

        Assert.Equal(0m, sim.OutstandingPrincipal);
        Assert.True(sim.IsPaidOff);
        Assert.Equal(0, sim.RemainingInstallments);
        Assert.Equal(0m, sim.RemainingTotal);
    }

    [Fact]
    public void ReduceTerm_SavesMoreInterestThanReduceInstallment()
    {
        var reduceTerm = Run(new[]
        {
            Installment(1, 100m),
            Prepayment(2, 400m, PrepaymentEffect.ReduceTerm),
        }, annualRate: 24m);

        var reduceInstallment = Run(new[]
        {
            Installment(1, 100m),
            Prepayment(2, 400m, PrepaymentEffect.ReduceInstallment),
        }, annualRate: 24m);

        // Both clear the same principal now, but finishing earlier avoids more interest.
        Assert.True(reduceTerm.TotalInterest <= reduceInstallment.TotalInterest);
    }

    [Fact]
    public void RecalculatedSchedule_RepaysWholePrincipal()
    {
        var sim = Run(new[]
        {
            Installment(1, 100m),
            Prepayment(2, 300m, PrepaymentEffect.ReduceTerm),
        });

        var totalPrincipal = sim.Schedule.Sum(r => r.Principal);
        Assert.Equal(1200m, totalPrincipal, 2);
        Assert.Equal(0m, sim.Schedule[^1].RemainingBalance);
        // Reduce-term must not exceed the original term.
        Assert.True(sim.Schedule.Count <= 12);
    }
}
