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
    /// <summary>How many days before the due date we start warning "your installment is due soon".</summary>
    private const int DueSoonThresholdDays = 3;

    public static CreditSummaryDto ToSummary(Credit credit, decimal totalPaid, DateTime asOf)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel);
        var progress = AmortizationCalculator.ComputeProgress(plan, credit.Principal, totalPaid);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(progress.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        // Early payoff: an optional penalty is charged on the amount being prepaid
        // (the outstanding principal). Net savings = interest avoided minus that penalty.
        var penaltyAmount = Math.Round(
            progress.OutstandingPrincipal * (credit.PrepaymentPenaltyRate / 100m),
            2, MidpointRounding.AwayFromZero);
        var netSavings = Math.Max(progress.SavingsIfPaidOffToday - penaltyAmount, 0m);
        var payoffToday = Math.Round(progress.OutstandingPrincipal + penaltyAmount, 2, MidpointRounding.AwayFromZero);

        var due = ComputeDueStatus(credit, progress.InstallmentsCovered, progress.IsPaidOff, asOf);

        return new CreditSummaryDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            InterestModel: credit.InterestModel.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            MonthlyInterestRate: Math.Round(plan.MonthlyRate * 100m, 4, MidpointRounding.AwayFromZero),
            EffectiveAnnualRate: plan.EffectiveAnnualRatePercent,
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
            PrepaymentPenaltyRate: credit.PrepaymentPenaltyRate,
            PrepaymentPenaltyAmount: penaltyAmount,
            NetSavingsIfPaidOffToday: netSavings,
            PayoffAmountToday: payoffToday,
            PrepaymentEffect: credit.PrepaymentEffect.ToString(),
            InstallmentsCovered: progress.InstallmentsCovered,
            InstallmentsRemaining: Math.Max(credit.TermMonths - progress.InstallmentsCovered, 0),
            ExpectedInstallmentsToDate: ExpectedInstallmentsToDate(credit, asOf),
            ProgressPercent: progressPercent,
            PaymentDueDay: credit.PaymentDueDay,
            NextDueDate: due.NextDueDate,
            DaysUntilDue: due.DaysUntilDue,
            IsOverdue: due.IsOverdue,
            IsDueSoon: due.IsDueSoon,
            AlertLevel: due.AlertLevel,
            Status: progress.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditDto ToListItem(Credit credit, decimal totalPaid, DateTime asOf)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel);
        var progress = AmortizationCalculator.ComputeProgress(plan, credit.Principal, totalPaid);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(progress.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        var due = ComputeDueStatus(credit, progress.InstallmentsCovered, progress.IsPaidOff, asOf);

        return new CreditDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            InterestModel: credit.InterestModel.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            TermMonths: credit.TermMonths,
            StartDate: credit.StartDate,
            PrepaymentPenaltyRate: credit.PrepaymentPenaltyRate,
            PrepaymentEffect: credit.PrepaymentEffect.ToString(),
            MonthlyInstallment: plan.MonthlyInstallment,
            TotalToPay: plan.TotalToPay,
            TotalPaid: progress.TotalPaid,
            OutstandingPrincipal: progress.OutstandingPrincipal,
            ProgressPercent: progressPercent,
            PaymentDueDay: credit.PaymentDueDay,
            NextDueDate: due.NextDueDate,
            DaysUntilDue: due.DaysUntilDue,
            IsOverdue: due.IsOverdue,
            IsDueSoon: due.IsDueSoon,
            AlertLevel: due.AlertLevel,
            Status: progress.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditScheduleDto ToSchedule(Credit credit)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel);
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

    /// <summary>
    /// Derives the payment alert for a credit: the next due date (based on the user's
    /// payment due day), how many days remain, and whether it is due soon or already
    /// overdue. Everything is derived from the terms + payments, nothing is stored.
    /// </summary>
    private static DueStatus ComputeDueStatus(Credit credit, int installmentsCovered, bool isPaidOff, DateTime asOf)
    {
        if (isPaidOff)
            return new DueStatus(credit.StartDate, 0, false, false, "PaidOff");

        // The next installment the user still owes. Its calendar due date decides the alert.
        var nextNumber = Math.Min(installmentsCovered + 1, credit.TermMonths);
        var nextDue = DueDateForInstallment(credit, nextNumber);

        var daysUntil = (int)Math.Floor((nextDue.Date - asOf.Date).TotalDays);
        var overdue = daysUntil < 0;
        var dueSoon = !overdue && daysUntil <= DueSoonThresholdDays;
        var level = overdue ? "Overdue" : dueSoon ? "DueSoon" : "Upcoming";

        return new DueStatus(nextDue, daysUntil, overdue, dueSoon, level);
    }

    /// <summary>
    /// Calendar due date of the k-th installment: the payment due day of the month that is
    /// k months after the start month, clamped to the month length (e.g. a due day of 31
    /// becomes Feb 28/29).
    /// </summary>
    private static DateTime DueDateForInstallment(Credit credit, int installmentNumber)
    {
        var baseMonth = new DateTime(credit.StartDate.Year, credit.StartDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(installmentNumber);
        var day = Math.Min(Math.Max(credit.PaymentDueDay, 1), DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month));
        return new DateTime(baseMonth.Year, baseMonth.Month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private readonly record struct DueStatus(
        DateTime NextDueDate,
        int DaysUntilDue,
        bool IsOverdue,
        bool IsDueSoon,
        string AlertLevel);
}
