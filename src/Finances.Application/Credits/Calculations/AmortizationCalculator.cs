namespace Finances.Application.Credits.Calculations;

/// <summary>
/// Pure financial calculator for the French amortization model (fixed monthly
/// installment). All monetary values use <see cref="decimal"/> and are rounded to
/// 2 decimals; the final installment absorbs any rounding residue so the balance
/// reaches exactly zero.
///
/// The interest rate is provided as an ANNUAL percentage (e.g. 2.3 = 2.3%) and is
/// converted to a nominal monthly rate (annual / 12), which is the convention used
/// in most loan contracts.
///
/// This class has no dependencies on EF, the database, or the web layer, which
/// makes it trivial to unit test.
/// </summary>
public static class AmortizationCalculator
{
    /// <summary>Converts an annual percentage rate into a nominal monthly rate (fraction).</summary>
    public static decimal MonthlyRateFromAnnual(decimal annualRatePercent) =>
        annualRatePercent / 100m / 12m;

    /// <summary>Builds the full repayment plan (installment + schedule) for a credit.</summary>
    public static AmortizationPlan BuildPlan(decimal principal, decimal annualRatePercent, int termMonths)
    {
        if (principal <= 0) throw new ArgumentOutOfRangeException(nameof(principal));
        if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
        if (annualRatePercent < 0) throw new ArgumentOutOfRangeException(nameof(annualRatePercent));

        var i = MonthlyRateFromAnnual(annualRatePercent);

        var installment = i == 0m
            ? Round(principal / termMonths)
            : Round(principal * (i * Pow(1 + i, termMonths)) / (Pow(1 + i, termMonths) - 1));

        var schedule = new List<AmortizationRow>(termMonths);
        var balance = principal;
        decimal totalToPay = 0m;
        decimal totalInterest = 0m;

        for (var k = 1; k <= termMonths; k++)
        {
            var interest = Round(balance * i);
            decimal principalPortion;
            decimal rowInstallment;

            if (k == termMonths)
            {
                // Last installment pays off whatever is left (absorbs rounding residue).
                principalPortion = balance;
                rowInstallment = Round(principalPortion + interest);
            }
            else
            {
                principalPortion = installment - interest;
                rowInstallment = installment;
            }

            balance = Round(balance - principalPortion);
            if (balance < 0) balance = 0m;

            totalToPay += rowInstallment;
            totalInterest += interest;

            schedule.Add(new AmortizationRow(k, rowInstallment, interest, principalPortion, balance));
        }

        return new AmortizationPlan(i, installment, Round(totalToPay), Round(totalInterest), schedule);
    }

    /// <summary>
    /// Given a plan and the total amount actually paid, walks the schedule in order
    /// (each installment covers its interest first, then principal) to derive how
    /// much principal/interest has been paid, the outstanding principal (payoff
    /// amount today) and the interest that would be saved by paying it off now.
    /// </summary>
    public static CreditProgress ComputeProgress(AmortizationPlan plan, decimal principal, decimal totalPaid)
    {
        if (totalPaid < 0) totalPaid = 0m;

        decimal principalPaid = 0m;
        decimal interestPaid = 0m;
        decimal outstanding = principal;
        var installmentsCovered = 0;
        var remaining = totalPaid;

        foreach (var row in plan.Schedule)
        {
            if (remaining <= 0m) break;

            if (remaining >= row.Installment)
            {
                interestPaid += row.Interest;
                principalPaid += row.Principal;
                outstanding = row.RemainingBalance;
                remaining -= row.Installment;
                installmentsCovered++;
            }
            else
            {
                // Partial installment: interest first, remainder to principal.
                var payInterest = Math.Min(remaining, row.Interest);
                interestPaid += payInterest;
                var toPrincipal = remaining - payInterest;
                principalPaid += toPrincipal;
                outstanding = Round(outstanding - toPrincipal);
                remaining = 0m;
            }
        }

        if (outstanding < 0m) outstanding = 0m;

        var isPaidOff = outstanding <= 0m || totalPaid >= plan.TotalToPay;
        var remainingTotal = Round(Math.Max(plan.TotalToPay - totalPaid, 0m));

        // Paying off the outstanding principal today avoids all interest not yet paid.
        var savings = Round(Math.Max(plan.TotalInterest - interestPaid, 0m));

        return new CreditProgress(
            TotalPaid: Round(totalPaid),
            PrincipalPaid: Round(principalPaid),
            InterestPaid: Round(interestPaid),
            OutstandingPrincipal: Round(outstanding),
            RemainingTotal: remainingTotal,
            SavingsIfPaidOffToday: savings,
            InstallmentsCovered: installmentsCovered,
            IsPaidOff: isPaidOff);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Integer power for decimals (term months are small, so this is fine).</summary>
    private static decimal Pow(decimal baseValue, int exponent)
    {
        decimal result = 1m;
        for (var n = 0; n < exponent; n++)
            result *= baseValue;
        return result;
    }
}
