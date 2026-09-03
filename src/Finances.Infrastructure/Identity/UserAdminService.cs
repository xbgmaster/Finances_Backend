using System.Globalization;
using System.Text;
using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Finances.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Finances.Infrastructure.Identity;

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly FinanceDbContext _db;

    public UserAdminService(UserManager<ApplicationUser> users, FinanceDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(UserFilter filter, CancellationToken ct = default)
    {
        var adminIds = await GetAdminIdsAsync(ct);
        var query = ApplyFilter(_users.Users.AsQueryable(), filter, adminIds);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var counts = await ExpenseCountsAsync(users.Select(u => u.Id).ToList(), ct);
        var items = users.Select(u => Map(u, adminIds, counts)).ToList();
        return new PagedResult<AdminUserDto>(items, total, page, pageSize);
    }

    public async Task<AdminUserDto> GetUserAsync(string id, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id) ?? throw new NotFoundException("User not found.");
        var adminIds = await GetAdminIdsAsync(ct);
        var counts = await ExpenseCountsAsync(new[] { user.Id }, ct);
        return Map(user, adminIds, counts);
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var activeSince = now.AddDays(-30);

        var adminIds = await GetAdminIdsAsync(ct);
        var totalUsers = await _users.Users.CountAsync(ct);
        var newUsersThisMonth = await _users.Users.CountAsync(u => u.CreatedAt >= monthStart, ct);
        var activeUsers = await _users.Users.CountAsync(u => u.LastLoginAt != null && u.LastLoginAt >= activeSince, ct);
        var neverLoggedIn = await _users.Users.CountAsync(u => u.LastLoginAt == null, ct);
        var inactiveUsers = await _users.Users.CountAsync(u => u.LastLoginAt != null && u.LastLoginAt < activeSince, ct);
        var pendingOnboarding = await _users.Users.CountAsync(u => !u.OnboardingCompleted, ct);
        var usersWithActivity = await _db.Expenses.Select(e => e.UserId).Distinct().CountAsync(ct);

        var since = monthStart.AddMonths(-5);
        var createdDates = await _users.Users
            .Where(u => u.CreatedAt >= since)
            .Select(u => u.CreatedAt)
            .ToListAsync(ct);

        var signups = new List<MonthCountDto>();
        for (var i = 0; i < 6; i++)
        {
            var month = since.AddMonths(i);
            var count = createdDates.Count(d => d.Year == month.Year && d.Month == month.Month);
            signups.Add(new MonthCountDto(month.Year, month.Month, count));
        }

        return new AdminStatsDto(
            totalUsers, adminIds.Count, activeUsers, newUsersThisMonth,
            neverLoggedIn, inactiveUsers, pendingOnboarding, usersWithActivity,
            signups);
    }

    public async Task<byte[]> ExportUsersCsvAsync(UserFilter filter, CancellationToken ct = default)
    {
        var adminIds = await GetAdminIdsAsync(ct);
        var query = ApplyFilter(_users.Users.AsQueryable(), filter, adminIds);
        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        var counts = await ExpenseCountsAsync(users.Select(u => u.Id).ToList(), ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Email,FullName,Role,Country,Currency,OnboardingCompleted,CreatedAt,LastLoginAt,ExpenseCount");
        foreach (var u in users)
        {
            var d = Map(u, adminIds, counts);
            sb.AppendLine(string.Join(",",
                Csv(d.Id), Csv(d.Email), Csv(d.FullName), Csv(d.Role), Csv(d.Country), Csv(d.Currency),
                d.OnboardingCompleted, d.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                d.LastLoginAt?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
                d.ExpenseCount));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private IQueryable<ApplicationUser> ApplyFilter(
        IQueryable<ApplicationUser> query, UserFilter filter, HashSet<string> adminIds)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(s)));
        }
        if (filter.From is not null) query = query.Where(u => u.CreatedAt >= filter.From);
        if (filter.To is not null) query = query.Where(u => u.CreatedAt <= filter.To);

        if (string.Equals(filter.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => adminIds.Contains(u.Id));
        else if (string.Equals(filter.Role, "User", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !adminIds.Contains(u.Id));

        var activeSince = DateTime.UtcNow.AddDays(-30);
        var status = filter.Status?.Trim();
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.LastLoginAt != null && u.LastLoginAt >= activeSince);
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.LastLoginAt != null && u.LastLoginAt < activeSince);
        else if (string.Equals(status, "Never", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.LastLoginAt == null);
        else if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !u.OnboardingCompleted);
        else if (string.Equals(status, "NoActivity", StringComparison.OrdinalIgnoreCase))
        {
            var withActivity = _db.Expenses.Select(e => e.UserId);
            query = query.Where(u => !withActivity.Contains(u.Id));
        }

        return query;
    }

    private async Task<HashSet<string>> GetAdminIdsAsync(CancellationToken ct)
    {
        var roleId = await _db.Roles.Where(r => r.Name == AuthService.AdminRole)
            .Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (roleId is null) return new HashSet<string>();
        var ids = await _db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);
        return ids.ToHashSet();
    }

    private async Task<Dictionary<string, int>> ExpenseCountsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<string, int>();
        return await _db.Expenses
            .Where(e => userIds.Contains(e.UserId))
            .GroupBy(e => e.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private static AdminUserDto Map(ApplicationUser u, HashSet<string> adminIds, Dictionary<string, int> expenseCount)
    {
        var role = adminIds.Contains(u.Id) ? AuthService.AdminRole : AuthService.UserRole;
        return new AdminUserDto(
            u.Id, u.Email ?? string.Empty, u.FullName, role, u.Country, u.Currency,
            u.OnboardingCompleted, u.CreatedAt, u.LastLoginAt, expenseCount.GetValueOrDefault(u.Id));
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
