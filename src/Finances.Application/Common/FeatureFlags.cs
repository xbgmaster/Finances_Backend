namespace Finances.Application.Common;

/// <summary>
/// Helpers to (de)serialize the per-user feature blocklist stored as a comma-separated string.
/// Keys are normalized to lower-case so comparisons are consistent across API and front-end.
/// </summary>
public static class FeatureFlags
{
    public static IReadOnlyList<string> Parse(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(s => s.ToLowerInvariant())
                 .Distinct()
                 .ToArray();

    public static string? Serialize(IEnumerable<string>? features)
    {
        if (features is null) return null;
        var list = features
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        return list.Length == 0 ? null : string.Join(',', list);
    }
}
