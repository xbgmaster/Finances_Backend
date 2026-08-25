namespace Finances.Domain.Entities;

/// <summary>
/// An explicit exchange of money between two of the user's own currencies (e.g. USD -> COP).
/// It reduces the balance of <see cref="FromCurrency"/> by <see cref="FromAmount"/> and increases
/// the balance of <see cref="ToCurrency"/> by <see cref="ToAmount"/>. No automatic conversion is
/// ever applied to totals: currencies stay independent and only move when the user exchanges.
/// </summary>
public class CurrencyExchange
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>ISO code of the currency the money comes out of, e.g. "USD".</summary>
    public string FromCurrency { get; set; } = string.Empty;

    /// <summary>Amount deducted from the source currency balance.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>ISO code of the currency the money goes into, e.g. "COP".</summary>
    public string ToCurrency { get; set; } = string.Empty;

    /// <summary>Amount added to the destination currency balance.</summary>
    public decimal ToAmount { get; set; }

    /// <summary>Exchange rate used: destination units per 1 source unit (informational).</summary>
    public decimal Rate { get; set; }

    public string? Note { get; set; }

    /// <summary>Owner of the exchange (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
