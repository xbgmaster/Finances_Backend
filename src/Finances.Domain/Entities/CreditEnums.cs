namespace Finances.Domain.Entities;

/// <summary>
/// How interest is charged on a credit. This is independent of <see cref="CreditType"/>
/// (the product label), because the same product can use different math.
/// </summary>
public enum InterestModel
{
    /// <summary>
    /// French amortization: interest is charged on the OUTSTANDING balance, which
    /// decreases as principal is repaid. Fixed installment; interest portion shrinks.
    /// </summary>
    CompoundFrench = 0,

    /// <summary>
    /// Simple/flat interest: interest is charged on the ORIGINAL principal for the
    /// whole term and spread evenly across installments. More expensive for the same
    /// nominal rate.
    /// </summary>
    SimpleFlat = 1
}

/// <summary>
/// How an ANNUAL interest rate is converted into the monthly rate used for compounding.
/// Latin-American loans (e.g. Colombia) quote <b>% E.A.</b> (efectivo anual) and convert with
/// the effective formula; many other contexts quote a nominal APR divided by 12.
/// </summary>
public enum RateConvention
{
    /// <summary>Nominal: monthly rate = annual / 12. Overstates the effective monthly rate.</summary>
    Nominal = 0,

    /// <summary>Effective annual (E.A.): monthly rate = (1 + annual)^(1/12) − 1.</summary>
    EffectiveAnnual = 1
}

/// <summary>
/// How unearned interest is refunded when a flat-interest credit is paid off early.
/// (French credits inherently save all future interest, so this mainly affects flat.)
/// </summary>
public enum PrepaymentRebateMethod
{
    /// <summary>No rebate: all contractual interest is charged regardless (worst case).</summary>
    None = 0,

    /// <summary>Interest earned proportionally to elapsed term; unearned interest is refunded.</summary>
    Actuarial = 1,

    /// <summary>Rule of 78 (sum-of-digits): interest is front-loaded, so early payoff saves less.</summary>
    RuleOf78 = 2
}

/// <summary>
/// What a partial prepayment (an extra amount beyond the scheduled installment) does
/// to the remaining plan.
/// </summary>
public enum PrepaymentEffect
{
    /// <summary>Keep the installment, finish earlier (saves the most interest).</summary>
    ReduceTerm = 0,

    /// <summary>Keep the term, lower the future installment (eases monthly cash flow).</summary>
    ReduceInstallment = 1
}

/// <summary>
/// What a recorded <see cref="CreditPayment"/> represents. A regular installment pays the
/// period's interest first and the rest to principal; an extra principal prepayment
/// ("abono a capital") goes entirely to principal and reshapes the remaining plan.
/// </summary>
public enum CreditPaymentType
{
    /// <summary>A regular monthly installment (interest first, remainder to principal).</summary>
    Installment = 0,

    /// <summary>An extra payment applied entirely to principal ("abono a capital").</summary>
    PrincipalPrepayment = 1
}
