using System.Globalization;
using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LoanProject.Infrastructure.Rates;

/// <summary>
/// Cache-aside decorator over the rate source: try Redis, fall back to the
/// source, write the answer back with a TTL. Any Redis failure degrades to
/// a slower lookup, never an error — a cache must not be able to break the
/// business path it accelerates.
/// </summary>
public sealed class RedisRateCache(
    IConnectionMultiplexer redis,
    IInterestRateLookup source,
    ILogger<RedisRateCache> logger) : IInterestRateLookup
{
    // Rates change on business decisions, not per request — five minutes of
    // staleness is acceptable and keeps the demo's hit/miss cycle visible.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<decimal> GetAnnualRateAsync(
        RateType rateType, int termMonths, CancellationToken cancellationToken)
    {
        var key = $"rate:{rateType}:{termMonths}";

        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key);
            if (cached.HasValue)
            {
                logger.LogDebug("Rate cache hit for {Key}.", key);
                // Invariant round-trip: the decimal is stored as text, so
                // culture must not touch it in either direction.
                return decimal.Parse(cached!, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            logger.LogWarning(exception, "Rate cache read failed for {Key}; using the source.", key);
        }

        var rate = await source.GetAnnualRateAsync(rateType, termMonths, cancellationToken);

        try
        {
            await redis.GetDatabase().StringSetAsync(
                key, rate.ToString(CultureInfo.InvariantCulture), CacheTtl);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            logger.LogWarning(exception, "Rate cache write failed for {Key}.", key);
        }

        return rate;
    }
}
