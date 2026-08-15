using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.Rates;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Cache decorator against the real Redis container. A counting source
/// makes hits visible: the second identical lookup must not reach it.
/// </summary>
public class RedisRateCacheTests
{
    private static string RedisConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "localhost:6379";

    private sealed class CountingSource(decimal rate) : IInterestRateLookup
    {
        public int Calls { get; private set; }

        public Task<decimal> GetAnnualRateAsync(
            RateType rateType, int termMonths, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(rate);
        }
    }

    [Fact]
    public async Task GetAnnualRateAsync_SecondLookup_ComesFromCacheNotSource()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        var source = new CountingSource(0.18m);
        var cache = new RedisRateCache(redis, source, NullLogger<RedisRateCache>.Instance);
        // Unique term per run: the real Redis keeps keys across test runs.
        var termMonths = 10_000 + Random.Shared.Next(10_000);

        var first = await cache.GetAnnualRateAsync(RateType.Effective, termMonths, CancellationToken.None);
        var second = await cache.GetAnnualRateAsync(RateType.Effective, termMonths, CancellationToken.None);

        Assert.Equal(0.18m, first);
        Assert.Equal(0.18m, second);
        Assert.Equal(1, source.Calls); // the decimal round-tripped through Redis text exactly once
    }

    [Fact]
    public async Task GetAnnualRateAsync_RedisUnreachable_FallsBackToTheSource()
    {
        // Nothing listens on port 1; AbortOnConnectFail=false hands back a
        // multiplexer whose operations fail — exactly the production setup.
        var options = ConfigurationOptions.Parse("localhost:1,connectTimeout=200");
        options.AbortOnConnectFail = false;
        var deadRedis = await ConnectionMultiplexer.ConnectAsync(options);
        var source = new CountingSource(0.16m);
        var cache = new RedisRateCache(deadRedis, source, NullLogger<RedisRateCache>.Instance);

        var rate = await cache.GetAnnualRateAsync(RateType.Effective, 12, CancellationToken.None);

        // A dead cache degrades to a slow lookup, never an error.
        Assert.Equal(0.16m, rate);
        Assert.Equal(1, source.Calls);
    }
}
