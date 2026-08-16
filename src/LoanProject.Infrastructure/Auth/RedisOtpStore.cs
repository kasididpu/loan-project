using System.Security.Cryptography;
using System.Text;
using LoanProject.Application.Auth;
using StackExchange.Redis;

namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// One-time passcode store backed by Redis. The code is kept only as a SHA-256
/// hash under a per-user key with a TTL, so it expires on its own if unused and
/// a Redis dump never reveals a live code. A successful check deletes the key,
/// making every code single-use; the comparison is constant-time.
/// </summary>
public sealed class RedisOtpStore : IOtpStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisOtpStore(IConnectionMultiplexer redis) => _redis = redis;

    public async Task StoreAsync(Guid subjectId, string code, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(KeyFor(subjectId), Hash(code), lifetime);
    }

    public async Task<bool> ValidateAndConsumeAsync(Guid subjectId, string code, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var key = KeyFor(subjectId);

        var stored = (string?)await db.StringGetAsync(key);
        if (stored is null)
            return false;

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(Hash(code)));
        if (!matches)
            return false;

        await db.KeyDeleteAsync(key); // single-use: consume on success
        return true;
    }

    private static string KeyFor(Guid subjectId) => $"otp:{subjectId}";

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
