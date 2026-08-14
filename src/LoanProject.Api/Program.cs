using LoanProject.Application;
using LoanProject.Application.Audit;
using LoanProject.Application.Customers;
using LoanProject.Application.LoanApplications;
using LoanProject.Application.Loans;
using LoanProject.Application.Payments;
using LoanProject.Application.Reports;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Mongo;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("LoanDb")
    ?? throw new InvalidOperationException("Connection string 'LoanDb' is not configured.");
builder.Services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILoanRepository>(_ => new LoanEventStoreRepository(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// The unit of work is the DbContext itself — same scoped instance, second door.
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LoanDbContext>());
builder.Services.AddScoped<IEndOfDaySummaryQuery>(_ => new EndOfDaySummaryQuery(connectionString));
builder.Services.AddScoped<DevDataSeeder>();

var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException("Connection string 'Mongo' is not configured.");
// Unlike the scoped DbContext, the Mongo client is a singleton: it is
// thread-safe and owns its connection pool for the whole process.
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<IMongoClient>().GetDatabase("LoanProject"));
builder.Services.AddScoped<IAuditLogWriter, MongoAuditLogWriter>();
builder.Services.AddScoped<ILoanApplicationStore, MongoLoanApplicationStore>();

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

app.Run();
