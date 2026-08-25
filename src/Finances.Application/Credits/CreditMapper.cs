using Finances.Application.Credits.Calculations;
using Finances.Application.Dtos;
using Finances.Domain.Entities;

namespace Finances.Application.Credits;

/// <summary>
/// Maps a <see cref="Credit"/> plus the total amount paid into the derived DTOs.
/// All balances/progress are computed here via the amortization calculator, never
/// read from stored fields.
/// </summary>
public static class CreditMapper
{
    public static CreditSummaryDto ToSummary(Credit credit, decimal totalPaid, DateTime asOf)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths);
        var progress = AmortizationCalculator.ComputeProgress(plan, credit.Principal, totalPaid);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(progress.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        return new CreditSummaryDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            MonthlyInterestRate: Math.Round(plan.MonthlyRate * 100m, 4, MidpointRounding.AwayFromZero),
            TermMonths: credit.TermMonths,
            StartDate: credit.StartDate,
            MonthlyInstallment: plan.MonthlyInstallment,
            TotalToPay: plan.TotalToPay,
            TotalInterest: plan.TotalInterest,
            TotalPaid: progress.TotalPaid,
            PrincipalPaid: progress.PrincipalPaid,
            InterestPaid: progress.InterestPaid,
            OutstandingPrincipal: progress.OutstandingPrincipal,
            RemainingTotal: progress.RemainingTotal,
            SavingsIfPaidOffToday: progress.SavingsIfPaidOffToday,
            InstallmentsCovered: progress.InstallmentsCovered,
            InstallmentsRemaining: Math.Max(credit.TermMonths - progress.InstallmentsCovered, 0),
            ExpectedInstallmentsToDate: ExpectedInstallmentsToDate(credit, asOf),
            ProgressPercent: progressPercent,
            Status: progress.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditDto ToListItem(Credit credit, decimal totalPaid)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths);
        var progress = AmortizationCalculator.ComputeProgress(plan, credit.Principal, totalPaid);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(progress.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        return new CreditDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            TermMonths: credit.TermMonths,
            StartDate: credit.StartDate,
            MonthlyInstallment: plan.MonthlyInstallment,
            TotalToPay: plan.TotalToPay,
            TotalPaid: progress.TotalPaid,
            OutstandingPrincipal: progress.OutstandingPrincipal,
            ProgressPercent: progressPercent,
            Status: progress.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditScheduleDto ToSchedule(Credit credit)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths);
        var rows = plan.Schedule
            .Select(r => new AmortizationRowDto(
                r.Number,
                credit.StartDate.AddMonths(r.Number),
                r.Installment,
                r.Interest,
                r.Principal,
                r.RemainingBalance))
            .ToList();
        return new CreditScheduleDto(credit.Id, rows);
    }

    private static int ExpectedInstallmentsToDate(Credit credit, DateTime asOf)
    {
        if (asOf <= credit.StartDate) return 0;
        var months = ((asOf.Year - credit.StartDate.Year) * 12) + asOf.Month - credit.StartDate.Month;
        if (asOf.Day < credit.StartDate.Day) months--;
        return Math.Clamp(months, 0, credit.TermMonths);
    }
}
