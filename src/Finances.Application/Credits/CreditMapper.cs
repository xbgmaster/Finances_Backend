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

    /// <summary>
    /// Monthly add-on charges (life insurance as a % of the given balance + a fixed fee).
    /// These are billed on top of the installment ("valor a pagar"), never to principal/interest.
    /// </summary>
    private static decimal MonthlyCharge(decimal balance, Credit credit)
    {
        var insurance = balance > 0m
            ? Math.Round(balance * (credit.MonthlyInsuranceRate / 100m), 2, MidpointRounding.AwayFromZero)
            : 0m;
        return Math.Round(insurance + credit.MonthlyFixedFee, 2, MidpointRounding.AwayFromZero);
    }

    public static CreditSummaryDto ToSummary(Credit credit, IReadOnlyList<CreditPayment> payments, DateTime asOf)
    {
        var plan = AmortizationCalculator.BuildPlan(credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel, credit.RateConvention);
        var sim = AmortizationCalculator.Simulate(
            credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel,
            credit.StartDate, ToEvents(credit, payments), credit.RateConvention);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(sim.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        // Early payoff: an optional penalty is charged on the amount being prepaid
        // (the outstanding principal). Net savings = interest avoided minus that penalty.
        var penaltyAmount = Math.Round(
            sim.OutstandingPrincipal * (credit.PrepaymentPenaltyRate / 100m),
            2, MidpointRounding.AwayFromZero);
        var netSavings = Math.Max(sim.SavingsIfPaidOffToday - penaltyAmount, 0m);
        var payoffToday = Math.Round(sim.OutstandingPrincipal + penaltyAmount, 2, MidpointRounding.AwayFromZero);

        var due = ComputeDueStatus(credit, sim.InstallmentsCovered, sim.IsPaidOff, asOf);

        // Add-on charges for the current outstanding balance (what the next "valor a pagar" carries).
        var monthlyCharges = sim.IsPaidOff ? 0m : MonthlyCharge(sim.OutstandingPrincipal, credit);
        var monthlyTotalDue = Math.Round(sim.CurrentInstallment + monthlyCharges, 2, MidpointRounding.AwayFromZero);

        return new CreditSummaryDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            InterestModel: credit.InterestModel.ToString(),
            RateConvention: credit.RateConvention.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            MonthlyInterestRate: Math.Round(plan.MonthlyRate * 100m, 4, MidpointRounding.AwayFromZero),
            EffectiveAnnualRate: plan.EffectiveAnnualRatePercent,
            TermMonths: credit.TermMonths,
            StartDate: credit.StartDate,
            MonthlyInstallment: sim.CurrentInstallment,
            MonthlyInsuranceRate: credit.MonthlyInsuranceRate,
            MonthlyFixedFee: credit.MonthlyFixedFee,
            MonthlyCharges: monthlyCharges,
            MonthlyTotalDue: monthlyTotalDue,
            TotalToPay: sim.TotalToPay,
            TotalInterest: sim.TotalInterest,
            TotalPaid: sim.TotalPaid,
            PrincipalPaid: sim.PrincipalPaid,
            InterestPaid: sim.InterestPaid,
            PrepaidPrincipal: sim.PrepaidPrincipal,
            OutstandingPrincipal: sim.OutstandingPrincipal,
            RemainingTotal: sim.RemainingTotal,
            SavingsIfPaidOffToday: sim.SavingsIfPaidOffToday,
            PrepaymentPenaltyRate: credit.PrepaymentPenaltyRate,
            PrepaymentPenaltyAmount: penaltyAmount,
            NetSavingsIfPaidOffToday: netSavings,
            PayoffAmountToday: payoffToday,
            PrepaymentEffect: credit.PrepaymentEffect.ToString(),
            InstallmentsCovered: sim.InstallmentsCovered,
            InstallmentsRemaining: sim.RemainingInstallments,
            ExpectedInstallmentsToDate: ExpectedInstallmentsToDate(credit, asOf),
            ProgressPercent: progressPercent,
            PaymentDueDay: credit.PaymentDueDay,
            NextDueDate: due.NextDueDate,
            DaysUntilDue: due.DaysUntilDue,
            IsOverdue: due.IsOverdue,
            IsDueSoon: due.IsDueSoon,
            AlertLevel: due.AlertLevel,
            Status: sim.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditDto ToListItem(Credit credit, IReadOnlyList<CreditPayment> payments, DateTime asOf)
    {
        var sim = AmortizationCalculator.Simulate(
            credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel,
            credit.StartDate, ToEvents(credit, payments), credit.RateConvention);

        var progressPercent = credit.Principal <= 0
            ? 0m
            : Math.Round(sim.PrincipalPaid / credit.Principal * 100m, 2, MidpointRounding.AwayFromZero);

        var due = ComputeDueStatus(credit, sim.InstallmentsCovered, sim.IsPaidOff, asOf);

        return new CreditDto(
            Id: credit.Id,
            Name: credit.Name,
            Type: credit.Type.ToString(),
            InterestModel: credit.InterestModel.ToString(),
            RateConvention: credit.RateConvention.ToString(),
            Currency: credit.Currency,
            Principal: credit.Principal,
            AnnualInterestRate: credit.AnnualInterestRate,
            TermMonths: credit.TermMonths,
            StartDate: credit.StartDate,
            PrepaymentPenaltyRate: credit.PrepaymentPenaltyRate,
            MonthlyInsuranceRate: credit.MonthlyInsuranceRate,
            MonthlyFixedFee: credit.MonthlyFixedFee,
            PrepaymentEffect: credit.PrepaymentEffect.ToString(),
            MonthlyInstallment: sim.CurrentInstallment,
            TotalToPay: sim.TotalToPay,
            TotalPaid: sim.TotalPaid,
            OutstandingPrincipal: sim.OutstandingPrincipal,
            ProgressPercent: progressPercent,
            PaymentDueDay: credit.PaymentDueDay,
            NextDueDate: due.NextDueDate,
            DaysUntilDue: due.DaysUntilDue,
            IsOverdue: due.IsOverdue,
            IsDueSoon: due.IsDueSoon,
            AlertLevel: due.AlertLevel,
            Status: sim.IsPaidOff ? "PaidOff" : "Active");
    }

    public static CreditScheduleDto ToSchedule(Credit credit, IReadOnlyList<CreditPayment> payments)
    {
        var sim = AmortizationCalculator.Simulate(
            credit.Principal, credit.AnnualInterestRate, credit.TermMonths, credit.InterestModel,
            credit.StartDate, ToEvents(credit, payments), credit.RateConvention);

        var rows = sim.Schedule
            .Select(r =>
            {
                // Insurance is billed on the balance at the START of the installment period
                // (before this row's principal is applied). Prepayment rows carry no charges.
                var startBalance = r.RemainingBalance + r.Principal;
                var charges = r.IsPrepayment ? 0m : MonthlyCharge(startBalance, credit);
                var totalDue = Math.Round(r.Installment + charges, 2, MidpointRounding.AwayFromZero);

                return new AmortizationRowDto(
                    r.Number,
                    r.IsPrepayment && r.Date.HasValue
                        ? DateTime.SpecifyKind(r.Date.Value, DateTimeKind.Utc)
                        : DueDateForInstallment(credit, r.Number),
                    r.Installment,
                    r.Interest,
                    r.Principal,
                    r.RemainingBalance,
                    r.IsPrepayment,
                    charges,
                    totalDue);
            })
            .ToList();
        return new CreditScheduleDto(credit.Id, rows);
    }

    /// <summary>
    /// Projects credit payments onto the calculator's <see cref="PaymentEvent"/> input. An
    /// abono a capital without an explicit effect falls back to the credit's default effect.
    /// </summary>
    private static IReadOnlyList<PaymentEvent> ToEvents(Credit credit, IReadOnlyList<CreditPayment> payments) =>
        payments
            .Select(p => new PaymentEvent(
                p.Date,
                p.Amount,
                p.Type == CreditPaymentType.PrincipalPrepayment,
                p.Effect ?? credit.PrepaymentEffect))
            .ToList();

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
