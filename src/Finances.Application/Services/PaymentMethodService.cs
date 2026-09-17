using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public PaymentMethodService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var now = DateTime.UtcNow;

        await EnsureDefaultCashAsync(userId, ct);
        await MergeDefaultCashDuplicatesAsync(userId, baseCurrency, ct);

        var methods = await _db.PaymentMethods
            .Where(p => p.UserId == userId)
            .Where(p => includeArchived || !p.Archived)
            .OrderBy(p => p.Archived).ThenByDescending(p => p.IsFavorite).ThenBy(p => p.Name)
            .ToListAsync(ct);

        if (methods.Count == 0) return new List<PaymentMethodDto>();

        var ids = methods.Select(m => m.Id).ToList();
        // Each account is single-currency; only same-currency movements affect its balance/totals.
        var currencyById = methods.ToDictionary(m => m.Id, m => (m.Currency ?? baseCurrency).ToUpperInvariant());

        // Group also by currency so foreign-currency rows attributed to an account can be dropped.
        var expenseAgg = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId != null && ids.Contains(e.PaymentMethodId.Value))
            .GroupBy(e => new { Pm = e.PaymentMethodId!.Value, e.Currency })
            .Select(g => new
            {
                g.Key.Pm,
                g.Key.Currency,
                Total = g.Sum(x => x.Amount),
                Month = g.Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month).Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        var incomeAgg = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId != null && ids.Contains(i.PaymentMethodId.Value))
            .GroupBy(i => new { Pm = i.PaymentMethodId!.Value, i.Currency })
            .Select(g => new
            {
                g.Key.Pm,
                g.Key.Currency,
                Total = g.Sum(x => x.Amount),
                Month = g.Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month).Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        var expenseById = new Dictionary<int, (decimal Month, decimal Total)>();
        foreach (var g in expenseAgg)
        {
            var eff = (g.Currency ?? baseCurrency).ToUpperInvariant();
            if (!currencyById.TryGetValue(g.Pm, out var cur) || eff != cur) continue;
            var acc = expenseById.GetValueOrDefault(g.Pm);
            expenseById[g.Pm] = (acc.Month + g.Month, acc.Total + g.Total);
        }
        var incomeById = new Dictionary<int, (decimal Month, decimal Total)>();
        foreach (var g in incomeAgg)
        {
            var eff = (g.Currency ?? baseCurrency).ToUpperInvariant();
            if (!currencyById.TryGetValue(g.Pm, out var cur) || eff != cur) continue;
            var acc = incomeById.GetValueOrDefault(g.Pm);
            incomeById[g.Pm] = (acc.Month + g.Month, acc.Total + g.Total);
        }

        // Payments applied TO each credit card (reduce its debt / free cupo).
        var paidToCardAgg = await _db.CardPayments
            .Where(p => p.UserId == userId && ids.Contains(p.CreditCardId))
            .GroupBy(p => p.CreditCardId)
            .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        // Payments funded FROM each cash/debit account (reduce its available balance).
        var fundedAgg = await _db.CardPayments
            .Where(p => p.UserId == userId && p.SourcePaymentMethodId != null
                && ids.Contains(p.SourcePaymentMethodId.Value))
            .GroupBy(p => p.SourcePaymentMethodId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        // Currency exchanges that landed IN / left FROM each account. Only the leg whose currency
        // matches the account counts, so a transfer to/from a different-currency account never
        // inflates this account's balance (e.g. a COP amount landing in a CAD account).
        var exLegs = await _db.CurrencyExchanges
            .Where(x => x.UserId == userId
                && ((x.ToPaymentMethodId != null && ids.Contains(x.ToPaymentMethodId.Value))
                    || (x.FromPaymentMethodId != null && ids.Contains(x.FromPaymentMethodId.Value))))
            .Select(x => new { x.FromPaymentMethodId, x.FromCurrency, x.FromAmount, x.ToPaymentMethodId, x.ToCurrency, x.ToAmount })
            .ToListAsync(ct);
        var exchangeInById = new Dictionary<int, decimal>();
        var exchangeOutById = new Dictionary<int, decimal>();
        foreach (var x in exLegs)
        {
            if (x.ToPaymentMethodId is int toId && currencyById.TryGetValue(toId, out var toCur)
                && string.Equals(x.ToCurrency, toCur, StringComparison.OrdinalIgnoreCase))
                exchangeInById[toId] = exchangeInById.GetValueOrDefault(toId) + x.ToAmount;
            if (x.FromPaymentMethodId is int fromId && currencyById.TryGetValue(fromId, out var fromCur)
                && string.Equals(x.FromCurrency, fromCur, StringComparison.OrdinalIgnoreCase))
                exchangeOutById[fromId] = exchangeOutById.GetValueOrDefault(fromId) + x.FromAmount;
        }

        var paidToCardById = paidToCardAgg.ToDictionary(a => a.Id, a => a.Total);
        var fundedById = fundedAgg.ToDictionary(a => a.Id, a => a.Total);
        return methods.Select(m =>
        {
            expenseById.TryGetValue(m.Id, out var exp);
            incomeById.TryGetValue(m.Id, out var inc);
            paidToCardById.TryGetValue(m.Id, out var paidToCard);
            fundedById.TryGetValue(m.Id, out var funded);
            exchangeInById.TryGetValue(m.Id, out var exIn);
            exchangeOutById.TryGetValue(m.Id, out var exOut);
            return Map(m, baseCurrency, exp.Month, exp.Total,
                inc.Month, inc.Total, paidToCard, funded, exIn, exOut);
        }).ToList();
    }

    public async Task<PaymentMethodDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var now = DateTime.UtcNow;

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (method is null) return null;

        var (expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut) =
            await AggregateAsync(id, userId, now, (method.Currency ?? baseCurrency).ToUpperInvariant(), baseCurrency.ToUpperInvariant(), ct);
        return Map(method, baseCurrency, expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut);
    }

    public async Task<PaymentMethodDto> CreateAsync(PaymentMethodCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var type = ParseType(dto.Type);

        var method = new PaymentMethod
        {
            Name = dto.Name.Trim(),
            Type = type,
            Currency = NormalizeCurrency(dto.Currency, baseCurrency),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "card" : dto.Icon.Trim(),
            Archived = dto.Archived,
            IsFavorite = dto.IsFavorite,
            UserId = userId
        };
        ApplyCreditCardFields(method, type, dto);

        _db.PaymentMethods.Add(method);
        if (dto.IsFavorite) await ClearOtherFavoritesAsync(userId, method, baseCurrency, ct);
        await _db.SaveChangesAsync(ct);
        return Map(method, baseCurrency, 0m, 0m, 0m, 0m, 0m, 0m);
    }

    public async Task<PaymentMethodDto> UpdateAsync(int id, PaymentMethodCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var type = ParseType(dto.Type);

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago no existe.");

        method.Name = dto.Name.Trim();
        method.Type = type;
        method.Currency = NormalizeCurrency(dto.Currency, baseCurrency);
        method.Color = string.IsNullOrWhiteSpace(dto.Color) ? method.Color : dto.Color.Trim();
        method.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? method.Icon : dto.Icon.Trim();
        method.Archived = dto.Archived;
        method.IsFavorite = dto.IsFavorite;
        ApplyCreditCardFields(method, type, dto);

        if (dto.IsFavorite) await ClearOtherFavoritesAsync(userId, method, baseCurrency, ct);
        await _db.SaveChangesAsync(ct);

        var (expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut) =
            await AggregateAsync(id, userId, DateTime.UtcNow, (method.Currency ?? baseCurrency).ToUpperInvariant(), baseCurrency.ToUpperInvariant(), ct);
        return Map(method, baseCurrency, expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut);
    }

    public async Task<PaymentMethodDto> SetFavoriteAsync(int id, bool isFavorite, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago no existe.");

        method.IsFavorite = isFavorite;
        if (isFavorite) await ClearOtherFavoritesAsync(userId, method, baseCurrency, ct);
        await _db.SaveChangesAsync(ct);

        var (expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut) =
            await AggregateAsync(id, userId, DateTime.UtcNow, (method.Currency ?? baseCurrency).ToUpperInvariant(), baseCurrency.ToUpperInvariant(), ct);
        return Map(method, baseCurrency, expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exIn, exOut);
    }

    /// <summary>
    /// Ensures a single favorite per user *per currency*: unsets the flag only on other methods that
    /// share the favorite's (effective) currency, so each currency can keep its own favorite.
    /// </summary>
    private async Task ClearOtherFavoritesAsync(string userId, PaymentMethod favorite, string baseCurrency, CancellationToken ct)
    {
        var favCurrency = (favorite.Currency ?? baseCurrency).ToUpperInvariant();
        var others = await _db.PaymentMethods
            .Where(p => p.UserId == userId && p.IsFavorite && p.Id != favorite.Id)
            .ToListAsync(ct);
        foreach (var o in others)
            if ((o.Currency ?? baseCurrency).ToUpperInvariant() == favCurrency) o.IsFavorite = false;
    }

    /// <summary>Creates the built-in cash account the first time an existing user has none.</summary>
    private async Task EnsureDefaultCashAsync(string userId, CancellationToken ct)
    {
        var hasAny = await _db.PaymentMethods.AnyAsync(p => p.UserId == userId, ct);
        if (hasAny) return;
        _db.PaymentMethods.Add(PaymentMethod.DefaultCash(userId));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Heals historical data: collapses duplicate built-in "Cash" accounts into a single one per
    /// currency and repoints every reference to the survivor. These duplicates were created before a
    /// null-currency base account was recognized by the currency checks. Only auto-created cash
    /// accounts (canonical/localized "Cash"/"Efectivo") are merged; user-named accounts (e.g.
    /// "Personal") are never touched. Idempotent and cheap once there is nothing to merge.
    /// </summary>
    private async Task MergeDefaultCashDuplicatesAsync(string userId, string baseCurrency, CancellationToken ct)
    {
        var defaultNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PaymentMethod.DefaultCashName, "Cash", "Efectivo",
        };

        var cashes = await _db.PaymentMethods
            .Where(p => p.UserId == userId && p.Type == PaymentMethodType.Cash)
            .ToListAsync(ct);

        string Eff(PaymentMethod p) => (p.Currency ?? baseCurrency).ToUpperInvariant();

        var groups = cashes
            .Where(p => defaultNames.Contains(p.Name))
            .GroupBy(Eff)
            .Where(g => g.Count() > 1)
            .ToList();
        if (groups.Count == 0) return;

        foreach (var group in groups)
        {
            // Keep the favorite, then the base (null-currency) account, then the oldest.
            var keeper = group
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.Currency == null ? 0 : 1)
                .ThenBy(p => p.Id)
                .First();
            keeper.Currency = Eff(keeper); // pin an explicit currency so the ambiguity can't return
            var dups = group.Where(p => p.Id != keeper.Id).ToList();
            var dupIds = dups.Select(p => p.Id).ToList();

            // Repoint every reference from the duplicates to the survivor (tracked updates so we
            // stay on the EF Core abstractions the Application layer depends on).
            var expenses = await _db.Expenses
                .Where(e => e.UserId == userId && e.PaymentMethodId != null && dupIds.Contains(e.PaymentMethodId.Value))
                .ToListAsync(ct);
            foreach (var e in expenses) e.PaymentMethodId = keeper.Id;

            var incomes = await _db.Incomes
                .Where(i => i.UserId == userId && i.PaymentMethodId != null && dupIds.Contains(i.PaymentMethodId.Value))
                .ToListAsync(ct);
            foreach (var i in incomes) i.PaymentMethodId = keeper.Id;

            var schedules = await _db.IncomeSchedules
                .Where(x => x.UserId == userId && x.PaymentMethodId != null && dupIds.Contains(x.PaymentMethodId.Value))
                .ToListAsync(ct);
            foreach (var s in schedules) s.PaymentMethodId = keeper.Id;

            var cardPayments = await _db.CardPayments
                .Where(x => x.UserId == userId && x.SourcePaymentMethodId != null && dupIds.Contains(x.SourcePaymentMethodId.Value))
                .ToListAsync(ct);
            foreach (var c in cardPayments) c.SourcePaymentMethodId = keeper.Id;

            var exchanges = await _db.CurrencyExchanges
                .Where(x => x.UserId == userId
                    && ((x.FromPaymentMethodId != null && dupIds.Contains(x.FromPaymentMethodId.Value))
                        || (x.ToPaymentMethodId != null && dupIds.Contains(x.ToPaymentMethodId.Value))))
                .ToListAsync(ct);
            foreach (var x in exchanges)
            {
                if (x.FromPaymentMethodId != null && dupIds.Contains(x.FromPaymentMethodId.Value)) x.FromPaymentMethodId = keeper.Id;
                if (x.ToPaymentMethodId != null && dupIds.Contains(x.ToPaymentMethodId.Value)) x.ToPaymentMethodId = keeper.Id;
            }

            _db.PaymentMethods.RemoveRange(dups);
        }
        await _db.SaveChangesAsync(ct); // one atomic save: repoints, pinned currency and deletions
    }

    private async Task<(decimal expMonth, decimal expTotal, decimal incMonth, decimal incTotal,
        decimal paidToCard, decimal funded, decimal exchangeIn, decimal exchangeOut)> AggregateAsync(
        int id, string userId, DateTime now, string currency, string baseCurrency, CancellationToken ct)
    {
        // Only movements in the account's own currency affect its balance (an account is
        // single-currency; a foreign-currency row attributed to it must not inflate it).
        var expMonth = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId == id
                && (e.Currency ?? baseCurrency) == currency
                && e.Date.Year == now.Year && e.Date.Month == now.Month)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var expTotal = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId == id && (e.Currency ?? baseCurrency) == currency)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var incMonth = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId == id
                && (i.Currency ?? baseCurrency) == currency
                && i.Date.Year == now.Year && i.Date.Month == now.Month)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var incTotal = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId == id && (i.Currency ?? baseCurrency) == currency)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var paidToCard = await _db.CardPayments
            .Where(p => p.UserId == userId && p.CreditCardId == id)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var funded = await _db.CardPayments
            .Where(p => p.UserId == userId && p.SourcePaymentMethodId == id)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        // Only count the exchange leg whose currency matches this account (avoids a foreign-currency
        // transfer inflating the balance).
        var exchangeIn = await _db.CurrencyExchanges
            .Where(x => x.UserId == userId && x.ToPaymentMethodId == id && x.ToCurrency == currency)
            .SumAsync(x => (decimal?)x.ToAmount, ct) ?? 0m;
        var exchangeOut = await _db.CurrencyExchanges
            .Where(x => x.UserId == userId && x.FromPaymentMethodId == id && x.FromCurrency == currency)
            .SumAsync(x => (decimal?)x.FromAmount, ct) ?? 0m;
        return (expMonth, expTotal, incMonth, incTotal, paidToCard, funded, exchangeIn, exchangeOut);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago no existe.");

        // Linked expenses/incomes keep their history: the FK is set to null on delete.
        _db.PaymentMethods.Remove(method);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CardPaymentDto> PayCardAsync(int cardId, CardPaymentCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var card = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == cardId && p.UserId == userId, ct)
            ?? throw new NotFoundException("La tarjeta no existe.");
        if (card.Type != PaymentMethodType.CreditCard)
            throw new ValidationException("Solo se pueden pagar tarjetas de crédito.");

        var currency = card.Currency ?? baseCurrency;

        PaymentMethod? source = null;
        if (dto.SourcePaymentMethodId is not null)
        {
            source = await _db.PaymentMethods
                .FirstOrDefaultAsync(p => p.Id == dto.SourcePaymentMethodId && p.UserId == userId, ct)
                ?? throw new NotFoundException("La cuenta de origen no existe.");
            if (source.Type == PaymentMethodType.CreditCard)
                throw new ValidationException("El pago debe salir de una cuenta de efectivo o débito.");
            var sourceCurrency = source.Currency ?? baseCurrency;
            if (!string.Equals(sourceCurrency, currency, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La cuenta de origen debe estar en la misma moneda que la tarjeta.");
        }

        var payment = new CardPayment
        {
            CreditCardId = card.Id,
            SourcePaymentMethodId = source?.Id,
            Amount = dto.Amount,
            Currency = currency,
            Date = dto.Date ?? DateTime.UtcNow,
            Note = dto.Note?.Trim(),
            UserId = userId
        };
        _db.CardPayments.Add(payment);
        await _db.SaveChangesAsync(ct);

        return new CardPaymentDto(
            payment.Id, card.Id, card.Name, source?.Id, source?.Name,
            payment.Amount, payment.Currency, payment.Date, payment.Note);
    }

    public async Task<CardPaymentDto> UpdateCardPaymentAsync(int paymentId, CardPaymentCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var payment = await _db.CardPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, ct)
            ?? throw new NotFoundException("El pago no existe.");
        var card = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == payment.CreditCardId && p.UserId == userId, ct)
            ?? throw new NotFoundException("La tarjeta no existe.");
        var currency = card.Currency ?? baseCurrency;

        PaymentMethod? source = null;
        if (dto.SourcePaymentMethodId is not null)
        {
            source = await _db.PaymentMethods
                .FirstOrDefaultAsync(p => p.Id == dto.SourcePaymentMethodId && p.UserId == userId, ct)
                ?? throw new NotFoundException("La cuenta de origen no existe.");
            if (source.Type == PaymentMethodType.CreditCard)
                throw new ValidationException("El pago debe salir de una cuenta de efectivo o débito.");
            var sourceCurrency = source.Currency ?? baseCurrency;
            if (!string.Equals(sourceCurrency, currency, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La cuenta de origen debe estar en la misma moneda que la tarjeta.");
        }

        payment.Amount = dto.Amount;
        payment.SourcePaymentMethodId = source?.Id;
        payment.Date = dto.Date ?? payment.Date;
        payment.Note = dto.Note?.Trim();
        await _db.SaveChangesAsync(ct);

        return new CardPaymentDto(
            payment.Id, card.Id, card.Name, source?.Id, source?.Name,
            payment.Amount, payment.Currency, payment.Date, payment.Note);
    }

    /// <summary>Card payments funded FROM a given cash/debit account (money that left it to pay a card).</summary>
    public async Task<IReadOnlyList<CardPaymentDto>> GetPaymentsFundedFromAsync(int accountId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.CardPayments
            .Where(p => p.UserId == userId && p.SourcePaymentMethodId == accountId)
            .OrderByDescending(p => p.Date)
            .Select(p => new CardPaymentDto(
                p.Id, p.CreditCardId, p.CreditCard!.Name,
                p.SourcePaymentMethodId, p.SourcePaymentMethod != null ? p.SourcePaymentMethod.Name : null,
                p.Amount, p.Currency, p.Date, p.Note))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CardPaymentDto>> GetCardPaymentsAsync(int cardId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.CardPayments
            .Where(p => p.UserId == userId && p.CreditCardId == cardId)
            .OrderByDescending(p => p.Date)
            .Select(p => new CardPaymentDto(
                p.Id, p.CreditCardId, p.CreditCard!.Name,
                p.SourcePaymentMethodId, p.SourcePaymentMethod != null ? p.SourcePaymentMethod.Name : null,
                p.Amount, p.Currency, p.Date, p.Note))
            .ToListAsync(ct);
    }

    public async Task DeleteCardPaymentAsync(int paymentId, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var payment = await _db.CardPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, ct)
            ?? throw new NotFoundException("El pago no existe.");
        _db.CardPayments.Remove(payment);
        await _db.SaveChangesAsync(ct);
    }

    private static PaymentMethodDto Map(
        PaymentMethod m, string baseCurrency,
        decimal spentThisMonth, decimal totalCharged, decimal receivedThisMonth, decimal totalReceived,
        decimal totalPaidToCard, decimal totalFundedFromThis,
        decimal exchangeIn = 0m, decimal exchangeOut = 0m)
    {
        var isCard = m.Type == PaymentMethodType.CreditCard;
        // Credit card: balance is the debt owed (charges minus payments applied to the card).
        // Debit/cash: money in the account (income minus expenses minus card payments funded here,
        // plus money exchanged into this account and minus money exchanged out of it).
        var balance = isCard
            ? totalCharged - totalPaidToCard
            : totalReceived - totalCharged - totalFundedFromThis + exchangeIn - exchangeOut;
        decimal? available = isCard && m.CreditLimit is not null ? m.CreditLimit - balance : null;
        return new PaymentMethodDto(
            m.Id, m.Name, m.Type.ToString(), m.Currency ?? baseCurrency, m.Color, m.Icon,
            m.CreditLimit, m.StatementDay, m.PaymentDueDay, m.Archived, m.IsFavorite,
            spentThisMonth, receivedThisMonth, balance, available);
    }

    private static void ApplyCreditCardFields(PaymentMethod method, PaymentMethodType type, PaymentMethodCreateDto dto)
    {
        if (type == PaymentMethodType.CreditCard)
        {
            method.CreditLimit = dto.CreditLimit;
            method.StatementDay = dto.StatementDay;
            method.PaymentDueDay = dto.PaymentDueDay;
        }
        else
        {
            // Debit/cash never carry a limit or statement dates.
            method.CreditLimit = null;
            method.StatementDay = null;
            method.PaymentDueDay = null;
        }
    }

    private static PaymentMethodType ParseType(string? type) =>
        Enum.TryParse<PaymentMethodType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : PaymentMethodType.Debit;

    private static string NormalizeCurrency(string? requested, string baseCurrency) =>
        string.IsNullOrWhiteSpace(requested) ? baseCurrency : requested.Trim().ToUpperInvariant();
}
