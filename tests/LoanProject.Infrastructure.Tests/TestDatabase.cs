using LoanProject.Infrastructure.Persistence;
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

    /// <summary>Fresh context per call — tests use separate contexts for write and read-back.</summary>
    public static LoanDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new LoanDbContext(options);
    }
}
