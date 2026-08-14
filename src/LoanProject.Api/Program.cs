using LoanProject.Application;
using LoanProject.Application.Audit;
using LoanProject.Application.Customers;
using LoanProject.Application.LoanApplications;
using LoanProject.Application.Loans;
using LoanProject.Application.Payments;
using LoanProject.Application.Reports;
using LoanProject.Application.Secrets;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Mongo;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Reports;
using LoanProject.Infrastructure.Secrets;
using LoanProject.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Secrets come from Vault through ISecretProvider (phase 3.5). The
// appsettings values are non-secret local defaults, kept only as the
// Development fallback for when the dev Vault has not been seeded yet.
var connectionString = builder.Configuration.GetConnectionString("LoanDb")
    ?? throw new InvalidOperationException("Connection string 'LoanDb' is not configured.");
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException("Connection string 'Mongo' is not configured.");

var vaultAddress = builder.Configuration["Vault:Address"];
if (!string.IsNullOrWhiteSpace(vaultAddress))
{
    var vaultToken = builder.Configuration["Vault:Token"]
        ?? throw new InvalidOperationException("Vault:Token is not configured.");
    ISecretProvider secretProvider = new VaultSecretProvider(vaultAddress, vaultToken);
    builder.Services.AddSingleton(secretProvider);

    try
    {
        connectionString = await secretProvider.GetSecretAsync("LoanDb", CancellationToken.None);
        mongoConnectionString = await secretProvider.GetSecretAsync("Mongo", CancellationToken.None);
    }
    catch (Exception exception) when (builder.Environment.IsDevelopment())
    {
        // Local dev may boot before the dev vault is seeded — fall back to
        // the non-secret defaults and say so out loud. Anywhere else this
        // rethrows: an unreadable secret store must stop the boot.
        Console.WriteLine($"WARN: Vault unavailable, using local dev defaults. ({exception.Message})");
    }
}

builder.Services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILoanRepository>(_ => new LoanEventStoreRepository(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// The unit of work is the DbContext itself — same scoped instance, second door.
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LoanDbContext>());
builder.Services.AddScoped<IEndOfDaySummaryQuery>(_ => new EndOfDaySummaryQuery(connectionString));
builder.Services.AddScoped<DevDataSeeder>();

// Unlike the scoped DbContext, the Mongo client is a singleton: it is
// thread-safe and owns its connection pool for the whole process.
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<IMongoClient>().GetDatabase("LoanProject"));
builder.Services.AddScoped<IAuditLogWriter, MongoAuditLogWriter>();
builder.Services.AddScoped<ILoanApplicationStore, MongoLoanApplicationStore>();
builder.Services.AddScoped<RecordStripePaymentHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Sample data for local exploration only — real environments are never seeded.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(CancellationToken.None);
}

app.UseHttpsRedirection();

// Endpoint groups live in Api/Endpoints — Program.cs only composes.
app.MapStripeWebhook();

app.Run();
