using Finances.Application.Services;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Scheduling;

public class IncomeAutoPostOptions
{
    public bool Enabled { get; set; } = true;
    public double CheckEveryHours { get; set; } = 6;
    public double StartupDelaySeconds { get; set; } = 45;
}

/// <summary>
/// Background job that scans every user's active auto-post income schedules ("jobs") and posts the
/// matching income once each monthly pay day is reached. Idempotent: the poster advances each
/// schedule's LastPostedPeriod, so a pay is never posted twice even across restarts or downtime.
/// </summary>
public class IncomeAutoPostService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IncomeAutoPostOptions _options;
    private readonly ILogger<IncomeAutoPostService> _logger;

    public IncomeAutoPostService(
        IServiceScopeFactory scopeFactory,
        IncomeAutoPostOptions options,
        ILogger<IncomeAutoPostService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var period = TimeSpan.FromHours(Math.Max(0.1, _options.CheckEveryHours));
        using var timer = new PeriodicTimer(period);

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
                _logger.LogError(ex, "Income auto-post run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

        var today = DateTime.UtcNow;
        var schedules = await db.IncomeSchedules
            .Where(s => s.Active && s.AutoPost)
            .ToListAsync(ct);
        if (schedules.Count == 0) return;

        // Preload unposted shifts for hourly jobs so each pay cut can total them.
        var hourlyIds = schedules
            .Where(s => s.PayType == Domain.Entities.PayType.Hourly)
            .Select(s => s.Id)
            .ToList();
        var shiftsByJob = hourlyIds.Count == 0
            ? new Dictionary<int, List<Domain.Entities.WorkShift>>()
            : (await db.WorkShifts
                .Where(w => w.IncomeId == null && hourlyIds.Contains(w.IncomeScheduleId))
                .ToListAsync(ct))
                .GroupBy(w => w.IncomeScheduleId)
                .ToDictionary(g => g.Key, g => g.ToList());

        var posted = schedules.Sum(s => IncomeSchedulePoster.PostDue(
            db, s, today, shiftsByJob.TryGetValue(s.Id, out var list) ? list : null));
        if (posted > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Auto-posted {Count} scheduled income(s).", posted);
        }
    }
}
