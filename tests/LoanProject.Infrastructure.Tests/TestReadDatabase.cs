using LoanProject.Infrastructure.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Read-side twin of <see cref="TestDatabase"/>: the CQRS Read database on the
/// dev SQL Server. Migrated once per test run (idempotent, same path the app
/// uses on boot), then a fresh context per call. Tests isolate by fresh loan
/// ids, so the shared database is never cleaned up.
/// </summary>
internal static class TestReadDatabase
{
    // Raw string used to build the migrating context — must not trigger migration
    // itself (that would recurse through the ConnectionString getter below).
    private static string RawConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__LoanReadDb")
        ?? "Server=localhost,1433;Database=LoanReadDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True";

    // Reading the connection string ensures the schema exists first, so tests that
    // use this string directly still find the database on a fresh server.
    public static string ConnectionString
    {
        get
        {
            _ = Migrated.Value;
            return RawConnectionString;
        }
    }

    private static readonly Lazy<bool> Migrated = new(() =>
    {
        using var context = NewContext();
        context.Database.Migrate();
        return true;
    });

    public static ReadDbContext CreateContext()
    {
        _ = Migrated.Value; // ensure the schema exists once per run
        return NewContext();
    }

    private static ReadDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ReadDbContext>()
            .UseSqlServer(RawConnectionString)
            .Options;
        return new ReadDbContext(options);
    }
}
