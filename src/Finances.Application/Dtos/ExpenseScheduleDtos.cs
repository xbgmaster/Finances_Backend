namespace Finances.Application.Dtos;

public record ExpenseScheduleDto(
    int Id,
    string Name,
    decimal Amount,
    string Currency,
    int CategoryId,
    string CategoryName,
    string? CategoryIcon,
    string? CategoryColor,
    int PaymentMethodId,
    string PaymentMethodName,
    string PaymentMethodType,
    string PayFrequency,
    int DayOfMonth,
    int? SecondDayOfMonth,
    bool AutoPost,
    bool Active,
    DateTime NextChargeDate,
    string? LastPostedPeriod);

public class ExpenseScheduleCreateDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public int CategoryId { get; set; }
    public int PaymentMethodId { get; set; }
    /// <summary>"Monthly" or "SemiMonthly". Defaults to Monthly.</summary>
    public string? PayFrequency { get; set; }
    public int? DayOfMonth { get; set; }
    public int? SecondDayOfMonth { get; set; }
    public bool AutoPost { get; set; } = true;
}

public class ExpenseScheduleUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public int CategoryId { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PayFrequency { get; set; }
    public int? DayOfMonth { get; set; }
    public int? SecondDayOfMonth { get; set; }
    public bool AutoPost { get; set; } = true;
    public bool Active { get; set; } = true;
}
