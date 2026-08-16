using LoanProject.Infrastructure.Auth;
using StackExchange.Redis;

namespace LoanProject.Infrastructure.Tests;

/// <summary>Integration tests for the Redis-backed OTP store (docker compose must be up).</summary>
public class RedisOtpStoreTests
{
    private static IConnectionMultiplexer Connect()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }

    [Fact]
    public async Task ValidateAndConsume_CorrectCode_SucceedsOnceThenFails()
    {
        using var redis = Connect();
        var store = new RedisOtpStore(redis);
        var userId = Guid.NewGuid();

        await store.StoreAsync(userId, "123456", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.True(await store.ValidateAndConsumeAsync(userId, "123456", CancellationToken.None));
        // Single-use: the first success consumed the code, so a replay fails.
        Assert.False(await store.ValidateAndConsumeAsync(userId, "123456", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAndConsume_WrongCode_Fails()
    {
        using var redis = Connect();
        var store = new RedisOtpStore(redis);
        var userId = Guid.NewGuid();

        await store.StoreAsync(userId, "111111", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.False(await store.ValidateAndConsumeAsync(userId, "999999", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAndConsume_UnknownUser_Fails()
    {
        using var redis = Connect();
        var store = new RedisOtpStore(redis);

        Assert.False(await store.ValidateAndConsumeAsync(Guid.NewGuid(), "000000", CancellationToken.None));
    }
}
