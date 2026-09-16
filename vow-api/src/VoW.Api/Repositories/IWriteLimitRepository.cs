namespace VoW.Api.Repositories;

/// <summary>
/// Hourly per-key write budget shared by the anonymous write endpoints.
/// </summary>
public interface IWriteLimitRepository
{
    /// <summary>
    /// Records one write against <paramref name="key"/> in the current hour and reports whether
    /// the caller is still inside its budget.
    /// </summary>
    /// <returns><c>true</c> while the key is within <paramref name="limit"/> writes this hour.</returns>
    Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct);
}
