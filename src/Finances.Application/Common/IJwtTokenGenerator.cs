namespace Finances.Application.Common;

public record TokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    TokenResult Generate(string userId, string email, IEnumerable<string> roles);
}
