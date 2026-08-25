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

    /// <summary>The fixed French installment for a principal, monthly rate and term.</summary>
    private static decimal FrenchInstallment(decimal principal, decimal i, int termMonths)
    {
        if (termMonths <= 0 || principal <= 0m) return Round(Math.Max(principal, 0m));
        return i == 0m
            ? Round(principal / termMonths)
            : Round(principal * (i * Pow(1 + i, termMonths)) / (Pow(1 + i, termMonths) - 1));
    }

    private static AmortizationPlan BuildFrenchPlan(decimal principal, decimal annualRatePercent, int termMonths)
    {
        var i = MonthlyRateFromAnnual(annualRatePercent);

        var installment = FrenchInstallment(principal, i, termMonths);

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
    /// Replays the whole payment ledger (regular installments + extra principal prepayments)
    /// to derive the credit's real state. Prepayments ("abonos a capital") go entirely to
    /// principal and, per each prepayment's <see cref="PrepaymentEffect"/>, either shorten the
    /// remaining term (keep the installment, finish earlier) or lower the future installment.
    ///
    /// When there are no prepayments this delegates to the classic
    /// <see cref="BuildPlan(decimal, decimal, int, InterestModel)"/> +
    /// <see cref="ComputeProgress"/> path, so existing behaviour is unchanged.
    /// Interest is accrued per monthly period on the outstanding balance.
    /// </summary>
    public static CreditSimulation Simulate(
        decimal principal, decimal annualRatePercent, int termMonths, InterestModel model,
        DateTime startDate, IReadOnlyList<PaymentEvent> payments)
    {
        payments ??= Array.Empty<PaymentEvent>();
        var plan = BuildPlan(principal, annualRatePercent, termMonths, model);

        var hasPrepayments = payments.Any(p => p.IsPrincipalPrepayment && p.Amount > 0m);
        if (!hasPrepayments)
        {
            var totalPaid = payments.Sum(p => Math.Max(p.Amount, 0m));
            var progress = ComputeProgress(plan, principal, totalPaid);
            return new CreditSimulation(
                CurrentInstallment: plan.MonthlyInstallment,
                TotalToPay: plan.TotalToPay,
                TotalInterest: plan.TotalInterest,
                TotalPaid: progress.TotalPaid,
                PrincipalPaid: progress.PrincipalPaid,
                InterestPaid: progress.InterestPaid,
                PrepaidPrincipal: 0m,
                OutstandingPrincipal: progress.OutstandingPrincipal,
                RemainingTotal: progress.RemainingTotal,
                SavingsIfPaidOffToday: progress.SavingsIfPaidOffToday,
                InstallmentsCovered: progress.InstallmentsCovered,
                RemainingInstallments: progress.IsPaidOff ? 0 : Math.Max(termMonths - progress.InstallmentsCovered, 0),
                IsPaidOff: progress.IsPaidOff,
                Schedule: plan.Schedule);
        }

        var i = MonthlyRateFromAnnual(annualRatePercent);
        return model == InterestModel.SimpleFlat
            ? SimulateFlatWithPrepayments(principal, termMonths, plan, payments)
            : SimulateFrenchWithPrepayments(principal, i, termMonths, startDate, payments);
    }

    /// <summary>
    /// French/compound simulation with prepayments. Walks the ledger in date order accruing
    /// interest monthly on the live balance; regular payments pay accrued interest first then
    /// principal, prepayments go straight to principal and reshape the plan.
    /// </summary>
    private static CreditSimulation SimulateFrenchWithPrepayments(
        decimal principal, decimal i, int termMonths,
        DateTime startDate, IReadOnlyList<PaymentEvent> payments)
    {
        var originalInstallment = FrenchInstallment(principal, i, termMonths);

        var ordered = payments
            .Where(p => p.Amount > 0m)
            // On the same day, apply the regular installment before an extra abono.
            .OrderBy(p => p.Date)
            .ThenBy(p => p.IsPrincipalPrepayment ? 1 : 0)
            .ToList();

        decimal outstanding = principal;
        decimal installment = originalInstallment;
        decimal principalPaid = 0m, interestPaid = 0m, prepaid = 0m, totalPaid = 0m, accruedInterest = 0m;
        var regularCount = 0;
        var elapsedMonths = 0;

        foreach (var pmt in ordered)
        {
            // Accrue interest for each whole month elapsed since the last processed point.
            var months = WholeMonths(startDate.AddMonths(elapsedMonths), pmt.Date);
            for (var m = 0; m < months; m++)
            {
                accruedInterest = Round(accruedInterest + outstanding * i);
                elapsedMonths++;
            }

            totalPaid += pmt.Amount;

            if (pmt.IsPrincipalPrepayment)
            {
                var toPrincipal = Math.Min(pmt.Amount, outstanding);
                outstanding = Round(outstanding - toPrincipal);
                principalPaid += toPrincipal;
                prepaid += toPrincipal;

                if (pmt.Effect == PrepaymentEffect.ReduceInstallment)
                {
                    var remaining = Math.Max(1, termMonths - elapsedMonths);
                    installment = FrenchInstallment(outstanding, i, remaining);
                }
                // ReduceTerm keeps the installment; the balance simply clears sooner.
            }
            else
            {
                var payInterest = Math.Min(pmt.Amount, accruedInterest);
                accruedInterest = Round(accruedInterest - payInterest);
                interestPaid += payInterest;

                var toPrincipal = Math.Max(0m, Math.Min(pmt.Amount - payInterest, outstanding));
                outstanding = Round(outstanding - toPrincipal);
                principalPaid += toPrincipal;
                regularCount++;
            }

            if (outstanding < 0m) outstanding = 0m;
        }

        var isPaidOff = outstanding <= 0m;
        var (futureInterest, remainingInstallments) = ProjectForward(outstanding, i, installment);
        var totalInterest = Round(interestPaid + futureInterest);

        return new CreditSimulation(
            CurrentInstallment: Round(installment),
            TotalToPay: Round(principal + totalInterest),
            TotalInterest: totalInterest,
            TotalPaid: Round(totalPaid),
            PrincipalPaid: Round(principalPaid),
            InterestPaid: Round(interestPaid),
            PrepaidPrincipal: Round(prepaid),
            OutstandingPrincipal: Round(outstanding),
            RemainingTotal: isPaidOff ? 0m : Round(outstanding + futureInterest),
            SavingsIfPaidOffToday: isPaidOff ? 0m : Round(futureInterest),
            InstallmentsCovered: regularCount,
            RemainingInstallments: isPaidOff ? 0 : remainingInstallments,
            IsPaidOff: isPaidOff,
            Schedule: BuildEffectiveFrenchSchedule(principal, i, termMonths, startDate, payments));
    }

    /// <summary>
    /// Flat/simple interest with prepayments. Flat interest is contractually fixed, so this
    /// keeps the classic progress for regular payments and applies each prepayment as a direct
    /// principal reduction, saving the future interest proportional to the principal cleared.
    /// (Flat + abono a capital is an edge case; the French path above is the exact model.)
    /// </summary>
    private static CreditSimulation SimulateFlatWithPrepayments(
        decimal principal, int termMonths, AmortizationPlan plan, IReadOnlyList<PaymentEvent> payments)
    {
        var regularPaid = payments.Where(p => !p.IsPrincipalPrepayment).Sum(p => Math.Max(p.Amount, 0m));
        var prepaid = payments.Where(p => p.IsPrincipalPrepayment).Sum(p => Math.Max(p.Amount, 0m));

        var progress = ComputeProgress(plan, principal, regularPaid);

        var scheduledOutstanding = progress.OutstandingPrincipal;
        var outstanding = Round(Math.Max(scheduledOutstanding - prepaid, 0m));
        var appliedPrepaid = Round(scheduledOutstanding - outstanding);

        // Future interest still owed shrinks in proportion to the principal cleared by the abono.
        var remainingInterestBefore = Math.Max(plan.TotalInterest - progress.InterestPaid, 0m);
        var factor = scheduledOutstanding <= 0m ? 0m : outstanding / scheduledOutstanding;
        var savings = Round(remainingInterestBefore * factor);

        var isPaidOff = outstanding <= 0m;
        var totalInterest = Round(progress.InterestPaid + savings);

        return new CreditSimulation(
            CurrentInstallment: plan.MonthlyInstallment,
            TotalToPay: Round(principal + totalInterest),
            TotalInterest: totalInterest,
            TotalPaid: Round(regularPaid + appliedPrepaid),
            PrincipalPaid: Round(progress.PrincipalPaid + appliedPrepaid),
            InterestPaid: progress.InterestPaid,
            PrepaidPrincipal: appliedPrepaid,
            OutstandingPrincipal: outstanding,
            RemainingTotal: isPaidOff ? 0m : Round(outstanding + savings),
            SavingsIfPaidOffToday: isPaidOff ? 0m : savings,
            InstallmentsCovered: progress.InstallmentsCovered,
            RemainingInstallments: isPaidOff ? 0 : Math.Max(termMonths - progress.InstallmentsCovered, 0),
            IsPaidOff: isPaidOff,
            Schedule: plan.Schedule);
    }

    /// <summary>
    /// Projects the remaining French installments from a balance, returning the future
    /// interest that would be avoided by paying it off today and how many installments remain.
    /// </summary>
    private static (decimal FutureInterest, int Periods) ProjectForward(decimal balance, decimal i, decimal installment)
    {
        if (balance <= 0m) return (0m, 0);

        decimal futureInterest = 0m;
        var periods = 0;
        for (var guard = 0; guard < 1000 && balance > 0m; guard++)
        {
            var interest = Round(balance * i);
            var principalPortion = installment - interest;
            if (principalPortion <= 0m)
            {
                // Installment can't cover interest (degenerate); clear in one shot to avoid a loop.
                futureInterest += interest;
                periods++;
                break;
            }

            if (principalPortion > balance) principalPortion = balance;
            balance = Round(balance - principalPortion);
            futureInterest += interest;
            periods++;
        }

        return (Round(futureInterest), periods);
    }

    /// <summary>
    /// Builds the recalculated French amortization table with prepayments folded into the
    /// period their date falls in. A "reduce term" abono clears the balance sooner (fewer
    /// rows); a "reduce installment" abono recomputes the installment for the periods left.
    /// </summary>
    private static IReadOnlyList<AmortizationRow> BuildEffectiveFrenchSchedule(
        decimal principal, decimal i, int termMonths,
        DateTime startDate, IReadOnlyList<PaymentEvent> payments)
    {
        var prepaymentsByPeriod = payments
            .Where(p => p.IsPrincipalPrepayment && p.Amount > 0m)
            .GroupBy(p => Math.Clamp(WholeMonths(startDate, p.Date), 1, termMonths))
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Date).ToList());

        var rows = new List<AmortizationRow>(termMonths);
        var balance = principal;
        var installment = FrenchInstallment(principal, i, termMonths);

        for (var k = 1; k <= termMonths && balance > 0m; k++)
        {
            var interest = Round(balance * i);
            var scheduledPrincipal = Math.Max(0m, installment - interest);
            if (scheduledPrincipal > balance) scheduledPrincipal = balance;

            var afterInstallment = Round(balance - scheduledPrincipal);
            var extra = 0m;

            if (prepaymentsByPeriod.TryGetValue(k, out var preps))
            {
                foreach (var p in preps)
                {
                    var applied = Math.Min(p.Amount, Round(afterInstallment - extra));
                    if (applied <= 0m) continue;
                    extra += applied;

                    if (p.Effect == PrepaymentEffect.ReduceInstallment)
                    {
                        var newBalance = Round(afterInstallment - extra);
                        installment = FrenchInstallment(newBalance, i, Math.Max(1, termMonths - k));
                    }
                }
            }

            var totalPrincipal = Math.Min(scheduledPrincipal + extra, balance);
            balance = Round(balance - totalPrincipal);
            if (balance < 0m) balance = 0m;

            rows.Add(new AmortizationRow(k, Round(interest + totalPrincipal), interest, Round(totalPrincipal), balance));
        }

        return rows;
    }

    /// <summary>Whole calendar months between two dates (0 if <paramref name="to"/> is not after).</summary>
    private static int WholeMonths(DateTime from, DateTime to)
    {
        if (to <= from) return 0;
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        return Math.Max(0, months);
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
