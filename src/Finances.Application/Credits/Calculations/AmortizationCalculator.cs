using Finances.Domain.Entities;

namespace Finances.Application.Credits.Calculations;

/// <summary>
/// Pure financial calculator for credits. Supports two interest models:
/// <list type="bullet">
/// <item><description><b>French</b> (<see cref="InterestModel.CompoundFrench"/>): fixed installment,
/// interest charged on the declining balance.</description></item>
/// <item><description><b>Flat/simple</b> (<see cref="InterestModel.SimpleFlat"/>): interest charged on
/// the original principal for the whole term and split evenly.</description></item>
/// </list>
/// All monetary values use <see cref="decimal"/> and are rounded to 2 decimals; the final
/// installment absorbs any rounding residue so the balance reaches exactly zero.
///
/// The interest rate is provided as an ANNUAL percentage (e.g. 2.3 = 2.3%). This class has
/// no dependencies on EF, the database, or the web layer, which makes it trivial to test.
/// </summary>
public static class AmortizationCalculator
{
    /// <summary>Converts an annual percentage rate into a nominal monthly rate (fraction).</summary>
    public static decimal MonthlyRateFromAnnual(decimal annualRatePercent) =>
        annualRatePercent / 100m / 12m;

    /// <summary>Builds the repayment plan using the French model (kept for backward compatibility).</summary>
    public static AmortizationPlan BuildPlan(decimal principal, decimal annualRatePercent, int termMonths) =>
        BuildPlan(principal, annualRatePercent, termMonths, InterestModel.CompoundFrench);

    /// <summary>Builds the full repayment plan (installment + schedule) for the given interest model.</summary>
    public static AmortizationPlan BuildPlan(decimal principal, decimal annualRatePercent, int termMonths, InterestModel model)
    {
        if (principal <= 0) throw new ArgumentOutOfRangeException(nameof(principal));
        if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
        if (annualRatePercent < 0) throw new ArgumentOutOfRangeException(nameof(annualRatePercent));

        return model switch
        {
            InterestModel.SimpleFlat => BuildFlatPlan(principal, annualRatePercent, termMonths),
            _ => BuildFrenchPlan(principal, annualRatePercent, termMonths),
        };
    }

    private static AmortizationPlan BuildFrenchPlan(decimal principal, decimal annualRatePercent, int termMonths)
    {
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

        var effective = EffectiveAnnualRatePercent(principal, schedule);
        return new AmortizationPlan(i, installment, Round(totalToPay), Round(totalInterest), effective, schedule);
    }

    private static AmortizationPlan BuildFlatPlan(decimal principal, decimal annualRatePercent, int termMonths)
    {
        // Simple interest on the ORIGINAL principal for the whole term.
        var years = termMonths / 12m;
        var totalInterest = Round(principal * (annualRatePercent / 100m) * years);
        var totalToPay = Round(principal + totalInterest);

        // Even installment; interest and principal are split evenly, last row absorbs residue.
        var installment = Round(totalToPay / termMonths);
        var evenInterest = Round(totalInterest / termMonths);
        var evenPrincipal = Round(principal / termMonths);

        var schedule = new List<AmortizationRow>(termMonths);
        var balance = principal;
        decimal interestAccrued = 0m;

        for (var k = 1; k <= termMonths; k++)
        {
            decimal interest;
            decimal principalPortion;

            if (k == termMonths)
            {
                interest = Round(totalInterest - interestAccrued);
                principalPortion = balance;
            }
            else
            {
                interest = evenInterest;
                principalPortion = evenPrincipal;
            }

            interestAccrued += interest;
            balance = Round(balance - principalPortion);
            if (balance < 0) balance = 0m;

            var rowInstallment = Round(principalPortion + interest);
            schedule.Add(new AmortizationRow(k, rowInstallment, interest, principalPortion, balance));
        }

        var monthlyRate = MonthlyRateFromAnnual(annualRatePercent);
        var effective = EffectiveAnnualRatePercent(principal, schedule);
        return new AmortizationPlan(monthlyRate, installment, totalToPay, totalInterest, effective, schedule);
    }

    /// <summary>
    /// Given a plan and the total amount actually paid, walks the schedule in order
    /// (each installment covers its interest first, then principal) to derive how
    /// much principal/interest has been paid, the outstanding principal (payoff
    /// amount today) and the interest that would be saved by paying it off now.
    /// This works for both models because it operates on the generic schedule.
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

        // Paying off the outstanding principal today avoids all interest not yet paid
        // (this is the actuarial / pro-rata rebate: unearned interest is refunded).
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

    /// <summary>
    /// True effective annual rate (APR) implied by the installment stream, expressed as a
    /// percentage. Found by solving for the monthly IRR that discounts the installments back
    /// to the principal, then compounding to a yearly figure. Returns 0 for interest-free plans.
    /// </summary>
    public static decimal EffectiveAnnualRatePercent(decimal principal, IReadOnlyList<AmortizationRow> schedule)
    {
        var p = (double)principal;
        var totalPaid = schedule.Sum(r => (double)r.Installment);
        if (p <= 0 || totalPaid <= p) return 0m;

        var cash = schedule.Select(r => (double)r.Installment).ToArray();

        // Newton-Raphson on the monthly internal rate of return.
        var r = 0.01; // 1% monthly initial guess
        for (var iter = 0; iter < 100; iter++)
        {
            double f = -p, df = 0d;
            for (var k = 0; k < cash.Length; k++)
            {
                var denom = Math.Pow(1 + r, k + 1);
                f += cash[k] / denom;
                df += -(k + 1) * cash[k] / (denom * (1 + r));
            }

            if (Math.Abs(df) < 1e-12) break;
            var next = r - f / df;
            if (double.IsNaN(next) || double.IsInfinity(next)) break;
            if (next <= -0.9999) next = -0.9999;
            if (Math.Abs(next - r) < 1e-10)
            {
                r = next;
                break;
            }
            r = next;
        }

        var effectiveAnnual = (Math.Pow(1 + r, 12) - 1) * 100d;
        if (double.IsNaN(effectiveAnnual) || effectiveAnnual < 0) return 0m;
        return Math.Round((decimal)effectiveAnnual, 2, MidpointRounding.AwayFromZero);
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
