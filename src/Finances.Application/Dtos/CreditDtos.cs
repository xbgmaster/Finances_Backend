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
    decimal MonthlyInstallment,
    decimal TotalToPay,
    decimal TotalPaid,
    decimal OutstandingPrincipal,
    decimal ProgressPercent,
    string Status);

/// <summary>A recorded payment against a credit.</summary>
public record CreditPaymentDto(int Id, decimal Amount, DateTime Date, string? Note);

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
    string Status);

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
