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
