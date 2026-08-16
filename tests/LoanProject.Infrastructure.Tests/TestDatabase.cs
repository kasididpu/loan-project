using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Integration tests hit the real SQL Server dev container (docker compose
/// must be up). Every test uses fresh aggregate ids, so nothing is ever
/// cleaned up — the ledger stays append-only even under test.
/// </summary>
internal static class TestDatabase
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__LoanDb")
        ?? "Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True";

    // Migrate once per run (idempotent), same treatment as TestReadDatabase — so a
    // fresh clone can `dotnet test` without a separate `dotnet ef database update`.
    private static readonly Lazy<bool> Migrated = new(() =>
    {
        using var context = NewContext();
        context.Database.Migrate();
        return true;
    });

    /// <summary>Fresh context per call — tests use separate contexts for write and read-back.</summary>
    public static LoanDbContext CreateContext()
    {
        _ = Migrated.Value; // ensure the schema exists once per run
        return NewContext();
    }

    private static LoanDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        // A fixed test key: integration tests need encrypt/decrypt to round-trip,
        // not to protect anything.
        return new LoanDbContext(options, new AesGcmFieldEncryptor("test-field-encryption-key"));
    }
}
