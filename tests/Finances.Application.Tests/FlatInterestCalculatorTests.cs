using Finances.Application.Credits.Calculations;
using Finances.Domain.Entities;
using Xunit;

namespace Finances.Application.Tests;

public class FlatInterestCalculatorTests
{
    // The user's original example is a FLAT credit: $1000, 24 months, 2.3% annual
    // => interest 1000 * 0.023 * 2 = $46, total $1046, installment $43.58.
    private const decimal Principal = 1000m;
    private const decimal AnnualRate = 2.3m;
    private const int Term = 24;

    private static AmortizationPlan Flat() =>
        AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term, InterestModel.SimpleFlat);

    [Fact]
    public void Flat_TotalInterest_IsSimpleInterestOnOriginalPrincipal()
    {
        Assert.Equal(46m, Flat().TotalInterest, 2);
    }

    [Fact]
    public void Flat_TotalToPay_IsPrincipalPlusFlatInterest()
    {
        Assert.Equal(1046m, Flat().TotalToPay, 2);
    }

    [Fact]
    public void Flat_MonthlyInstallment_IsTotalDividedByTerm()
    {
        Assert.Equal(43.58m, Flat().MonthlyInstallment, 2);
    }

    [Fact]
    public void Flat_ProducesOneRowPerMonth()
    {
        Assert.Equal(Term, Flat().Schedule.Count);
    }

    [Fact]
    public void Flat_PrincipalPortionsSumToPrincipal()
    {
        Assert.Equal(Principal, Flat().Schedule.Sum(r => r.Principal), 2);
    }

    [Fact]
    public void Flat_InterestPortionsSumToTotalInterest()
    {
        Assert.Equal(46m, Flat().Schedule.Sum(r => r.Interest), 2);
    }

    [Fact]
    public void Flat_LastRow_LeavesZeroBalance()
    {
        Assert.Equal(0m, Flat().Schedule[^1].RemainingBalance);
    }

    [Fact]
    public void Flat_CostsMoreThanFrench_ForSameNominalRate()
    {
        var french = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term, InterestModel.CompoundFrench);
        var flat = Flat();
        Assert.True(flat.TotalInterest > french.TotalInterest);
    }

    [Fact]
    public void Flat_EffectiveAnnualRate_IsHigherThanNominal()
    {
        var flat = Flat();
        Assert.True(flat.EffectiveAnnualRatePercent > AnnualRate);
    }

    [Fact]
    public void Flat_ZeroInterest_SplitsPrincipalEvenlyAndZeroApr()
    {
        var plan = AmortizationCalculator.BuildPlan(1200m, 0m, 12, InterestModel.SimpleFlat);
        Assert.Equal(100m, plan.MonthlyInstallment);
        Assert.Equal(1200m, plan.TotalToPay);
        Assert.Equal(0m, plan.TotalInterest);
        Assert.Equal(0m, plan.EffectiveAnnualRatePercent);
        Assert.All(plan.Schedule, r => Assert.Equal(100m, r.Principal));
    }

    [Fact]
    public void Flat_ComputeProgress_HalfwayThrough_RefundsAboutHalfTheInterest()
    {
        var plan = Flat();
        // Pay the first 12 of 24 even installments.
        var paid = plan.Schedule.Take(12).Sum(r => r.Installment);
        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, paid);

        Assert.Equal(12, progress.InstallmentsCovered);
        // Actuarial rebate: roughly half the interest is still unearned and thus saved.
        Assert.Equal(23m, progress.SavingsIfPaidOffToday, 1);
        Assert.False(progress.IsPaidOff);
    }

    [Fact]
    public void Flat_ComputeProgress_PayEverything_IsPaidOff()
    {
        var plan = Flat();
        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, plan.TotalToPay);

        Assert.Equal(0m, progress.OutstandingPrincipal);
        Assert.Equal(0m, progress.SavingsIfPaidOffToday);
        Assert.True(progress.IsPaidOff);
    }
}
