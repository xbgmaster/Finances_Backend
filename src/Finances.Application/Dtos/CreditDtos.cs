namespace Finances.Application.Dtos;

/// <summary>List item for a credit, with a few derived headline figures.</summary>
public record CreditDto(
    int Id,
    string Name,
    string Type,
    string InterestModel,
    string? Currency,
    decimal Principal,
    decimal AnnualInterestRate,
    int TermMonths,
    DateTime StartDate,
    decimal PrepaymentPenaltyRate,
    string PrepaymentEffect,
    decimal MonthlyInstallment,
    decimal TotalToPay,
    decimal TotalPaid,
    decimal OutstandingPrincipal,
    decimal ProgressPercent,
    int PaymentDueDay,
    DateTime NextDueDate,
    int DaysUntilDue,
    bool IsOverdue,
    bool IsDueSoon,
    string AlertLevel,
    string Status);

/// <summary>A recorded payment against a credit. <see cref="Type"/> is "Installment" or
/// "PrincipalPrepayment"; <see cref="Effect"/> is set only for prepayments.</summary>
public record CreditPaymentDto(int Id, decimal Amount, DateTime Date, string? Note, string Type, string? Effect);

/// <summary>The full "smart summary" of a credit as of today, entirely derived.</summary>
public record CreditSummaryDto(
    int Id,
    string Name,
    string Type,
    string InterestModel,
    string? Currency,
    decimal Principal,
    decimal AnnualInterestRate,
    decimal MonthlyInterestRate,
    decimal EffectiveAnnualRate,
    int TermMonths,
    DateTime StartDate,
    decimal MonthlyInstallment,
    decimal TotalToPay,
    decimal TotalInterest,
    decimal TotalPaid,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal PrepaidPrincipal,
    decimal OutstandingPrincipal,
    decimal RemainingTotal,
    decimal SavingsIfPaidOffToday,
    decimal PrepaymentPenaltyRate,
    decimal PrepaymentPenaltyAmount,
    decimal NetSavingsIfPaidOffToday,
    decimal PayoffAmountToday,
    string PrepaymentEffect,
    int InstallmentsCovered,
    int InstallmentsRemaining,
    int ExpectedInstallmentsToDate,
    decimal ProgressPercent,
    int PaymentDueDay,
    DateTime NextDueDate,
    int DaysUntilDue,
    bool IsOverdue,
    bool IsDueSoon,
    string AlertLevel,
    string Status);

/// <summary>A single credit that currently needs the user's attention (due soon or overdue).</summary>
public record CreditAlertItemDto(
    int CreditId,
    string Name,
    string AlertLevel,
    DateTime NextDueDate,
    int DaysUntilDue,
    decimal MonthlyInstallment,
    string? Currency);

/// <summary>Aggregated payment alerts for the current user (feeds the bell + dashboard banner).</summary>
public record CreditAlertsDto(
    int OverdueCount,
    int DueSoonCount,
    IReadOnlyList<CreditAlertItemDto> Items);

/// <summary>One row of the amortization schedule exposed to the API.</summary>
public record AmortizationRowDto(
    int Number,
    DateTime DueDate,
    decimal Installment,
    decimal Interest,
    decimal Principal,
    decimal RemainingBalance);

/// <summary>Full amortization schedule for a credit.</summary>
public record CreditScheduleDto(int CreditId, IReadOnlyList<AmortizationRowDto> Rows);
