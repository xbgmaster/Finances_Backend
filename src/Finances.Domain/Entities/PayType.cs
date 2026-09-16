namespace Finances.Domain.Entities;

/// <summary>How a job pays, which decides how its income is generated.</summary>
public enum PayType
{
    /// <summary>A fixed amount posted on the pay day of every month (a salary).</summary>
    Fixed = 0,

    /// <summary>
    /// Paid by the hour: the user logs work shifts (hours × rate) and, when the pay-cut day is
    /// reached, all shifts up to that day are totaled into a single income.
    /// </summary>
    Hourly = 1,
}
