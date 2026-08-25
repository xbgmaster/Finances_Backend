namespace Finances.Application.Credits.Calculations;

/// <summary>A single row of the amortization schedule (one scheduled installment).</summary>
public record AmortizationRow(
    int Number,
    decimal Installment,
    decimal Interest,
    decimal Principal,
    decimal RemainingBalance);

/// <summary>
/// The full repayment plan for a credit (French or flat). <see cref="MonthlyRate"/> is
/// the nominal monthly rate; <see cref="EffectiveAnnualRatePercent"/> is the true annual
/// cost (APR) derived from the installment stream, which is especially useful for flat
/// credits where the nominal rate hides the real cost.
/// </summary>
public record AmortizationPlan(
    decimal MonthlyRate,
    decimal MonthlyInstallment,
    decimal TotalToPay,
    decimal TotalInterest,
    decimal EffectiveAnnualRatePercent,
    IReadOnlyList<AmortizationRow> Schedule);

/// <summary>
/// Derived progress of a credit given the total amount actually paid so far.
/// Everything here is computed, never stored.
/// </summary>
public record CreditProgress(
    decimal TotalPaid,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal OutstandingPrincipal,
    decimal RemainingTotal,
    decimal SavingsIfPaidOffToday,
    int InstallmentsCovered,
    bool IsPaidOff);
