using Finances.Application.Common;
using Finances.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Notifications;

/// <summary>
/// Emails each user once per day at 8:00 pm Pacific if they have not signed in that day.
/// </summary>
public class ActivityReminderService : BackgroundService
{
    private static readonly TimeZoneInfo Pacific =
        TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityReminderService> _logger;

    public ActivityReminderService(IServiceScopeFactory scopeFactory, ILogger<ActivityReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activity reminder run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Pacific);
        if (nowLocal.Hour < 20) return;

        var today = nowLocal.Date;
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var pending = await users.Users
            .Where(u => u.Email != null && (u.ActivityReminderOn == null || u.ActivityReminderOn < today))
            .ToListAsync(ct);

        foreach (var user in pending)
        {
            var loggedToday = user.LastLoginAt.HasValue
                && TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(user.LastLoginAt.Value, DateTimeKind.Utc), Pacific).Date == today;
            user.ActivityReminderOn = today;
            if (loggedToday)
            {
                await users.UpdateAsync(user);
                continue;
            }

            var name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
            var html = $"""
                <p>Hi {name},</p>
                <p>You have not signed in to Tishe today. Open the app and record today's income and expenses before the day ends.</p>
                <p>Hola {name},</p>
                <p>Hoy no has entrado a Tishe. Abre la app y registra los ingresos y gastos del día.</p>
                """;
            try
            {
                await email.SendAsync(user.Email!, "Tishe: record your transactions today", html, ct);
                await users.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send the 8 pm reminder to {Email}", user.Email);
            }
        }
    }
}
