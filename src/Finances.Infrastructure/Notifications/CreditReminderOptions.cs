namespace Finances.Infrastructure.Notifications;

/// <summary>
/// Configuration for the background job that emails credit payment reminders.
/// Bound from the "CreditReminders" configuration section.
/// </summary>
public class CreditReminderOptions
{
    /// <summary>Master switch. When false the background service is not registered.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often the job wakes up to look for new alerts. A per-credit dedupe key means
    /// running several times a day does NOT spam the user: an email is only sent when a
    /// credit newly becomes due-soon or overdue.
    /// </summary>
    public double CheckEveryHours { get; set; } = 12;

    /// <summary>Delay before the first run, so it does not race app startup/migrations.</summary>
    public double StartupDelaySeconds { get; set; } = 30;
}
