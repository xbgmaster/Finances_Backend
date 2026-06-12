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
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    int ExpenseCount);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record MonthCountDto(int Year, int Month, int Count);

public record AdminStatsDto(
    int TotalUsers,
    int AdminUsers,
    int ActiveUsers,
    int NewUsersThisMonth,
    decimal TotalIncome,
    decimal TotalExpense,
    int TotalExpenses,
    IReadOnlyList<MonthCountDto> SignupsByMonth,
    IReadOnlyList<AdminUserDto> TopUsersBySpend);

public record UserFilter(
    string? Search,
    string? Role,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 10);
