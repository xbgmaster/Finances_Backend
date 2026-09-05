using Finances.Domain.Entities;

namespace Finances.Application.Credits.Calculations;

/// <summary>
/// A single row of the amortization schedule. Normally one scheduled installment, but it
/// can also represent a principal prepayment (abono a capital) shown on its own line: in
/// that case <see cref="IsPrepayment"/> is true, <see cref="Interest"/> is 0 and
/// <see cref="Date"/> carries the real payment date instead of the installment due date.
/// </summary>
public record AmortizationRow(
    int Number,
    decimal Installment,
    decimal Interest,
    decimal Principal,
    decimal RemainingBalance,
    DateTime? Date = null,
    bool IsPrepayment = false);

/// <summary>
/// A single payment fed into the simulator: its date, amount, whether it is an extra
/// principal prepayment (abono a capital) and, for prepayments, the effect it has on the
/// remaining plan (reduce term vs reduce installment).
/// </summary>
public record PaymentEvent(
    DateTime Date,
    decimal Amount,
    bool IsPrincipalPrepayment,
    PrepaymentEffect Effect);

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

/// <summary>
/// Full derived state of a credit after replaying its whole payment ledger, including any
/// principal prepayments (abonos a capital) that reshaped the plan by reducing the term or
/// lowering the installment. <see cref="CurrentInstallment"/> is the installment currently
/// in effect (it drops after a "reduce installment" prepayment); <see cref="Schedule"/> is
/// the recalculated amortization table reflecting those prepayments. Everything is computed,
/// never stored.
/// </summary>
public record CreditSimulation(
    decimal CurrentInstallment,
    decimal TotalToPay,
    decimal TotalInterest,
    decimal TotalPaid,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PrepaidPrincipal,
    decimal OutstandingPrincipal,
    decimal RemainingTotal,
    decimal SavingsIfPaidOffToday,
    int InstallmentsCovered,
    int RemainingInstallments,
    bool IsPaidOff,
    IReadOnlyList<AmortizationRow> Schedule);
