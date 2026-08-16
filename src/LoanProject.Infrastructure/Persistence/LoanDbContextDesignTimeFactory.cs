using LoanProject.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoanProject.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build LoanDbContext without executing the API's startup
/// (which reads Vault, migrates and seeds). Design-time only — never used at
/// runtime. The connection string comes from the standard env var, falling back
/// to the local dev default; the encryptor is a throwaway because migrations
/// shape the schema and never encrypt or decrypt a value.
/// </summary>
public sealed class LoanDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LoanDbContext>
{
    public LoanDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__LoanDb")
            ?? "Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LoanDbContext(options, new AesGcmFieldEncryptor("design-time-only-key"));
    }
}
