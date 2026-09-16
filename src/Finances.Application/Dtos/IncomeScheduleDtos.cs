using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record IncomeScheduleDto(
    int Id,
    string Name,
    string PayType,
    string PayFrequency,
    decimal Amount,
    decimal? HourlyRate,
    string Currency,
    string? Color,
    int? PaymentMethodId,
    string? PaymentMethodName,
    string? PaymentMethodType,
    int DayOfMonth,
    int? SecondDayOfMonth,
    DateTime? AnchorDate,
    bool AutoPost,
    bool Active,
    DateTime NextPayDate,
    string? LastPostedPeriod);

public class IncomeScheduleCreateDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Fixed" (monthly salary) or "Hourly" (paid by logged shifts). Defaults to Fixed.</summary>
    public string? PayType { get; set; }

    /// <summary>Fixed monthly amount. Required (and &gt; 0) when PayType is Fixed.</summary>
    public decimal Amount { get; set; }

    /// <summary>Default hourly rate. Required (and &gt; 0) when PayType is Hourly.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>ISO currency code. Empty/null falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>Hex color to tint this job's shifts in the calendar. Empty falls back to a default.</summary>
    [MaxLength(9)]
    public string? Color { get; set; }

    /// <summary>Account / payment method the income lands in (optional).</summary>
    public int? PaymentMethodId { get; set; }

    /// <summary>"Weekly", "Biweekly", "SemiMonthly" or "Monthly". Defaults to Monthly.</summary>
    public string? PayFrequency { get; set; }

    /// <summary>First pay day of the month. Required (1-31) for Monthly / SemiMonthly.</summary>
    [Range(1, 31, ErrorMessage = "El día de pago debe estar entre 1 y 31.")]
    public int? DayOfMonth { get; set; }

    /// <summary>Second pay day. Required (1-31, greater than the first) when SemiMonthly.</summary>
    [Range(1, 31, ErrorMessage = "El segundo día de pago debe estar entre 1 y 31.")]
    public int? SecondDayOfMonth { get; set; }

    /// <summary>Reference pay date. Required for Weekly / Biweekly.</summary>
    public DateTime? AnchorDate { get; set; }

    public bool AutoPost { get; set; } = true;
}

public class IncomeScheduleUpdateDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public string? PayType { get; set; }

    public decimal Amount { get; set; }

    public decimal? HourlyRate { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    [MaxLength(9)]
    public string? Color { get; set; }

    public int? PaymentMethodId { get; set; }

    public string? PayFrequency { get; set; }

    [Range(1, 31, ErrorMessage = "El día de pago debe estar entre 1 y 31.")]
    public int? DayOfMonth { get; set; }

    [Range(1, 31, ErrorMessage = "El segundo día de pago debe estar entre 1 y 31.")]
    public int? SecondDayOfMonth { get; set; }

    public DateTime? AnchorDate { get; set; }

    public bool AutoPost { get; set; } = true;

    public bool Active { get; set; } = true;
}

public record WorkShiftDto(
    int Id,
    int IncomeScheduleId,
    string JobName,
    DateTime Date,
    decimal Hours,
    decimal HourlyRate,
    decimal Amount,
    string Currency,
    bool Posted,
    int? IncomeId);

public class WorkShiftCreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un trabajo válido.")]
    public int IncomeScheduleId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Range(0.01, 24, ErrorMessage = "Las horas deben estar entre 0 y 24.")]
    public decimal Hours { get; set; }

    /// <summary>Rate for this shift. Empty/null falls back to the job's default rate.</summary>
    public decimal? HourlyRate { get; set; }
}

public class WorkShiftUpdateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Range(0.01, 24, ErrorMessage = "Las horas deben estar entre 0 y 24.")]
    public decimal Hours { get; set; }

    public decimal? HourlyRate { get; set; }
}

/// <summary>A direct one-off payment attributed to a job, posted as income on the given day.</summary>
public class WorkPaymentCreateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }
}
