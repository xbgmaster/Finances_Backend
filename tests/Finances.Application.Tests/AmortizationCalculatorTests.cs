using Finances.Application.Credits.Calculations;
using Xunit;

namespace Finances.Application.Tests;

public class AmortizationCalculatorTests
{
    // The user's example: $1000, 24 months, 2.3% ANNUAL.
    private const decimal Principal = 1000m;
    private const decimal AnnualRate = 2.3m;
    private const int Term = 24;

    [Fact]
    public void BuildPlan_ProducesOneRowPerMonth()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        Assert.Equal(Term, plan.Schedule.Count);
    }

    [Fact]
    public void BuildPlan_MonthlyInstallment_MatchesFrenchFormula()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        // Expected ~42.67 for these terms.
        Assert.Equal(42.67m, plan.MonthlyInstallment, 2);
    }

    [Fact]
    public void BuildPlan_LastRow_LeavesZeroBalance()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        Assert.Equal(0m, plan.Schedule[^1].RemainingBalance);
    }

    [Fact]
    public void BuildPlan_PrincipalPortionsSumToPrincipal()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        var totalPrincipal = plan.Schedule.Sum(r => r.Principal);
        Assert.Equal(Principal, totalPrincipal, 2);
    }

    [Fact]
    public void BuildPlan_TotalToPay_EqualsPrincipalPlusInterest()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        Assert.Equal(plan.TotalToPay, Principal + plan.TotalInterest, 2);
        Assert.True(plan.TotalInterest > 0m);
    }

    [Fact]
    public void BuildPlan_ZeroInterest_SplitsPrincipalEvenly()
    {
        var plan = AmortizationCalculator.BuildPlan(1200m, 0m, 12);
        Assert.Equal(100m, plan.MonthlyInstallment);
        Assert.Equal(1200m, plan.TotalToPay);
        Assert.Equal(0m, plan.TotalInterest);
        Assert.All(plan.Schedule, r => Assert.Equal(100m, r.Principal));
    }

    [Fact]
    public void ComputeProgress_NoPayments_OwesFullPrincipalAndSavesAllInterest()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, totalPaid: 0m);

        Assert.Equal(Principal, progress.OutstandingPrincipal);
        Assert.Equal(0m, progress.TotalPaid);
        Assert.Equal(0, progress.InstallmentsCovered);
        Assert.Equal(plan.TotalInterest, progress.SavingsIfPaidOffToday, 2);
        Assert.False(progress.IsPaidOff);
    }

    [Fact]
    public void ComputeProgress_PayFirstInstallment_ReducesBalanceByFirstPrincipalPortion()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        var first = plan.Schedule[0];

        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, totalPaid: first.Installment);

        Assert.Equal(1, progress.InstallmentsCovered);
        Assert.Equal(first.Interest, progress.InterestPaid, 2);
        Assert.Equal(first.Principal, progress.PrincipalPaid, 2);
        Assert.Equal(first.RemainingBalance, progress.OutstandingPrincipal, 2);
    }

    [Fact]
    public void ComputeProgress_PayEverything_IsPaidOffWithNoSavingsLeft()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, totalPaid: plan.TotalToPay);

        Assert.Equal(0m, progress.OutstandingPrincipal);
        Assert.Equal(0m, progress.RemainingTotal);
        Assert.Equal(0m, progress.SavingsIfPaidOffToday);
        Assert.True(progress.IsPaidOff);
    }

    [Fact]
    public void ComputeProgress_Overpayment_IsClampedAndPaidOff()
    {
        var plan = AmortizationCalculator.BuildPlan(Principal, AnnualRate, Term);
        var progress = AmortizationCalculator.ComputeProgress(plan, Principal, totalPaid: plan.TotalToPay + 500m);

        Assert.Equal(0m, progress.OutstandingPrincipal);
        Assert.Equal(0m, progress.RemainingTotal);
        Assert.True(progress.IsPaidOff);
    }
}
