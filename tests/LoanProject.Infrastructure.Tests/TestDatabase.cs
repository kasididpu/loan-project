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
    // The raw string — used to build the migrating context below. It must NOT
    // trigger migration itself, or the ConnectionString getter would recurse.
    private static string RawConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__LoanDb")
        ?? "Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True";

    // Reading the connection string ensures the schema exists first (once per run,
    // via the Lazy below). This matters on a fresh server: the event-store tests
    // open a raw SqlConnection with this string instead of going through
    // CreateContext(), so without this the database might not exist yet.
    public static string ConnectionString
    {
        get
        {
            _ = Migrated.Value;
            return RawConnectionString;
        }
    }

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
            .UseSqlServer(RawConnectionString)
            .Options;
        // Use the app's dev field key, NOT an independent test key: the seed
        // customers have fixed ids shared with the app and the API test assembly,
        // so whichever process seeds first must write ciphertext the others can
        // decrypt. A private test key would make cross-assembly reads fail on a
        // fresh database (CI). This value matches the dev default in Vault.
        return new LoanDbContext(options, new AesGcmFieldEncryptor("loan-dev-field-encryption-key-change-me"));
    }
}
