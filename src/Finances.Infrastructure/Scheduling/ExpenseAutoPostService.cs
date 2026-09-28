using Finances.Application.Services;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Scheduling;

public class ExpenseAutoPostOptions
{
    public bool Enabled { get; set; } = true;
    public double CheckEveryHours { get; set; } = 6;
    public double StartupDelaySeconds { get; set; } = 50;
}

/// <summary>
/// Background job that posts due subscription charges (ExpenseSchedule → Expense) for all users.
/// </summary>
public class ExpenseAutoPostService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExpenseAutoPostOptions _options;
    private readonly ILogger<ExpenseAutoPostService> _logger;

    public ExpenseAutoPostService(
        IServiceScopeFactory scopeFactory,
        ExpenseAutoPostOptions options,
        ILogger<ExpenseAutoPostService> logger)
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
                _logger.LogError(ex, "Expense (subscription) auto-post run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var today = DateTime.UtcNow;

        var schedules = await db.ExpenseSchedules
            .Where(s => s.Active && s.AutoPost)
            .ToListAsync(ct);
        if (schedules.Count == 0) return;

        var posted = 0;
        foreach (var s in schedules)
            posted += ExpenseSchedulePoster.PostDue(db, s, today);

        if (posted > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Posted {Count} subscription expense(s).", posted);
        }
    }
}
