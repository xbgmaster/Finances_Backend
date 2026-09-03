namespace Finances.Application.Dtos;

public record AdminUserDto(
    string Id,
    string Email,
    string? FullName,
    string Role,
    string? Country,
    string Currency,
    bool OnboardingCompleted,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int ExpenseCount);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record MonthCountDto(int Year, int Month, int Count);

public record AdminStatsDto(
    int TotalUsers,
    int AdminUsers,
    int ActiveUsers,
    int NewUsersThisMonth,
    int NeverLoggedIn,
    int InactiveUsers,
    int PendingOnboarding,
    int UsersWithActivity,
    IReadOnlyList<MonthCountDto> SignupsByMonth);

public record UserFilter(
    string? Search,
    string? Role,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 10,
    string? Status = null);
