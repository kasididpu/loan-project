using System.Collections.Concurrent;
using LoanProject.Application.Auth;

namespace LoanProject.Api.Tests;

/// <summary>
/// Test double for <see cref="IOtpStore"/> that keeps the last generated code so a
/// test can complete the MFA flow without reading logs. Still single-use, so it
/// exercises the same consume-on-success behavior as the real Redis store.
/// </summary>
public sealed class CapturingOtpStore : IOtpStore
{
    private readonly ConcurrentDictionary<Guid, string> _codes = new();

    public string? LastCode { get; private set; }

    public Task StoreAsync(Guid subjectId, string code, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        _codes[subjectId] = code;
        LastCode = code;
        return Task.CompletedTask;
    }

    public Task<bool> ValidateAndConsumeAsync(Guid subjectId, string code, CancellationToken cancellationToken)
    {
        if (_codes.TryGetValue(subjectId, out var stored) && stored == code)
        {
            _codes.TryRemove(subjectId, out _);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
